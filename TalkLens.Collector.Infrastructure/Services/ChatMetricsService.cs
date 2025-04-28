using System.Threading;
using System.Threading.Tasks;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Infrastructure.Services;

/// <summary>
/// Сервис для работы с метриками чата
/// </summary>
public class ChatMetricsService : IChatMetricsService
{
    private readonly ITelegramMessageRepository _telegramMessageRepository;
    private readonly Random _random = new();

    public ChatMetricsService(ITelegramMessageRepository telegramMessageRepository)
    {
        _telegramMessageRepository = telegramMessageRepository;
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
        
        // Создаем и возвращаем результат
        return new ChatMetricsData
        {
            MyMetrics = new MessageMetricsData
            {
                MessageCount = userMessageCount,
                ComplimentCount = _random.Next(0, 50), // Пока оставляем случайным
                EngagementPercentage = _random.NextDouble() * 100, // Пока оставляем случайным
                AverageResponseTimeSeconds = userAverageResponseTime,
                AttachmentType = GetRandomAttachmentType(), 
                AttachmentConfidence = 60 + _random.NextDouble() * 40 // От 60 до 100
            },
            InterlocutorMetrics = new MessageMetricsData
            {
                MessageCount = interlocutorMessageCount,
                ComplimentCount = _random.Next(0, 50), // Пока оставляем случайным
                EngagementPercentage = _random.NextDouble() * 100, // Пока оставляем случайным
                AverageResponseTimeSeconds = interlocutorAverageResponseTime,
                AttachmentType = GetRandomAttachmentType(),
                AttachmentConfidence = 60 + _random.NextDouble() * 40 // От 60 до 100
            }
        };
    }
    
    /// <summary>
    /// Возвращает случайный тип привязанности
    /// </summary>
    private string GetRandomAttachmentType()
    {
        var types = new[] { "Secure", "Anxious", "Avoidant" };
        return types[_random.Next(types.Length)];
    }
} 