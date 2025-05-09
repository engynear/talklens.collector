namespace TalkLens.Collector.Domain.Models.Telegram;

/// <summary>
/// Данные о метриках чата
/// </summary>
public class ChatMetricsData
{
    /// <summary>
    /// Пустой объект метрик чата
    /// </summary>
    public static readonly ChatMetricsData Empty = new();
    
    /// <summary>
    /// Метрики сообщений пользователя
    /// </summary>
    public MessageMetricsData MyMetrics { get; set; } = new();
    
    /// <summary>
    /// Метрики сообщений собеседника
    /// </summary>
    public MessageMetricsData InterlocutorMetrics { get; set; } = new();
}

/// <summary>
/// Данные о метриках сообщений
/// </summary>
public class MessageMetricsData
{
    /// <summary>
    /// Количество сообщений
    /// </summary>
    public int MessageCount { get; set; }
    
    /// <summary>
    /// Количество комплиментов
    /// </summary>
    public int ComplimentCount { get; set; }
    
    /// <summary>
    /// Процент вовлеченности
    /// </summary>
    public double EngagementPercentage { get; set; }
    
    /// <summary>
    /// Среднее время ответа в секундах
    /// </summary>
    public double AverageResponseTimeSeconds { get; set; }
    
    /// <summary>
    /// Тип личности
    /// </summary>
    public string AttachmentType { get; set; } = string.Empty;
    
    /// <summary>
    /// Уверенность в определении типа личности (от 0 до 100)
    /// </summary>
    public double AttachmentConfidence { get; set; }
} 