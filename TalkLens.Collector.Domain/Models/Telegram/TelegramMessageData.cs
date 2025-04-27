namespace TalkLens.Collector.Domain.Models.Telegram;

/// <summary>
/// Данные о сообщении Telegram
/// </summary>
public class TelegramMessageData
{
    /// <summary>
    /// Идентификатор сообщения
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
    /// Идентификатор пользователя Telegram (владельца аккаунта)
    /// </summary>
    public long TelegramUserId { get; set; }
    
    /// <summary>
    /// Идентификатор собеседника Telegram
    /// </summary>
    public long TelegramInterlocutorId { get; set; }
    
    /// <summary>
    /// Идентификатор отправителя сообщения
    /// </summary>
    public long SenderId { get; set; }
    
    /// <summary>
    /// Время отправки сообщения
    /// </summary>
    public DateTime MessageTime { get; set; }
} 