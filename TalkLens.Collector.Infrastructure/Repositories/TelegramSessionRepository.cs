using LinqToDB;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Database;

namespace TalkLens.Collector.Infrastructure.Repositories;

public class TelegramSessionRepository : ITelegramSessionRepository
{
    private readonly TalkLensDbContext _db;

    public TelegramSessionRepository(TalkLensDbContext db)
    {
        _db = db;
    }

    public async Task<TelegramSessionData?> GetActiveSessionAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        var entity = await _db.TelegramSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && 
                                    s.SessionId == sessionId && 
                                    s.IsActive, 
                                    cancellationToken);

        return entity == null ? null : MapToData(entity);
    }

    public async Task<TelegramSessionData> SaveSessionAsync(TelegramSessionData session, CancellationToken cancellationToken)
    {
        var entity = MapToEntity(session);
        await _db.InsertAsync(entity, token: cancellationToken);
        return session;
    }

    public async Task UpdateSessionStatusAsync(string userId, string sessionId, bool isActive, CancellationToken cancellationToken)
    {
        await _db.TelegramSessions
            .Where(s => s.UserId == userId && s.SessionId == sessionId)
            .Set(s => s.IsActive, isActive)
            .Set(s => s.LastActivityAt, DateTime.UtcNow)
            .UpdateAsync(cancellationToken);
    }

    private static TelegramSessionData MapToData(TelegramSessionEntity entity)
    {
        var now = DateTime.UtcNow;
        return new TelegramSessionData
        {
            Id = entity.Id,
            UserId = entity.UserId,
            SessionId = entity.SessionId,
            PhoneNumber = entity.PhoneNumber,
            CreatedAt = entity.CreatedAt ?? now,
            LastActivityAt = entity.LastActivityAt ?? now,
            IsActive = entity.IsActive
        };
    }

    private static TelegramSessionEntity MapToEntity(TelegramSessionData data)
    {
        return new TelegramSessionEntity
        {
            Id = data.Id,
            UserId = data.UserId,
            SessionId = data.SessionId,
            PhoneNumber = data.PhoneNumber,
            CreatedAt = data.CreatedAt,
            LastActivityAt = data.LastActivityAt,
            IsActive = data.IsActive
        };
    }
} 