using LinqToDB.Mapping;

namespace TalkLens.Collector.Infrastructure.Database;

[Table("chat_metrics_history")]
public class ChatMetricsHistoryEntity
{
    [PrimaryKey, Identity]
    [Column("id")]
    public long Id { get; set; }
    
    [Column("session_id")]
    public string SessionId { get; set; } = null!;
    
    [Column("telegram_user_id")]
    public long TelegramUserId { get; set; }
    
    [Column("interlocutor_id")]
    public long InterlocutorId { get; set; }
    
    [Column("role")]
    public string Role { get; set; } = null!;
    
    [Column("ts")]
    public DateTime Timestamp { get; set; }
    
    [Column("compliments_delta")]
    public int? ComplimentsDelta { get; set; }
    
    [Column("total_compliments")]
    public int? TotalCompliments { get; set; }
    
    [Column("engagement_score")]
    public float? EngagementScore { get; set; }
    
    [Column("attachment_type")]
    public string? AttachmentType { get; set; }
    
    [Column("attachment_confidence")]
    public float? AttachmentConfidence { get; set; }
} 