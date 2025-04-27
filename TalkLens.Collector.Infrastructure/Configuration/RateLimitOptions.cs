using System;

namespace TalkLens.Collector.Infrastructure.Configuration;

/// <summary>
/// Настройки ограничения запросов к Telegram API
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// Максимальное количество запросов в указанный период времени
    /// </summary>
    public int MaxRequests { get; set; } = 30;
    
    /// <summary>
    /// Период времени для ограничения запросов
    /// </summary>
    public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(1);
} 