using TalkLens.Collector.Domain.Enums.Telegram;

namespace TalkLens.Collector.Domain.Models.Telegram;

public sealed record TelegramLoginResult
{
    public required TelegramLoginStatus Status { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Error { get; init; }
} 