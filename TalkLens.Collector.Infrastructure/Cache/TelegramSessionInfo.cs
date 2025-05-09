namespace TalkLens.Collector.Infrastructure.Cache;

/// <summary>
/// Информация о сессии для хранения в кэше
/// </summary>
public class TelegramSessionInfo
{
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string SessionFilePath { get; set; } = string.Empty;
    public string UpdatesFilePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
} 