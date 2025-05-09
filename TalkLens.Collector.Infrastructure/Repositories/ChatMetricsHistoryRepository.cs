using LinqToDB;
using TalkLens.Collector.Domain.Enums.Telegram;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Database;

namespace TalkLens.Collector.Infrastructure.Repositories;

/// <summary>
/// Репозиторий для работы с историей метрик чата
/// </summary>
public class ChatMetricsHistoryRepository : IChatMetricsHistoryRepository
{
    private readonly Func<TalkLensDbContext> _dbFactory;

    public ChatMetricsHistoryRepository(Func<TalkLensDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMetricsHistoryData>> GetLatestMetricsAsync(
        string sessionId,
        long telegramUserId,
        long interlocutorId,
        CancellationToken cancellationToken)
    {
        await using var db = _dbFactory();
        
        // Получаем последнюю запись для каждой роли
        var userMetrics = await GetLatestMetricsForRoleAsync(sessionId, telegramUserId, interlocutorId, ChatRole.User, cancellationToken);
        var interlocutorMetrics = await GetLatestMetricsForRoleAsync(sessionId, telegramUserId, interlocutorId, ChatRole.Interlocutor, cancellationToken);
        
        var result = new List<ChatMetricsHistoryData>();
        
        if (userMetrics != null)
            result.Add(userMetrics);
            
        if (interlocutorMetrics != null)
            result.Add(interlocutorMetrics);
            
        return result;
    }

    /// <inheritdoc />
    public async Task<ChatMetricsHistoryData?> GetLatestMetricsForRoleAsync(
        string sessionId, 
        long telegramUserId, 
        long interlocutorId, 
        ChatRole role, 
        CancellationToken cancellationToken)
    {
        await using var db = _dbFactory();
        
        var entity = await db.ChatMetricsHistory
            .Where(m => 
                m.SessionId == sessionId && 
                m.TelegramUserId == telegramUserId && 
                m.InterlocutorId == interlocutorId && 
                m.Role == role.ToString().ToLower())
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
            
        return entity != null ? MapToData(entity) : null;
    }

    /// <inheritdoc />
    public async Task<ChatMetricsHistoryData> SaveMetricsAsync(
        ChatMetricsHistoryData metrics, 
        CancellationToken cancellationToken)
    {
        await using var db = _dbFactory();
        
        var entity = MapToEntity(metrics);
        
        // Устанавливаем текущую дату, если не задана
        if (entity.Timestamp == default)
            entity.Timestamp = DateTime.UtcNow;
            
        await db.InsertAsync(entity, token: cancellationToken);
        metrics.Id = entity.Id;
        metrics.Timestamp = entity.Timestamp;
        
        return metrics;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ChatMetricsHistoryData>> GetMetricsHistoryAsync(
        string sessionId, 
        long telegramUserId, 
        long interlocutorId, 
        ChatRole? role, 
        DateTime fromDate, 
        DateTime toDate, 
        CancellationToken cancellationToken)
    {
        await using var db = _dbFactory();
        
        var query = db.ChatMetricsHistory
            .Where(m => 
                m.SessionId == sessionId && 
                m.TelegramUserId == telegramUserId && 
                m.InterlocutorId == interlocutorId &&
                m.Timestamp >= fromDate &&
                m.Timestamp <= toDate);
                
        // Фильтрация по роли, если указана
        if (role.HasValue)
            query = query.Where(m => m.Role == role.Value.ToString().ToLower());
            
        // Сортировка по дате (от старых к новым)
        var entities = await query.OrderBy(m => m.Timestamp).ToListAsync(cancellationToken);
        
        return entities.Select(MapToData);
    }
    
    /// <summary>
    /// Преобразует сущность БД в объект данных
    /// </summary>
    private static ChatMetricsHistoryData MapToData(ChatMetricsHistoryEntity entity)
    {
        return new ChatMetricsHistoryData
        {
            Id = entity.Id,
            SessionId = entity.SessionId,
            TelegramUserId = entity.TelegramUserId,
            InterlocutorId = entity.InterlocutorId,
            Role = Enum.Parse<ChatRole>(entity.Role, true),
            Timestamp = entity.Timestamp,
            ComplimentsDelta = entity.ComplimentsDelta,
            TotalCompliments = entity.TotalCompliments,
            EngagementScore = entity.EngagementScore,
            AttachmentType = string.IsNullOrEmpty(entity.AttachmentType) 
                ? null 
                : Enum.Parse<AttachmentType>(entity.AttachmentType, true),
            AttachmentConfidence = entity.AttachmentConfidence
        };
    }
    
    /// <summary>
    /// Преобразует объект данных в сущность БД
    /// </summary>
    private static ChatMetricsHistoryEntity MapToEntity(ChatMetricsHistoryData data)
    {
        return new ChatMetricsHistoryEntity
        {
            Id = data.Id,
            SessionId = data.SessionId,
            TelegramUserId = data.TelegramUserId,
            InterlocutorId = data.InterlocutorId,
            Role = data.Role.ToString().ToLower(),
            Timestamp = data.Timestamp,
            ComplimentsDelta = data.ComplimentsDelta,
            TotalCompliments = data.TotalCompliments,
            EngagementScore = data.EngagementScore,
            AttachmentType = data.AttachmentType?.ToString(),
            AttachmentConfidence = data.AttachmentConfidence
        };
    }
} 