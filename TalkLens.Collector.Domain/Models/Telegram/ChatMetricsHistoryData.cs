using TalkLens.Collector.Domain.Enums.Telegram;

namespace TalkLens.Collector.Domain.Models.Telegram;

/// <summary>
/// Данные истории метрик чата
/// </summary>
public class ChatMetricsHistoryData
{
    /// <summary>
    /// Идентификатор записи
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// Идентификатор сессии
    /// </summary>
    public string SessionId { get; set; } = null!;
    
    /// <summary>
    /// Идентификатор пользователя Telegram
    /// </summary>
    public long TelegramUserId { get; set; }
    
    /// <summary>
    /// Идентификатор собеседника
    /// </summary>
    public long InterlocutorId { get; set; }
    
    /// <summary>
    /// Роль в чате (пользователь или собеседник)
    /// </summary>
    public ChatRole Role { get; set; }
    
    /// <summary>
    /// Временная метка записи
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Изменение количества комплиментов
    /// </summary>
    public int? ComplimentsDelta { get; set; }
    
    /// <summary>
    /// Общее количество комплиментов
    /// </summary>
    public int? TotalCompliments { get; set; }
    
    /// <summary>
    /// Оценка вовлеченности (от 0 до 100)
    /// </summary>
    public float? EngagementScore { get; set; }
    
    /// <summary>
    /// Тип привязанности
    /// </summary>
    public AttachmentType? AttachmentType { get; set; }
    
    /// <summary>
    /// Уверенность в определении типа привязанности (от 0 до 1)
    /// </summary>
    public float? AttachmentConfidence { get; set; }
} 