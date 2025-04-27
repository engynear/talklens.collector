using LinqToDB;
using LinqToDB.Data;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Database;

namespace TalkLens.Collector.Infrastructure.Repositories;

/// <summary>
/// Репозиторий для работы с сообщениями Telegram
/// </summary>
public class TelegramMessageRepository : ITelegramMessageRepository
{
    private readonly TalkLensDbContext _db;

    public TelegramMessageRepository(TalkLensDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<TelegramMessageData> SaveMessageAsync(TelegramMessageData messageData, CancellationToken cancellationToken)
    {
        var entity = MapToEntity(messageData);
        
        await _db.InsertAsync(entity, token: cancellationToken);
        messageData.Id = entity.Id;
        
        return messageData;
    }

    /// <inheritdoc />
    public async Task<int> SaveMessagesAsync(IEnumerable<TelegramMessageData> messages, CancellationToken cancellationToken)
    {
        var entities = messages.Select(MapToEntity).ToList();
        
        if (!entities.Any())
            return 0;
            
        await _db.BulkCopyAsync(entities, cancellationToken: cancellationToken);
        return entities.Count;
    }

    /// <inheritdoc />
    public async Task<List<TelegramMessageData>> GetMessagesAsync(
        string userId, 
        string sessionId, 
        long interlocutorId, 
        CancellationToken cancellationToken)
    {
        var entities = await _db.TelegramMessages
            .Where(m => 
                m.UserId == userId && 
                m.SessionId == sessionId && 
                m.TelegramInterlocutorId == interlocutorId)
            .OrderBy(m => m.MessageTime)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToData).ToList();
    }

    private static TelegramMessageData MapToData(TelegramMessageEntity entity)
    {
        return new TelegramMessageData
        {
            Id = entity.Id,
            UserId = entity.UserId,
            SessionId = entity.SessionId,
            TelegramUserId = entity.TelegramUserId,
            TelegramInterlocutorId = entity.TelegramInterlocutorId,
            SenderId = entity.SenderId,
            MessageTime = entity.MessageTime
        };
    }

    private static TelegramMessageEntity MapToEntity(TelegramMessageData data)
    {
        return new TelegramMessageEntity
        {
            Id = data.Id,
            UserId = data.UserId,
            SessionId = data.SessionId,
            TelegramUserId = data.TelegramUserId,
            TelegramInterlocutorId = data.TelegramInterlocutorId,
            SenderId = data.SenderId,
            MessageTime = data.MessageTime
        };
    }
} 