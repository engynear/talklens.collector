using Microsoft.AspNetCore.Mvc;
using TalkLens.Collector.Api.Models.Telegram;

namespace TalkLens.Collector.Api.Controllers;

[Route("metrics/telegram")]
[ApiController]
public class ChatMetricsController : BaseApiController
{
    private readonly Random _random = new();

    private PersonalityDefinition GenerateRandomAttachmentDefinition()
    {
        var attachmentTypes = Enum.GetValues<AttachmentType>();
        var randomType = attachmentTypes[_random.Next(attachmentTypes.Length)];
        var confidence = 60 + _random.NextDouble() * 40; // От 60 до 100
        
        return new PersonalityDefinition
        {
            Type = randomType.ToString(),
            Confidence = confidence
        };
    }

    /// <summary>
    /// Получает метрики чата с конкретным собеседником в рамках определенной сессии
    /// </summary>
    /// <param name="sessionId">Идентификатор сессии Telegram</param>
    /// <param name="interlocutorId">Идентификатор собеседника в Telegram</param>
    [HttpGet("chat/{sessionId}/{interlocutorId}")]
    public ActionResult<ChatMetricsResponse> GetChatMetrics(
        [FromRoute] string sessionId,
        [FromRoute] long interlocutorId)
    {
        // Временная реализация с случайными значениями
        var response = new ChatMetricsResponse
        {
            MyMetrics = new MessageMetrics
            {
                MessageCount = _random.Next(10, 1000),
                ComplimentCount = _random.Next(0, 50),
                EngagementPercentage = _random.NextDouble() * 100,
                AverageResponseTimeSeconds = _random.NextDouble() * 300,
                AttachmentDefinition = GenerateRandomAttachmentDefinition()
            },
            InterlocutorMetrics = new MessageMetrics
            {
                MessageCount = _random.Next(10, 1000),
                ComplimentCount = _random.Next(0, 50),
                EngagementPercentage = _random.NextDouble() * 100,
                AverageResponseTimeSeconds = _random.NextDouble() * 300,
                AttachmentDefinition = GenerateRandomAttachmentDefinition()
            }
        };

        return Ok(response);
    }
} 