using System;
using System.Threading;
using System.Threading.Tasks;

namespace TalkLens.Collector.Infrastructure.RateLimiting;

/// <summary>
/// Интерфейс для ограничения частоты запросов
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Выполняет запрос с учетом ограничений
    /// </summary>
    /// <typeparam name="T">Тип результата</typeparam>
    /// <param name="methodName">Имя метода для отслеживания</param>
    /// <param name="factory">Фабрика для выполнения запроса</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат запроса</returns>
    Task<T> ExecuteWithRateLimitAsync<T>(
        string methodName, 
        Func<Task<T>> factory, 
        CancellationToken cancellationToken = default);
        
    /// <summary>
    /// Очищает историю запросов для указанного метода
    /// </summary>
    /// <param name="methodName">Имя метода</param>
    void ClearMethodHistory(string methodName);
} 