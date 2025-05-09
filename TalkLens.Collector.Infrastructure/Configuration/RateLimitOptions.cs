namespace TalkLens.Collector.Infrastructure.Configuration;

/// <summary>
/// Настройки ограничения запросов к Telegram API
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// Имя секции в конфигурации
    /// </summary>
    public const string SectionName = "Telegram:RateLimit";
    
    /// <summary>
    /// Количество запросов в минуту
    /// </summary>
    public int RequestsPerMinute { get; set; } = 20;
    
    /// <summary>
    /// Количество запросов в час
    /// </summary>
    public int RequestsPerHour { get; set; } = 300;
    
    /// <summary>
    /// Время ожидания при ограничении запросов (в секундах)
    /// </summary>
    public int CooldownSeconds { get; set; } = 3;
    
    /// <summary>
    /// Настройки ограничения для конкретных методов
    /// </summary>
    public Dictionary<string, MethodRateLimitOptions> MethodLimits { get; set; } = new();
} 