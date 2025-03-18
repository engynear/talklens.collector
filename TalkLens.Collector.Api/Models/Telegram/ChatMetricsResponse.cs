namespace TalkLens.Collector.Api.Models.Telegram;

public enum AttachmentType
{
    Secure, // Надежный
    Anxious, // Тревожный
    Avoidant // Избегающий
}

public sealed record ChatMetricsResponse
{
    public MessageMetrics MyMetrics { get; init; } = null!;
    public MessageMetrics InterlocutorMetrics { get; init; } = null!;
}

public sealed record MessageMetrics
{
    public int MessageCount { get; init; }
    public int ComplimentCount { get; init; }
    public double EngagementPercentage { get; init; }
    
    public double AverageResponseTimeSeconds { get; init; }
    public PersonalityDefinition AttachmentDefinition { get; init; } = null!;
}

public sealed record PersonalityDefinition
{
    public required string Type { get; init; }
    public required double Confidence { get; init; } // От 0 до 100
} 