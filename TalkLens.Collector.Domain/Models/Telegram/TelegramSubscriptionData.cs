namespace TalkLens.Collector.Domain.Models.Telegram;

/// <summary>
/// Данные о подписке на контакт Telegram
/// </summary>
public class TelegramSubscriptionData
{
    /// <summary>
    /// Идентификатор подписки
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// Идентификатор пользователя
    /// </summary>
    public string UserId { get; set; } = null!;
    
    /// <summary>
    /// Идентификатор сессии Telegram
    /// </summary>
    public string SessionId { get; set; } = null!;
    
    /// <summary>
    /// Идентификатор контакта Telegram (собеседника)
    /// </summary>
    public long TelegramInterlocutorId { get; set; }
    
    /// <summary>
    /// Дата создания подписки
    /// </summary>
    public DateTime CreatedAt { get; set; }
} 