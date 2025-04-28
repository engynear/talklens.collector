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
    private readonly Func<TalkLensDbContext> _dbFactory;

    public TelegramMessageRepository(Func<TalkLensDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <inheritdoc />
    public async Task<TelegramMessageData> SaveMessageAsync(TelegramMessageData messageData, CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        var entity = MapToEntity(messageData);
        await db.InsertAsync(entity, token: cancellationToken);
        messageData.Id = entity.Id;
        return messageData;
    }

    /// <inheritdoc />
    public async Task<int> SaveMessagesAsync(IEnumerable<TelegramMessageData> messages, CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        var entities = messages.Select(MapToEntity).ToList();
        if (!entities.Any())
            return 0;
        await db.BulkCopyAsync(entities, cancellationToken: cancellationToken);
        return entities.Count;
    }

    /// <inheritdoc />
    public async Task<List<TelegramMessageData>> GetMessagesAsync(
        string userId, 
        string sessionId, 
        long interlocutorId, 
        CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        var entities = await db.TelegramMessages
            .Where(m => 
                m.UserId == userId && 
                m.SessionId == sessionId && 
                m.TelegramInterlocutorId == interlocutorId)
            .OrderBy(m => m.MessageTime)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToData).ToList();
    }
    
    /// <inheritdoc />
    public async Task<int> GetUserMessageCountAsync(
        string userId, 
        string sessionId, 
        long interlocutorId, 
        long telegramUserId, 
        CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        return await db.TelegramMessages
            .Where(m => 
                m.UserId == userId && 
                m.SessionId == sessionId && 
                m.TelegramInterlocutorId == interlocutorId &&
                m.SenderId == telegramUserId)
            .CountAsync(cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<int> GetInterlocutorMessageCountAsync(
        string userId, 
        string sessionId, 
        long interlocutorId, 
        CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        return await db.TelegramMessages
            .Where(m => 
                m.UserId == userId && 
                m.SessionId == sessionId && 
                m.TelegramInterlocutorId == interlocutorId &&
                m.SenderId == interlocutorId)
            .CountAsync(cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<double> GetUserAverageResponseTimeAsync(
        string userId, 
        string sessionId, 
        long interlocutorId, 
        long telegramUserId, 
        CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        // Получаем все сообщения, отсортированные по времени
        var messages = await db.TelegramMessages
            .Where(m => 
                m.UserId == userId && 
                m.SessionId == sessionId && 
                m.TelegramInterlocutorId == interlocutorId)
            .OrderBy(m => m.MessageTime)
            .ToListAsync(cancellationToken);
        
        if (messages.Count < 2)
            return 0;
        
        // Для расчета времени ответа нам нужны два списка: собеседник -> пользователь
        var interlocutorMessages = messages.Where(m => m.SenderId == interlocutorId).ToList();
        var userMessages = messages.Where(m => m.SenderId == telegramUserId).ToList();
        
        if (interlocutorMessages.Count == 0 || userMessages.Count == 0)
            return 0;
        
        // Рассчитаем среднее время ответа пользователя на сообщения собеседника
        return CalculateAverageResponseTime(interlocutorMessages, userMessages);
    }
    
    /// <inheritdoc />
    public async Task<double> GetInterlocutorAverageResponseTimeAsync(
        string userId, 
        string sessionId, 
        long interlocutorId, 
        long telegramUserId, 
        CancellationToken cancellationToken)
    {
        using var db = _dbFactory();
        // Получаем все сообщения, отсортированные по времени
        var messages = await db.TelegramMessages
            .Where(m => 
                m.UserId == userId && 
                m.SessionId == sessionId && 
                m.TelegramInterlocutorId == interlocutorId)
            .OrderBy(m => m.MessageTime)
            .ToListAsync(cancellationToken);
        
        if (messages.Count < 2)
            return 0;
        
        // Для расчета времени ответа нам нужны два списка: пользователь -> собеседник
        var userMessages = messages.Where(m => m.SenderId == telegramUserId).ToList();
        var interlocutorMessages = messages.Where(m => m.SenderId == interlocutorId).ToList();
        
        if (userMessages.Count == 0 || interlocutorMessages.Count == 0)
            return 0;
        
        // Рассчитаем среднее время ответа собеседника на сообщения пользователя
        return CalculateAverageResponseTime(userMessages, interlocutorMessages);
    }

    private static double CalculateAverageResponseTime(List<TelegramMessageEntity> senderMessages, List<TelegramMessageEntity> receiverMessages)
    {
        // Список разниц во времени между сообщениями
        var responseTimes = new List<double>();
        
        // Сортируем сообщения по времени
        var sortedSenderMessages = senderMessages.OrderBy(m => m.MessageTime).ToList();
        var sortedReceiverMessages = receiverMessages.OrderBy(m => m.MessageTime).ToList();
        
        // Перебираем сообщения отправителя
        foreach (var senderMessage in sortedSenderMessages)
        {
            // Ищем ближайшее сообщение получателя, которое было отправлено после сообщения отправителя
            var nextReceiverMessage = sortedReceiverMessages
                .FirstOrDefault(m => m.MessageTime > senderMessage.MessageTime);
                
            if (nextReceiverMessage != null)
            {
                // Получаем разницу времени в секундах
                var responseTime = (nextReceiverMessage.MessageTime - senderMessage.MessageTime).TotalSeconds;
                responseTimes.Add(responseTime);
                
                // Удаляем этот ответ из списка, чтобы не считать его дважды
                sortedReceiverMessages.Remove(nextReceiverMessage);
            }
        }
        
        // Возвращаем среднее время ответа
        return responseTimes.Any() ? responseTimes.Average() : 0;
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