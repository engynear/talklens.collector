namespace TalkLens.Collector.Infrastructure.Configuration;

/// <summary>
/// Настройки кеширования для конкретного метода API
/// </summary>
public class MethodCacheOptions
{
    /// <summary>
    /// Время жизни кеша в минутах
    /// </summary>
    public int ExpirationMinutes { get; set; }
} 