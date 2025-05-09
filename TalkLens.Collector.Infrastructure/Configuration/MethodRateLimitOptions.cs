namespace TalkLens.Collector.Infrastructure.Configuration;

/// <summary>
/// Настройки ограничения запросов для конкретного метода API
/// </summary>
public class MethodRateLimitOptions
{
    /// <summary>
    /// Количество запросов в минуту
    /// </summary>
    public int RequestsPerMinute { get; set; }
    
    /// <summary>
    /// Количество запросов в час
    /// </summary>
    public int RequestsPerHour { get; set; }
    
    /// <summary>
    /// Время ожидания при ограничении запросов (в секундах)
    /// </summary>
    public int CooldownSeconds { get; set; }
} 