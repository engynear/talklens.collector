using TalkLens.Collector.Domain.Enums.Telegram;
using TalkLens.Collector.Domain.Models;
using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Domain.Interfaces;

/// <summary>
/// Интерфейс для работы с сессиями Telegram
/// </summary>
public interface ITelegramSessionService : ISessionService
{
    Task<TelegramLoginResult> LoginWithPhoneAsync(
        string userId, 
        string sessionId, 
        string phone, 
        CancellationToken cancellationToken);
    
    Task<TelegramLoginResult> SubmitVerificationCodeAsync(
        string userId,
        string sessionId,
        string code,
        CancellationToken cancellationToken);
    
    Task<TelegramLoginResult> SubmitTwoFactorPasswordAsync(
        string userId,
        string sessionId,
        string password,
        CancellationToken cancellationToken);
    
    // Task<TelegramLoginResult> GetSessionStatusAsync(
    //     string userId,
    //     string sessionId, 
    //     CancellationToken cancellationToken);

    /// <summary>
    /// Получает список активных сессий Telegram пользователя
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список активных сессий Telegram</returns>
    new Task<List<TelegramSessionData>> GetActiveSessionsAsync(
        string userId,
        CancellationToken cancellationToken);
        
    /// <summary>
    /// Удаляет указанную сессию Telegram
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если сессия удалена успешно, иначе False</returns>
    Task<bool> DeleteSessionAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken);
        
    /// <summary>
    /// Удаляет все сессии Telegram указанного пользователя
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Количество удаленных сессий</returns>
    Task<int> DeleteAllSessionsAsync(
        string userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Получает список контактов для указанной сессии
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="forceRefresh">Принудительное обновление кэша</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список контактов</returns>
    Task<List<TelegramContactResponse>> GetContactsAsync(
        string userId, 
        string sessionId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
        
    /// <summary>
    /// Получает объект сессии Telegram для доступа к API
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Объект сессии или null, если сессия не найдена</returns>
    Task<object?> GetSessionAsync(string userId, string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Получает список последних 10 контактов с сортировкой по дате последнего сообщения
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="forceRefresh">Принудительное обновление кэша</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список последних контактов</returns>
    Task<List<TelegramContactResponse>> GetRecentContactsAsync(
        string userId, 
        string sessionId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет подписку на контакт Telegram
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор контакта Telegram</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Данные созданной подписки</returns>
    Task<TelegramSubscriptionData> AddSubscriptionAsync(
        string userId,
        string sessionId,
        long interlocutorId,
        CancellationToken cancellationToken);
        
    /// <summary>
    /// Удаляет подписку на контакт Telegram
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор контакта Telegram</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если подписка удалена, иначе False</returns>
    Task<bool> RemoveSubscriptionAsync(
        string userId,
        string sessionId,
        long interlocutorId,
        CancellationToken cancellationToken);
        
    /// <summary>
    /// Получает список всех подписок для указанной сессии Telegram
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список подписок</returns>
    Task<List<TelegramSubscriptionData>> GetSubscriptionsAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken);
        
    /// <summary>
    /// Проверяет наличие подписки на контакт Telegram
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор контакта Telegram</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если подписка существует, иначе False</returns>
    Task<bool> HasSubscriptionAsync(
        string userId,
        string sessionId,
        long interlocutorId,
        CancellationToken cancellationToken);
}