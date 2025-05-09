using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Database;

namespace TalkLens.Collector.Infrastructure.Repositories;

/// <summary>
/// Репозиторий для работы с рекомендациями пользователей Telegram
/// </summary>
public class TelegramUserRecommendationRepository(Func<TalkLensDbContext> dbFactory) : ITelegramUserRecommendationRepository
{
    /// <inheritdoc />
    public async Task<IEnumerable<TelegramUserRecommendationData>> GetRecommendationsAsync(
        string sessionId,
        long interlocutorId,
        CancellationToken cancellationToken)
    {
        await using var db = dbFactory();
        var entities = await db.TelegramUserRecommendations
            .Where(r => r.SessionId == sessionId && r.InterlocutorId == interlocutorId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
            
        return entities.Select(MapToData);
    }
    
    /// <inheritdoc />
    public async Task<TelegramUserRecommendationData?> GetLastRecommendationAsync(
        string sessionId,
        long interlocutorId,
        CancellationToken cancellationToken)
    {
        await using var db = dbFactory();
        var entity = await db.TelegramUserRecommendations
            .Where(r => r.SessionId == sessionId && r.InterlocutorId == interlocutorId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
            
        return entity != null ? MapToData(entity) : null;
    }
    
    private static TelegramUserRecommendationData MapToData(TelegramUserRecommendationEntity entity)
    {
        return new TelegramUserRecommendationData
        {
            Id = entity.Id,
            SessionId = entity.SessionId,
            TelegramUserId = entity.TelegramUserId,
            InterlocutorId = entity.InterlocutorId,
            RecommendationText = entity.RecommendationText,
            CreatedAt = entity.CreatedAt
        };
    }
    
    private static TelegramUserRecommendationEntity MapToEntity(TelegramUserRecommendationData data)
    {
        return new TelegramUserRecommendationEntity
        {
            Id = data.Id,
            SessionId = data.SessionId,
            TelegramUserId = data.TelegramUserId,
            InterlocutorId = data.InterlocutorId,
            RecommendationText = data.RecommendationText,
            CreatedAt = data.CreatedAt
        };
    }
} 