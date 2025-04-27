using Microsoft.AspNetCore.Mvc;
using TalkLens.Collector.Api.Models.Session;
using TalkLens.Collector.Api.Models.Telegram;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Messengers.Telegram;

namespace TalkLens.Collector.Api.Controllers;

/// <summary>
/// Контроллер для работы с сессиями Telegram
/// </summary>
[Route("sessions/telegram")]
public class TelegramSessionController : BaseApiController
{
    private readonly ITelegramSessionService _telegramSessionService;

    public TelegramSessionController(ITelegramSessionService telegramSessionService)
    {
        _telegramSessionService = telegramSessionService;
    }

    /// <summary>
    /// Получает список активных сессий Telegram пользователя
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TelegramSessionResponse>>> GetTelegramSessionsAsync(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var sessions = await _telegramSessionService.GetActiveSessionsAsync(userId, cancellationToken);
        
        var response = sessions.Select(s => new TelegramSessionResponse
        {
            Id = s.Id,
            SessionId = s.SessionId,
            PhoneNumber = s.PhoneNumber,
            CreatedAt = s.CreatedAt,
            LastActivityAt = s.LastActivityAt
        }).ToList();
        
        return Ok(response);
    }

    /// <summary>
    /// Начинает процесс авторизации в Telegram с помощью номера телефона
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<TelegramSessionStatusResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _telegramSessionService.LoginWithPhoneAsync(
            userId,
            request.SessionId,
            request.Phone,
            cancellationToken);

