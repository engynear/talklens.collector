namespace TalkLens.Collector.Api.Models.Telegram;

public sealed record SessionResponse
{
    public required string SessionId { get; init; }
    public required string Status { get; init; }
    
    public string? PhoneNumber { get; init; }
    
    public string? Error { get; init; }
}