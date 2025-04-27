using System;

namespace TalkLens.Collector.Infrastructure.Configuration;

/// <summary>
/// Настройки для работы с Telegram API
/// </summary>
public class TelegramOptions
{
    /// <summary>
    /// API ID приложения Telegram
    /// </summary>
    public int ApiId { get; set; }
    
    /// <summary>
    /// API Hash приложения Telegram
    /// </summary>
    public string ApiHash { get; set; }
    
    /// <summary>
    /// Путь к локальной директории для кэширования сессий
    /// </summary>
    public string LocalCachePath { get; set; }
    
    /// <summary>
    /// Настройки ограничения запросов
    /// </summary>
    public RateLimitOptions RateLimit { get; set; } = new();
    
    /// <summary>
    /// Настройки Redis для кэширования
    /// </summary>
    public RedisOptions Redis { get; set; } = new();
} 