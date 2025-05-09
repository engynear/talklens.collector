namespace TalkLens.Collector.Domain.Models.Telegram;

/// <summary>
/// Модель данных сессии Telegram
/// </summary>
public class TelegramSessionData : SessionData
{
    /// <summary>
    /// Номер телефона, связанный с этой сессией
    /// </summary>
    public string PhoneNumber { get; set; }
    
    /// <summary>
    /// Идентификатор пользователя в Telegram
    /// </summary>
    public long? TelegramUserId { get; set; }
    
    public TelegramSessionData()
    {
        SessionType = "Telegram";
    }
} 