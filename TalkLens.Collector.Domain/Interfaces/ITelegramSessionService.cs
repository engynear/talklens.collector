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

    Task<List<TelegramContactResponse>> GetContactsAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken);

    Task<List<TelegramSessionData>> GetActiveSessionsAsync(
        string userId,
        CancellationToken cancellationToken);

    Task SubscribeToUpdatesAsync(
        string userId,
        string sessionId,
        long interlocutorId,
        CancellationToken cancellationToken);

    Task UnsubscribeFromUpdatesAsync(
        string userId,
        string sessionId,
        long interlocutorId,
        CancellationToken cancellationToken);

    Task<List<TelegramContactResponse>> GetSubscribedContactsAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken);
}