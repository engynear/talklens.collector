namespace TalkLens.Collector.Api.Models.Telegram;

/// <summary>
/// Ответ с информацией о подписке на контакт Telegram
/// </summary>
public class TelegramSubscriptionResponse
{
    /// <summary>
    /// Идентификатор подписки
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// Идентификатор сессии Telegram
    /// </summary>
    public string SessionId { get; set; } = null!;
    
    /// <summary>
    /// Идентификатор контакта Telegram
    /// </summary>
    public long InterlocutorId { get; set; }
    
    /// <summary>
    /// Дата создания подписки
    /// </summary>
    public DateTime CreatedAt { get; set; }
} 