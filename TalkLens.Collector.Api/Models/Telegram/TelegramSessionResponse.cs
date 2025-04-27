using System;
using TalkLens.Collector.Api.Models.Session;

namespace TalkLens.Collector.Api.Models.Telegram;

/// <summary>
/// Модель ответа для сессий Telegram
/// </summary>
public sealed record TelegramSessionResponse : SessionResponse
{
    /// <summary>
    /// Номер телефона, связанный с сессией Telegram
    /// </summary>
    public string PhoneNumber { get; set; }
    
    /// <summary>
    /// Идентификатор пользователя в Telegram
    /// </summary>
    public long? TelegramUserId { get; set; }
    
    public TelegramSessionResponse()
    {
        SessionType = "Telegram";
    }
} 