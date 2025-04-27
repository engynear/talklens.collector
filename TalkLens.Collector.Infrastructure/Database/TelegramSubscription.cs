using LinqToDB.Mapping;

namespace TalkLens.Collector.Infrastructure.Database;

[Table("telegram_subscriptions")]
public class TelegramSubscriptionEntity
{
    [PrimaryKey, Identity]
    [Column("id")]
    public long Id { get; set; }
    
    [Column("user_id")]
    public string UserId { get; set; } = null!;
    
    [Column("session_id")]
    public string SessionId { get; set; } = null!;
    
    [Column("telegram_interlocutor_id")]
    public long TelegramInterlocutorId { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
} 