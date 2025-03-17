namespace TalkLens.Collector.Domain.Enums.Telegram;

public enum TelegramLoginStatus
{
    Pending,
    VerificationCodeRequired,
    TwoFactorRequired,
    Success,
    Failed,
    Expired
}