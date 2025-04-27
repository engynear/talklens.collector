using Microsoft.AspNetCore.Mvc;
using TalkLens.Collector.Api.Models.Session;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models;

namespace TalkLens.Collector.Api.Controllers;

/// <summary>
/// Контроллер для работы со всеми типами сессий
/// </summary>
[Route("sessions")]
public class SessionController(IEnumerable<ISessionService> sessionServices) : BaseApiController
{
    /// <summary>
    /// Получает список всех активных сессий пользователя из всех сервисов
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SessionResponse>>> GetAllActiveSessionsAsync(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var allSessions = new List<SessionData>();
        
        // Получаем сессии из всех зарегистрированных сервисов
        foreach (var service in sessionServices)
        {
            try
            {
                var sessions = await service.GetActiveSessionsAsync(userId, cancellationToken);
                allSessions.AddRange(sessions);
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но продолжаем работу с другими сервисами
                Console.WriteLine($"Ошибка при получении сессий из сервиса {service.GetType().Name}: {ex.Message}");
            }
        }
        
        // Преобразуем в ответ API
        var response = allSessions.Select(s => new SessionResponse
        {
            Id = s.Id,
            SessionId = s.SessionId,
            SessionType = s.SessionType,
            CreatedAt = s.CreatedAt,
            LastActivityAt = s.LastActivityAt
        }).ToList();
        
        return Ok(response);
    }
} 