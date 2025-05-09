using System;

namespace TalkLens.Collector.Domain.Models.Telegram;

/// <summary>
/// Модель данных рекомендации для пользователя Telegram
/// </summary>
public class TelegramUserRecommendationData
{
    /// <summary>
    /// Идентификатор рекомендации
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Идентификатор сессии
    /// </summary>
    public string SessionId { get; set; }
    
    /// <summary>
    /// Идентификатор пользователя Telegram
    /// </summary>
    public long TelegramUserId { get; set; }
    
    /// <summary>
    /// Идентификатор собеседника
    /// </summary>
    public long InterlocutorId { get; set; }
    
    /// <summary>
    /// Текст рекомендации
    /// </summary>
    public string RecommendationText { get; set; }
    
    /// <summary>
    /// Дата создания рекомендации
    /// </summary>
    public DateTime CreatedAt { get; set; }
} 