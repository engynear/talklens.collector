using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Domain.Interfaces;

/// <summary>
/// Интерфейс репозитория для работы с подписками на контакты Telegram
/// </summary>
public interface ITelegramSubscriptionRepository
{
    /// <summary>
    /// Добавляет подписку на контакт Telegram
    /// </summary>
    /// <param name="subscription">Данные подписки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Данные созданной подписки</returns>
    Task<TelegramSubscriptionData> AddSubscriptionAsync(TelegramSubscriptionData subscription, CancellationToken cancellationToken);
    
    /// <summary>
    /// Удаляет подписку на контакт Telegram
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор контакта Telegram</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если подписка удалена, иначе False</returns>
    Task<bool> RemoveSubscriptionAsync(string userId, string sessionId, long interlocutorId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Проверяет наличие подписки на контакт Telegram
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор контакта Telegram</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если подписка существует, иначе False</returns>
    Task<bool> ExistsSubscriptionAsync(string userId, string sessionId, long interlocutorId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Получает список всех подписок для указанной сессии Telegram
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список подписок</returns>
    Task<List<TelegramSubscriptionData>> GetSessionSubscriptionsAsync(string userId, string sessionId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Проверяет наличие подписки на контакт Telegram по sessionId и interlocutorId (без userId)
    /// </summary>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор контакта Telegram</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если подписка существует для любого пользователя, иначе False</returns>
    Task<bool> ExistsAnySubscriptionAsync(string sessionId, long interlocutorId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Получает список всех подписок для указанной сессии Telegram (без привязки к userId)
    /// </summary>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список подписок</returns>
    Task<List<TelegramSubscriptionData>> GetAllSessionSubscriptionsAsync(string sessionId, CancellationToken cancellationToken);
} 