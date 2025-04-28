using LinqToDB;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Database;

namespace TalkLens.Collector.Infrastructure.Repositories;

/// <summary>
/// Репозиторий для работы с подписками на контакты Telegram
/// </summary>
public class TelegramSubscriptionRepository : ITelegramSubscriptionRepository
{
    private readonly Func<TalkLensDbContext> _dbFactory;

    public TelegramSubscriptionRepository(Func<TalkLensDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <inheritdoc />
    public async Task<TelegramSubscriptionData> AddSubscriptionAsync(TelegramSubscriptionData subscription, CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        var entity = MapToEntity(subscription);
        entity.CreatedAt = DateTime.UtcNow;
        
        await db.InsertAsync(entity, token: cancellationToken);
        subscription.Id = entity.Id;
        subscription.CreatedAt = entity.CreatedAt;
        
        return subscription;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveSubscriptionAsync(string userId, string sessionId, long interlocutorId, CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        int deleted = await db.TelegramSubscriptions
            .Where(s => s.UserId == userId && 
                      s.SessionId == sessionId && 
                      s.TelegramInterlocutorId == interlocutorId)
            .DeleteAsync(cancellationToken);
            
        return deleted > 0;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsSubscriptionAsync(string userId, string sessionId, long interlocutorId, CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        return await db.TelegramSubscriptions
            .AnyAsync(s => s.UserId == userId && 
                         s.SessionId == sessionId && 
                         s.TelegramInterlocutorId == interlocutorId, 
                         cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<TelegramSubscriptionData>> GetSessionSubscriptionsAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        var entities = await db.TelegramSubscriptions
            .Where(s => s.UserId == userId && s.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToData).ToList();
    }
    
    /// <inheritdoc />
    public async Task<bool> ExistsAnySubscriptionAsync(string sessionId, long interlocutorId, CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        return await db.TelegramSubscriptions
            .AnyAsync(s => s.SessionId == sessionId && 
                         s.TelegramInterlocutorId == interlocutorId, 
                         cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<List<TelegramSubscriptionData>> GetAllSessionSubscriptionsAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        var entities = await db.TelegramSubscriptions
            .Where(s => s.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToData).ToList();
    }

    private static TelegramSubscriptionData MapToData(TelegramSubscriptionEntity entity)
    {
        return new TelegramSubscriptionData
        {
            Id = entity.Id,
            UserId = entity.UserId,
            SessionId = entity.SessionId,
            TelegramInterlocutorId = entity.TelegramInterlocutorId,
            CreatedAt = entity.CreatedAt
        };
    }

    private static TelegramSubscriptionEntity MapToEntity(TelegramSubscriptionData data)
    {
        return new TelegramSubscriptionEntity
        {
            Id = data.Id,
            UserId = data.UserId,
            SessionId = data.SessionId,
            TelegramInterlocutorId = data.TelegramInterlocutorId,
            CreatedAt = data.CreatedAt
        };
    }
} 