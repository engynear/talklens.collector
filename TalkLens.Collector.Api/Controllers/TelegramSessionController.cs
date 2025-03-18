using Microsoft.AspNetCore.Mvc;
using TalkLens.Collector.Api.Models.Telegram;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Api.Controllers;

[Route("auth/telegram")]
[ApiController]
public class TelegramSessionController : BaseApiController
{
    private readonly ITelegramSessionService _telegramSessionService;

    public TelegramSessionController(ITelegramSessionService telegramSessionService)
    {
        _telegramSessionService = telegramSessionService;
    }

    /// <summary>
    /// Начинает процесс авторизации в Telegram с помощью номера телефона
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<SessionResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _telegramSessionService.LoginWithPhoneAsync(
            userId,
            request.SessionId,
            request.Phone,
            cancellationToken);

        return Ok(new SessionResponse
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
    public async Task<ActionResult<SessionResponse>> SubmitVerificationCodeAsync(
        [FromBody] VerificationCodeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _telegramSessionService.SubmitVerificationCodeAsync(
            userId,
            request.SessionId,
            request.Code,
            cancellationToken);

        return Ok(new SessionResponse
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
    public async Task<ActionResult<SessionResponse>> SubmitTwoFactorPasswordAsync(
        [FromBody] TwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _telegramSessionService.SubmitTwoFactorPasswordAsync(
            userId,
            request.SessionId,
            request.Password,
            cancellationToken);

        return Ok(new SessionResponse
        {
            SessionId = request.SessionId,
            Status = result.Status.ToString(),
            PhoneNumber = result.PhoneNumber,
            Error = result.Error
        });
    }

    /// <summary>
    /// Получает текущий статус сессии
    /// </summary>
    [HttpGet("status/{sessionId}")]
    public async Task<ActionResult<SessionResponse>> GetSessionStatusAsync(
        [FromRoute] string sessionId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _telegramSessionService.GetSessionStatusAsync(
            userId,
            sessionId,
            cancellationToken);

        return Ok(new SessionResponse
        {
            SessionId = sessionId,
            Status = result.Status.ToString(),
            PhoneNumber = result.PhoneNumber,
            Error = result.Error
        });
    }

    /// <summary>
    /// Получает список активных сессий пользователя
    /// </summary>
    [HttpGet("sessions")]
    public async Task<ActionResult<List<SessionResponse>>> GetActiveSessionsAsync(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var sessions = await _telegramSessionService.GetActiveSessionsAsync(userId, cancellationToken);
        
        var response = sessions.Select(s => new SessionResponse
        {
            SessionId = s.SessionId,
            Status = "Success",
            PhoneNumber = s.PhoneNumber,
            Error = null
        }).ToList();
        
        return Ok(response);
    }

    /// <summary>
    /// Подписывается на обновления сообщений для указанной сессии и собеседника
    /// </summary>
    [HttpPost("sessions/{sessionId}/interlocutors/{interlocutorId}/subscribe")]
    public async Task<ActionResult> SubscribeToUpdatesAsync(
        [FromRoute] string sessionId,
        [FromRoute] long interlocutorId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await _telegramSessionService.SubscribeToUpdatesAsync(userId, sessionId, interlocutorId, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Отписывается от обновлений сообщений для указанной сессии и собеседника
    /// </summary>
    [HttpPost("sessions/{sessionId}/interlocutors/{interlocutorId}/unsubscribe")]
    public async Task<ActionResult> UnsubscribeFromUpdatesAsync(
        [FromRoute] string sessionId,
        [FromRoute] long interlocutorId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await _telegramSessionService.UnsubscribeFromUpdatesAsync(userId, sessionId, interlocutorId, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Получает список контактов, на которые оформлена подписка на обновления
    /// </summary>
    [HttpGet("sessions/{sessionId}/subscribed")]
    public async Task<ActionResult<List<TelegramContactResponse>>> GetSubscribedContactsAsync(
        [FromRoute] string sessionId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var contacts = await _telegramSessionService.GetSubscribedContactsAsync(userId, sessionId, cancellationToken);
        return Ok(contacts);
    }
} 