        return Ok(new TelegramSessionStatusResponse
        {
            SessionId = request.SessionId,
            Status = result.Status.ToString(),
            PhoneNumber = result.PhoneNumber,
            Error = result.Error
        });
    }

    /// <summary>
    /// Отправляет код верификации для подтверждения номера телефона
    /// </summary>
    [HttpPost("verification-code")]
    public async Task<ActionResult<TelegramSessionStatusResponse>> SubmitVerificationCodeAsync(
        [FromBody] VerificationCodeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _telegramSessionService.SubmitVerificationCodeAsync(
            userId,
            request.SessionId,
            request.Code,
            cancellationToken);

        return Ok(new TelegramSessionStatusResponse
        {
            SessionId = request.SessionId,
            Status = result.Status.ToString(),
            PhoneNumber = result.PhoneNumber,
            Error = result.Error
        });
    }

    /// <summary>
    /// Отправляет пароль двухфакторной аутентификации
    /// </summary>
    [HttpPost("two-factor")]
    public async Task<ActionResult<TelegramSessionStatusResponse>> SubmitTwoFactorPasswordAsync(
        [FromBody] TwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _telegramSessionService.SubmitTwoFactorPasswordAsync(
            userId,
            request.SessionId,
            request.Password,
            cancellationToken);

        return Ok(new TelegramSessionStatusResponse
        {
            SessionId = request.SessionId,
            Status = result.Status.ToString(),
            PhoneNumber = result.PhoneNumber,
            Error = result.Error
        });
    }

    /// <summary>
    /// Удаляет указанную сессию Telegram
    /// </summary>
    [HttpDelete("{sessionId}")]
    public async Task<ActionResult> DeleteSessionAsync(
        [FromRoute] string sessionId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _telegramSessionService.DeleteSessionAsync(
            userId,
            sessionId,
            cancellationToken);
            
        if (result)
        {
            return Ok(new { message = "Сессия успешно удалена" });
        }
        
        return NotFound(new { error = "Сессия не найдена или не может быть удалена" });
    }
    
    /// <summary>
    /// Удаляет все сессии Telegram текущего пользователя
    /// </summary>
    [HttpDelete("all")]
    public async Task<ActionResult> DeleteAllSessionsAsync(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var deletedCount = await _telegramSessionService.DeleteAllSessionsAsync(
            userId,
            cancellationToken);
            
        return Ok(new { 
            message = "Сессии успешно удалены", 
            count = deletedCount 
        });
    }

    /// <summary>
    /// Получает список контактов для указанной сессии
    /// </summary>
    [HttpGet("{sessionId}/contacts")]
    public async Task<ActionResult<List<TelegramContactResponse>>> GetContactsAsync(
        [FromRoute] string sessionId,
        [FromQuery] bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        try
        {
            var contacts = await _telegramSessionService.GetContactsAsync(
                userId, 
                sessionId,
                cancellationToken);
                
            return Ok(contacts);
        }
        catch (ObjectDisposedException ex)
        {
            return StatusCode(500, $"Ошибка доступа к сессии: Сессия была закрыта. Попробуйте войти заново.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Ошибка при получении контактов: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Получает последние 10 контактов из Telegram
    /// </summary>
    [HttpGet("{sessionId}/contacts/recent")]
    public async Task<ActionResult<List<TelegramContactResponse>>> GetRecentContactsAsync(
        [FromRoute] string sessionId,
        [FromQuery] bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        
        try
        {
            var contacts = await _telegramSessionService.GetRecentContactsAsync(
                userId, 
                sessionId, 
                forceRefresh, 
                cancellationToken);
                
            return Ok(contacts);
        }
        catch (ObjectDisposedException ex)
        {
            return StatusCode(500, $"Ошибка доступа к сессии: Сессия была закрыта. Попробуйте войти заново.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Ошибка при получении контактов: {ex.Message}");
        }
    }

    /// <summary>
    /// Добавляет подписку на контакт Telegram
    /// </summary>
    /// <remarks>
    /// Данная подписка позволяет получать сообщения только от выбранных контактов
    /// </remarks>
    [HttpPost("subscriptions")]
    public async Task<ActionResult<TelegramSubscriptionResponse>> AddSubscriptionAsync(
        [FromBody] TelegramSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId == null)
        {
            return BadRequest(new { error = "Не указан идентификатор сессии" });
        }
        
        var userId = GetUserId();
        
        try
        {
            var subscription = await _telegramSessionService.AddSubscriptionAsync(
                userId,
                request.SessionId,
                request.InterlocutorId,
                cancellationToken);
                
            return Ok(new TelegramSubscriptionResponse
            {
                Id = subscription.Id,
                SessionId = subscription.SessionId,
                InterlocutorId = subscription.TelegramInterlocutorId,
                CreatedAt = subscription.CreatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при добавлении подписки: {ex.Message}" });
        }
    }
    
    /// <summary>
    /// Удаляет подписку на контакт Telegram
    /// </summary>
    [HttpDelete("subscriptions")]
    public async Task<ActionResult> RemoveSubscriptionAsync(
        [FromBody] TelegramSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SessionId == null)
        {
            return BadRequest(new { error = "Не указан идентификатор сессии" });
        }
        
        var userId = GetUserId();
        
        try
        {
            var result = await _telegramSessionService.RemoveSubscriptionAsync(
                userId,
                request.SessionId,
                request.InterlocutorId,
                cancellationToken);
                
            if (result)
            {
                return Ok(new { message = "Подписка успешно удалена" });
            }
            
            return NotFound(new { error = "Подписка не найдена" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при удалении подписки: {ex.Message}" });
        }
    }
    
    /// <summary>
    /// Получает список всех подписок для указанной сессии Telegram
    /// </summary>
    [HttpGet("{sessionId}/subscriptions")]
    public async Task<ActionResult<List<TelegramSubscriptionResponse>>> GetSubscriptionsAsync(
        [FromRoute] string sessionId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        
        try
        {
            var subscriptions = await _telegramSessionService.GetSubscriptionsAsync(
                userId,
                sessionId,
                cancellationToken);
                
            var response = subscriptions.Select(s => new TelegramSubscriptionResponse
            {
                Id = s.Id,
                SessionId = s.SessionId,
                InterlocutorId = s.TelegramInterlocutorId,
                CreatedAt = s.CreatedAt
            }).ToList();
            
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при получении списка подписок: {ex.Message}" });
        }
    }
} 