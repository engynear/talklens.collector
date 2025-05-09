using System;

namespace TalkLens.Collector.Api.Models.Telegram;

/// <summary>
/// Модель ответа с рекомендациями для пользователя Telegram
/// </summary>
public class TelegramUserRecommendationResponse
{
    /// <summary>
    /// Идентификатор рекомендации
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Текст рекомендации
    /// </summary>
    public string RecommendationText { get; set; }
    
    /// <summary>
    /// Дата создания рекомендации
    /// </summary>
    public DateTime CreatedAt { get; set; }
} 