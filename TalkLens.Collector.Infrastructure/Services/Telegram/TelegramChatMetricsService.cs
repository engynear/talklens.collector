using TalkLens.Collector.Domain.Enums.Telegram;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Infrastructure.Services.Telegram;

/// <summary>
/// Сервис для работы с метриками чата
/// </summary>
public class TelegramChatMetricsService : IChatMetricsService
{
    private readonly ITelegramMessageRepository _telegramMessageRepository;
    private readonly IChatMetricsHistoryRepository _chatMetricsHistoryRepository;

    public TelegramChatMetricsService(
        ITelegramMessageRepository telegramMessageRepository,
        IChatMetricsHistoryRepository chatMetricsHistoryRepository)
    {
        _telegramMessageRepository = telegramMessageRepository;
        _chatMetricsHistoryRepository = chatMetricsHistoryRepository;
    }

    /// <inheritdoc />
    public async Task<ChatMetricsData> GetChatMetricsAsync(
        string userId, 
        string sessionId, 
        long interlocutorId, 
        CancellationToken cancellationToken)
    {
        // Получаем первое сообщение, чтобы определить ID пользователя Telegram
        var messages = await _telegramMessageRepository.GetMessagesAsync(
            userId, 
            sessionId, 
            interlocutorId, 
            cancellationToken);
        
        if (messages.Count == 0)
        {
            return ChatMetricsData.Empty;
        }
        
        // Определяем идентификатор пользователя Telegram из первого сообщения
        var telegramUserId = messages[0].TelegramUserId;
        
        // Получаем количество сообщений пользователя
        var userMessageCount = await _telegramMessageRepository.GetUserMessageCountAsync(
            userId, 
            sessionId, 
            interlocutorId, 
            telegramUserId, 
            cancellationToken);
        
        // Получаем количество сообщений собеседника
        var interlocutorMessageCount = await _telegramMessageRepository.GetInterlocutorMessageCountAsync(
            userId, 
            sessionId, 
            interlocutorId, 
            cancellationToken);
        
        // Получаем среднее время ответа пользователя
        var userAverageResponseTime = await _telegramMessageRepository.GetUserAverageResponseTimeAsync(
            userId, 
            sessionId, 
            interlocutorId, 
            telegramUserId, 
            cancellationToken);
        
        // Получаем среднее время ответа собеседника
        var interlocutorAverageResponseTime = await _telegramMessageRepository.GetInterlocutorAverageResponseTimeAsync(
            userId, 
            sessionId, 
            interlocutorId, 
            telegramUserId, 
            cancellationToken);
            
        // Получаем последние метрики из истории метрик
        var latestUserMetrics = await _chatMetricsHistoryRepository.GetLatestMetricsForRoleAsync(
            sessionId,
            telegramUserId,
            interlocutorId,
            ChatRole.User,
            cancellationToken);
            
        var latestInterlocutorMetrics = await _chatMetricsHistoryRepository.GetLatestMetricsForRoleAsync(
            sessionId,
            telegramUserId,
            interlocutorId,
            ChatRole.Interlocutor,
            cancellationToken);
        
        // Создаем и возвращаем результат
        return new ChatMetricsData
        {
            MyMetrics = new MessageMetricsData
            {
                MessageCount = userMessageCount,
                ComplimentCount = latestUserMetrics?.TotalCompliments ?? 0,
                EngagementPercentage = latestUserMetrics?.EngagementScore ?? 0,
                AverageResponseTimeSeconds = userAverageResponseTime,
                AttachmentType = latestUserMetrics?.AttachmentType?.ToString() ?? string.Empty,
                AttachmentConfidence = latestUserMetrics?.AttachmentConfidence * 100 ?? 0 // Конвертируем в проценты (0-100)
            },
            InterlocutorMetrics = new MessageMetricsData
            {
                MessageCount = interlocutorMessageCount,
                ComplimentCount = latestInterlocutorMetrics?.TotalCompliments ?? 0,
                EngagementPercentage = latestInterlocutorMetrics?.EngagementScore ?? 0,
                AverageResponseTimeSeconds = interlocutorAverageResponseTime,
                AttachmentType = latestInterlocutorMetrics?.AttachmentType?.ToString() ?? string.Empty,
                AttachmentConfidence = latestInterlocutorMetrics?.AttachmentConfidence * 100 ?? 0 // Конвертируем в проценты (0-100)
            }
        };
    }
} 