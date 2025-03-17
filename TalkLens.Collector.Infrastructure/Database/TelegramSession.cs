using LinqToDB.Mapping;

namespace TalkLens.Collector.Infrastructure.Database;

[Table("telegram_sessions")]
public class TelegramSessionEntity
{
    [PrimaryKey]
    [Column("id")]
    public Guid Id { get; set; }
    
    [Column("user_id")]
    public string UserId { get; set; } = null!;
    
    [Column("session_id")]
    public string SessionId { get; set; } = null!;
    
    [Column("phone_number")]
    public string PhoneNumber { get; set; } = null!;
    
    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
    
    [Column("last_activity_at")]
    public DateTime? LastActivityAt { get; set; }
    
    [Column("is_active")]
    public bool IsActive { get; set; }
} 