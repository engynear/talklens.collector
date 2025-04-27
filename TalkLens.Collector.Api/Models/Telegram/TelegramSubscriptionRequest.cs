using System.ComponentModel.DataAnnotations;

namespace TalkLens.Collector.Api.Models.Telegram;

/// <summary>
/// Запрос на добавление/удаление подписки на контакт Telegram
/// </summary>
public class TelegramSubscriptionRequest
{
    /// <summary>
    /// Идентификатор сессии Telegram
    /// </summary>
    [Required]
    public string? SessionId { get; set; }
    
    /// <summary>
    /// Идентификатор контакта Telegram
    /// </summary>
    [Required]
    public long InterlocutorId { get; set; }
} 