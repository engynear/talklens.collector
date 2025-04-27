using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace TalkLens.Collector.Infrastructure.Services.Telegram;

/// <summary>
/// Класс для ограничения количества запросов к Telegram API
/// </summary>
public class TelegramRateLimiter
{
    private readonly ILogger<TelegramRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _methodSemaphores = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastRequestTimes = new();

    // Настройки лимитов
    private readonly int _defaultRequestsPerMinute;
    private readonly int _defaultRequestsPerHour;
    private readonly TimeSpan _defaultCooldownPeriod;

    // Лимиты для конкретных методов (если нужны специальные ограничения)
    private readonly ConcurrentDictionary<string, (int PerMinute, int PerHour, TimeSpan Cooldown)> _methodLimits = new();

    public TelegramRateLimiter(IConfiguration configuration, ILogger<TelegramRateLimiter> logger)
    {
        _logger = logger;
        
        // Загружаем настройки лимитов из конфигурации
        _defaultRequestsPerMinute = configuration.GetValue<int>("Telegram:RateLimit:RequestsPerMinute", 20);
        _defaultRequestsPerHour = configuration.GetValue<int>("Telegram:RateLimit:RequestsPerHour", 300);
        int cooldownSeconds = configuration.GetValue<int>("Telegram:RateLimit:CooldownSeconds", 3);
        _defaultCooldownPeriod = TimeSpan.FromSeconds(cooldownSeconds);
        
        // Инициализируем специальные лимиты для конкретных методов
        InitializeMethodLimits(configuration);
        
        _logger.LogInformation("TelegramRateLimiter инициализирован. Стандартные лимиты: {PerMinute} запросов в минуту, {PerHour} запросов в час, период охлаждения {Cooldown} сек.", 
            _defaultRequestsPerMinute, _defaultRequestsPerHour, cooldownSeconds);
    }
    
    private void InitializeMethodLimits(IConfiguration configuration)
    {
        // Загружаем специальные лимиты для методов, если они заданы в конфигурации
        var methodLimitsSection = configuration.GetSection("Telegram:RateLimit:MethodLimits");
        foreach (var methodSection in methodLimitsSection.GetChildren())
        {
            string methodName = methodSection.Key;
            int perMinute = methodSection.GetValue<int>("RequestsPerMinute", _defaultRequestsPerMinute);
            int perHour = methodSection.GetValue<int>("RequestsPerHour", _defaultRequestsPerHour);
            int cooldownSeconds = methodSection.GetValue<int>("CooldownSeconds", (int)_defaultCooldownPeriod.TotalSeconds);
            
            _methodLimits[methodName] = (perMinute, perHour, TimeSpan.FromSeconds(cooldownSeconds));
            _logger.LogDebug("Установлены специальные лимиты для метода {Method}: {PerMinute} запросов в минуту, {PerHour} запросов в час, охлаждение {Cooldown} сек.", 
                methodName, perMinute, perHour, cooldownSeconds);
        }
    }
    
    /// <summary>
    /// Ожидает, пока возможно выполнить запрос к API в соответствии с ограничениями
    /// </summary>
    /// <param name="methodName">Имя метода Telegram API</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True, если запрос разрешен, иначе False</returns>
    public async Task<bool> WaitForPermissionAsync(string methodName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Получаем семафор для метода
            var semaphore = _methodSemaphores.GetOrAdd(methodName, _ => new SemaphoreSlim(1, 1));
            
            // Получаем лимиты для метода
            var (perMinute, perHour, cooldown) = _methodLimits.GetValueOrDefault(methodName, 
                (_defaultRequestsPerMinute, _defaultRequestsPerHour, _defaultCooldownPeriod));
            
            // Ждем доступа к семафору
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Проверяем последний запрос и выдерживаем минимальную паузу
                if (_lastRequestTimes.TryGetValue(methodName, out var lastRequestTime))
                {
                    var timeSinceLastRequest = DateTime.UtcNow - lastRequestTime;
                    if (timeSinceLastRequest < cooldown)
                    {
                        var delayTime = cooldown - timeSinceLastRequest;
                        _logger.LogDebug("Ожидание {Delay}мс перед выполнением метода {Method}", delayTime.TotalMilliseconds, methodName);
                        await Task.Delay(delayTime, cancellationToken);
                    }
                }
                
                // Обновляем время последнего запроса
                _lastRequestTimes[methodName] = DateTime.UtcNow;
                
                return true;
            }
            finally
            {
                semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Ожидание разрешения на выполнение метода {Method} было отменено", methodName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке лимитов для метода {Method}: {Error}", methodName, ex.Message);
            return false;
        }
    }
    
    /// <summary>
    /// Выполняет функцию с учетом ограничений API
    /// </summary>
    /// <typeparam name="T">Тип результата</typeparam>
    /// <param name="methodName">Имя метода Telegram API</param>
    /// <param name="func">Функция для выполнения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат выполнения функции</returns>
    public async Task<T> ExecuteWithRateLimitAsync<T>(string methodName, Func<Task<T>> func, CancellationToken cancellationToken = default)
    {
        if (await WaitForPermissionAsync(methodName, cancellationToken))
        {
            _logger.LogDebug("Выполнение метода {Method}", methodName);
            return await func();
        }
        
        throw new OperationCanceledException($"Операция {methodName} была отменена");
    }
} 