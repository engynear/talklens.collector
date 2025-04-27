using System;

namespace TalkLens.Collector.Infrastructure.Configuration;

/// <summary>
/// Настройки Redis для хранения сессий Telegram
/// </summary>
public class RedisOptions
{
    /// <summary>
    /// Имя секции в конфигурации приложения
    /// </summary>
    public const string SectionName = "Redis";
    
    /// <summary>
    /// Строка подключения к Redis
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";
    
    /// <summary>
    /// Префикс ключа для хранения сессий Telegram
    /// </summary>
    public string KeyPrefix { get; set; } = "telegram:session:";
    
    /// <summary>
    /// Альтернативное имя свойства для обратной совместимости
    /// </summary>
    public string TelegramSessionPrefix 
    { 
        get => KeyPrefix; 
        set => KeyPrefix = value; 
    }
    
    /// <summary>
    /// Время жизни кэша в секундах (по умолчанию 7 дней)
    /// </summary>
    public int ExpiryTimeSeconds { get; set; } = 604800;
    
    /// <summary>
    /// Время жизни сессий в Redis (в часах)
    /// </summary>
    public int SessionExpirationHours 
    { 
        get => ExpiryTimeSeconds / 3600; 
        set => ExpiryTimeSeconds = value * 3600; 
    }
} 