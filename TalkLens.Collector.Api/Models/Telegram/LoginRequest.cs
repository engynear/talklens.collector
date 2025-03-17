namespace TalkLens.Collector.Api.Models.Telegram;

public sealed record LoginRequest
{
    public required string SessionId { get; init; }
    public required string Phone { get; init; }
}

