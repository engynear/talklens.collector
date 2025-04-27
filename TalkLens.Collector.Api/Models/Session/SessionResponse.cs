namespace TalkLens.Collector.Api.Models.Session;

/// <summary>
/// Модель ответа для сессий
/// </summary>
public record SessionResponse
{
    /// <summary>
    /// Уникальный идентификатор сессии
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Идентификатор сессии (клиентский)
    /// </summary>
    public string SessionId { get; set; }
    
    /// <summary>
    /// Тип сессии (например, "Telegram", "WhatsApp" и т.д.)
    /// </summary>
    public string SessionType { get; set; }
    
    /// <summary>
    /// Дата создания сессии
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Дата последней активности сессии
    /// </summary>
    public DateTime LastActivityAt { get; set; }
} 