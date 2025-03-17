namespace TalkLens.Collector.Api.Models.Telegram;

public sealed record TwoFactorRequest
{
    public required string SessionId { get; init; }
    public required string Password { get; init; }
}