namespace TalkLens.Collector.Domain.Models.Telegram;

public class TelegramSessionData
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public string SessionId { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public bool IsActive { get; set; }
} 