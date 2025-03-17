using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Domain.Interfaces;

public interface ITelegramSessionRepository
{
    Task<TelegramSessionData?> GetActiveSessionAsync(string userId, string sessionId, CancellationToken cancellationToken);
    Task<TelegramSessionData> SaveSessionAsync(TelegramSessionData session, CancellationToken cancellationToken);
    Task UpdateSessionStatusAsync(string userId, string sessionId, bool isActive, CancellationToken cancellationToken);
} 