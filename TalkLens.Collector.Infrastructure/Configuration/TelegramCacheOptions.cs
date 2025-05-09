namespace TalkLens.Collector.Infrastructure.Configuration;

/// <summary>
/// Настройки для кеширования Telegram API
/// </summary>
public class TelegramCacheOptions
{
    /// <summary>
    /// Имя секции в конфигурации
    /// </summary>
    public const string SectionName = "Telegram:Cache";
    
    /// <summary>
    /// Время жизни кеша в минутах (по умолчанию 30 минут)
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 30;
    
    /// <summary>
    /// Настройки кеширования для конкретных методов
    /// </summary>
    public Dictionary<string, MethodCacheOptions> MethodExpirations { get; set; } = new();
} 