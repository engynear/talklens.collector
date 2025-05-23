using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TalkLens.Collector.Infrastructure.Configuration;

namespace TalkLens.Collector.Infrastructure.RateLimiting;

/// <summary>
/// Реализация ограничителя запросов для Telegram API
/// </summary>
public class TelegramRateLimiter : IRateLimiter
{
    private readonly ILogger<TelegramRateLimiter> _logger;
    private readonly TelegramOptions _options;
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _methodHistory = new();
    private readonly TimeSpan _timeWindow;  // Окно времени для отслеживания запросов
    private readonly int _maxRequests;      // Максимальное количество запросов в окне

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="TelegramRateLimiter"/>
    /// </summary>
    /// <param name="options">Настройки Telegram</param>
    /// <param name="logger">Логгер</param>
    public TelegramRateLimiter(
        IOptions<TelegramOptions> options,
        ILogger<TelegramRateLimiter> logger)
    {
        _options = options.Value;
        _logger = logger;
        
        // Преобразуем новые настройки в формат, используемый в этом классе
        _timeWindow = TimeSpan.FromMinutes(1); // Стандартное окно в 1 минуту
        _maxRequests = _options.RateLimit.RequestsPerMinute; // Используем ограничение запросов в минуту
    }

    /// <inheritdoc/>
    public async Task<T> ExecuteWithRateLimitAsync<T>(
        string methodName,
        Func<Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        await EnsureRateLimitAsync(methodName, cancellationToken);
        
        try
        {
            var result = await factory();
            
            // Добавляем текущее время в историю запросов
            var methodQueue = _methodHistory.GetOrAdd(methodName, _ => new Queue<DateTime>());
            lock (methodQueue)
            {
                methodQueue.Enqueue(DateTime.UtcNow);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выполнении метода {MethodName}", methodName);
            throw;
        }
    }

    /// <inheritdoc/>
    public void ClearMethodHistory(string methodName)
    {
        if (_methodHistory.TryGetValue(methodName, out var queue))
        {
            lock (queue)
            {
                queue.Clear();
            }
        }
    }

    private async Task EnsureRateLimitAsync(string methodName, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var queue = _methodHistory.GetOrAdd(methodName, _ => new Queue<DateTime>());
        
        TimeSpan? delay = null;
        
        lock (queue)
        {
            // Удаляем старые записи старше TimeWindow
            while (queue.Count > 0 && now - queue.Peek() > _timeWindow)
            {
                queue.Dequeue();
            }
            
            // Если достигли лимита, вычисляем задержку
            if (queue.Count >= _maxRequests)
            {
                var oldestCall = queue.Peek();
                var timeToWait = _timeWindow - (now - oldestCall);
                
                if (timeToWait > TimeSpan.Zero)
                {
                    delay = timeToWait;
                }
            }
        }
        
        if (delay.HasValue)
        {
            _logger.LogWarning("Достигнут лимит запросов для метода {MethodName}. Ожидание {Delay} мс", 
                methodName, delay.Value.TotalMilliseconds);
                
            await Task.Delay(delay.Value, cancellationToken);
        }
    }
} 