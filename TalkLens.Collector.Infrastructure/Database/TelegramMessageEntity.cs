using LinqToDB.Mapping;

namespace TalkLens.Collector.Infrastructure.Database;

[Table("telegram_messages")]
public class TelegramMessageEntity
{
    [PrimaryKey, Identity]
    [Column("id")]
    public long Id { get; set; }
    
    [Column("user_id")]
    public string UserId { get; set; } = null!;
    
    [Column("session_id")]
    public string SessionId { get; set; } = null!;
    
    [Column("telegram_user_id")]
    public long TelegramUserId { get; set; }
    
    [Column("telegram_interlocutor_id")]
    public long TelegramInterlocutorId { get; set; }
    
    [Column("sender_id")]
    public long SenderId { get; set; }
    
    [Column("message_time")]
    public DateTime MessageTime { get; set; }
}