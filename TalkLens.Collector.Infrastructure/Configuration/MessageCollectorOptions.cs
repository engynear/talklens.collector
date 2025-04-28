namespace TalkLens.Collector.Infrastructure.Configuration;

/// <summary>
/// Настройки для коллектора сообщений
/// </summary>
public class MessageCollectorOptions
{
    /// <summary>
    /// Имя секции в конфигурации приложения
    /// </summary>
    public const string SectionName = "MessageCollector";
    
    /// <summary>
    /// Интервал сохранения сообщений в минутах 
    /// (по умолчанию 5 минут)
    /// </summary>
    public int SaveIntervalMinutes { get; set; } = 5;
    
    /// <summary>
    /// Максимальный размер очереди сообщений для одной сессии 
    /// (при достижении этого порога сообщения будут сохранены принудительно)
    /// </summary>
    public int MaxQueueSizePerSession { get; set; } = 1000;
} 