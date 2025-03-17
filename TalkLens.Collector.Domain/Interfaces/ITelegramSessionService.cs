using TalkLens.Collector.Domain.Enums.Telegram;
using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Domain.Interfaces;

public interface ITelegramSessionService
{
    Task<TelegramLoginResult> LoginWithPhoneAsync(
        string userId, 
        string sessionId, 
        string phone, 
        CancellationToken cancellationToken);
    
    Task<TelegramLoginResult> SubmitVerificationCodeAsync(
        string userId,
        string sessionId,
        string code,
        CancellationToken cancellationToken);
    
    Task<TelegramLoginResult> SubmitTwoFactorPasswordAsync(
        string userId,
        string sessionId,
        string password,
        CancellationToken cancellationToken);
    
    Task<TelegramLoginResult> GetSessionStatusAsync(
        string userId,
        string sessionId, 
        CancellationToken cancellationToken);
}