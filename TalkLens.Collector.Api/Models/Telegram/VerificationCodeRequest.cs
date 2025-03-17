namespace TalkLens.Collector.Api.Models.Telegram;

public sealed record VerificationCodeRequest
{
    public required string SessionId { get; init; }
    public required string Code { get; init; }
}