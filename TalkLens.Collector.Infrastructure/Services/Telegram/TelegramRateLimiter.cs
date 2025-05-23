using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using TalkLens.Collector.Infrastructure.Configuration;

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

    public TelegramRateLimiter(IOptions<TelegramOptions> telegramOptions, ILogger<TelegramRateLimiter> logger)
    {
        _logger = logger;
        
        // Загружаем настройки лимитов из опций
        var rateLimitOptions = telegramOptions.Value.RateLimit;
        
        _defaultRequestsPerMinute = rateLimitOptions.RequestsPerMinute;
        _defaultRequestsPerHour = rateLimitOptions.RequestsPerHour;
        _defaultCooldownPeriod = TimeSpan.FromSeconds(rateLimitOptions.CooldownSeconds);
        
        // Инициализируем специальные лимиты для конкретных методов
        InitializeMethodLimits(rateLimitOptions);
        
        _logger.LogInformation("TelegramRateLimiter инициализирован. Стандартные лимиты: {PerMinute} запросов в минуту, {PerHour} запросов в час, период охлаждения {Cooldown} сек.", 
            _defaultRequestsPerMinute, _defaultRequestsPerHour, rateLimitOptions.CooldownSeconds);
    }
    
    private void InitializeMethodLimits(RateLimitOptions rateLimitOptions)
    {
        // Загружаем специальные лимиты для методов из опций
        foreach (var methodEntry in rateLimitOptions.MethodLimits)
        {
            var methodName = methodEntry.Key;
            var methodOptions = methodEntry.Value;
            
            var perMinute = methodOptions.RequestsPerMinute;
            var perHour = methodOptions.RequestsPerHour;
            var cooldownSeconds = methodOptions.CooldownSeconds;
            
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