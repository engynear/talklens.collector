using LinqToDB;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Database;

namespace TalkLens.Collector.Infrastructure.Repositories;

public class TelegramSessionRepository(Func<TalkLensDbContext> dbFactory) : ITelegramSessionRepository
{
    public async Task<TelegramSessionData?> GetActiveSessionAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        await using var db = dbFactory();
        var entity = await db.TelegramSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && 
                                    s.SessionId == sessionId && 
                                    s.IsActive, 
                                    cancellationToken);
        return entity == null ? null : MapToData(entity);
    }

    public async Task<TelegramSessionData> SaveSessionAsync(TelegramSessionData session, CancellationToken cancellationToken)
    {
        await using var db = dbFactory();
        var entity = MapToEntity(session);
        await db.InsertAsync(entity, token: cancellationToken);
        return session;
    }

    public async Task UpdateSessionStatusAsync(string userId, string sessionId, bool isActive, CancellationToken cancellationToken)
    {
        await using var db = dbFactory();
        await db.TelegramSessions
            .Where(s => s.UserId == userId && s.SessionId == sessionId)
            .Set(s => s.IsActive, isActive)
            .Set(s => s.LastActivityAt, DateTime.UtcNow)
            .UpdateAsync(cancellationToken);
    }

    public async Task<List<TelegramSessionData>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = dbFactory();
        var entities = await db.TelegramSessions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToData).ToList();
    }
    
    public async Task<List<TelegramSessionData>> GetAllActiveSessionsAsync(CancellationToken cancellationToken)
    {
        await using var db = dbFactory();
        var entities = await db.TelegramSessions
            .Where(s => s.IsActive)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToData).ToList();
    }

    public async Task<bool> ExistsActiveSessionWithPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        await using var db = dbFactory();
        return await db.TelegramSessions
            .AnyAsync(s => s.PhoneNumber == phoneNumber && s.IsActive, cancellationToken);
    }
    
    public async Task<bool> DeleteSessionAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        await using var db = dbFactory();
        var deleted = await db.TelegramSessions
            .Where(s => s.UserId == userId && s.SessionId == sessionId)
            .DeleteAsync(cancellationToken);
            
        return deleted > 0;
    }
    
    public async Task<int> DeleteAllUserSessionsAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = dbFactory();
        return await db.TelegramSessions
            .Where(s => s.UserId == userId)
            .DeleteAsync(cancellationToken);
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