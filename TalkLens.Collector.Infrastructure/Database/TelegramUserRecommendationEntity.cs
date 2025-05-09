using System;
using LinqToDB.Mapping;

namespace TalkLens.Collector.Infrastructure.Database;

[Table("telegram_user_recommendations")]
public class TelegramUserRecommendationEntity
{
    [PrimaryKey, Identity]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("session_id")]
    public string SessionId { get; set; }
    
    [Column("telegram_user_id")]
    public long TelegramUserId { get; set; }
    
    [Column("interlocutor_id")]
    public long InterlocutorId { get; set; }
    
    [Column("recommendation_text")]
    public string RecommendationText { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
} 