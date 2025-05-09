using Microsoft.AspNetCore.Mvc;
using TalkLens.Collector.Api.Models.Telegram;
using TalkLens.Collector.Domain.Interfaces;

namespace TalkLens.Collector.Api.Controllers;

[Route("metrics/telegram")]
[ApiController]
public class ChatMetricsController(
    IChatMetricsService chatMetricsService,
    ITelegramUserRecommendationRepository recommendationRepository) : BaseApiController
{
    /// <summary>
    /// Получает метрики чата с конкретным собеседником в рамках определенной сессии
    /// </summary>
    /// <param name="sessionId">Идентификатор сессии Telegram</param>
    /// <param name="interlocutorId">Идентификатор собеседника в Telegram</param>
    /// <param name="cancellationToken"></param>
    [HttpGet("chat/{sessionId}/{interlocutorId}")]
    public async Task<ActionResult<ChatMetricsResponse>> GetChatMetrics(
        [FromRoute] string sessionId,
        [FromRoute] long interlocutorId,
        CancellationToken cancellationToken)
    {
        // Предполагаем, что userId берется из контекста аутентификации
        var userId = GetUserId();
        
        // Получаем метрики из сервиса
        var metrics = await chatMetricsService.GetChatMetricsAsync(
            userId, 
            sessionId, 
            interlocutorId, 
            cancellationToken);
        
        // Преобразуем доменную модель в модель ответа API
        var response = new ChatMetricsResponse
        {
            MyMetrics = new MessageMetrics
            {
                MessageCount = metrics.MyMetrics.MessageCount,
                ComplimentCount = metrics.MyMetrics.ComplimentCount,
                EngagementPercentage = metrics.MyMetrics.EngagementPercentage,
                AverageResponseTimeSeconds = metrics.MyMetrics.AverageResponseTimeSeconds,
                AttachmentDefinition = new PersonalityDefinition
                {
                    Type = metrics.MyMetrics.AttachmentType,
                    Confidence = metrics.MyMetrics.AttachmentConfidence
                }
            },
            InterlocutorMetrics = new MessageMetrics
            {
                MessageCount = metrics.InterlocutorMetrics.MessageCount,
                ComplimentCount = metrics.InterlocutorMetrics.ComplimentCount,
                EngagementPercentage = metrics.InterlocutorMetrics.EngagementPercentage,
                AverageResponseTimeSeconds = metrics.InterlocutorMetrics.AverageResponseTimeSeconds,
                AttachmentDefinition = new PersonalityDefinition
                {
                    Type = metrics.InterlocutorMetrics.AttachmentType,
                    Confidence = metrics.InterlocutorMetrics.AttachmentConfidence
                }
            }
        };

        return Ok(response);
    }
    
    /// <summary>
    /// Получает последнюю рекомендацию для пользователя в рамках определенной сессии и собеседника
    /// </summary>
    /// <param name="sessionId">Идентификатор сессии Telegram</param>
    /// <param name="interlocutorId">Идентификатор собеседника в Telegram</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Последняя рекомендация или 404, если рекомендаций нет</returns>
    [HttpGet("recommendations/{sessionId}/{interlocutorId}")]
    public async Task<ActionResult<TelegramUserRecommendationResponse>> GetRecommendation(
        [FromRoute] string sessionId,
        [FromRoute] long interlocutorId,
        CancellationToken cancellationToken)
    {
        var recommendation = await recommendationRepository.GetLastRecommendationAsync(
            sessionId,
            interlocutorId,
            cancellationToken);
            
        if (recommendation == null)
        {
            return NotFound(new { message = "Рекомендаций не найдено" });
        }
        
        var response = new TelegramUserRecommendationResponse
        {
            Id = recommendation.Id,
            RecommendationText = recommendation.RecommendationText,
            CreatedAt = recommendation.CreatedAt
        };
        
        return Ok(response);
    }
} 