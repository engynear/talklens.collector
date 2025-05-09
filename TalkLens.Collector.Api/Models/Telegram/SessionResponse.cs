namespace TalkLens.Collector.Api.Models.Telegram;

/// <summary>
/// Модель ответа для статуса сессии Telegram
/// </summary>
public class TelegramSessionStatusResponse
{
    /// <summary>
    /// Идентификатор сессии (клиентский)
    /// </summary>
    public string SessionId { get; set; }
    
    /// <summary>
    /// Статус авторизации
    /// </summary>
    public string Status { get; set; }
    
    /// <summary>
    /// Номер телефона
    /// </summary>
    public string PhoneNumber { get; set; }
    
    /// <summary>
    /// Сообщение об ошибке (если есть)
    /// </summary>
    public string Error { get; set; }
}