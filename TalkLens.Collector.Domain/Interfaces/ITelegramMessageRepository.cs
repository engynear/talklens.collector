using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Domain.Interfaces;

/// <summary>
/// Интерфейс репозитория для работы с сообщениями Telegram
/// </summary>
public interface ITelegramMessageRepository
{
    /// <summary>
    /// Сохраняет метаданные о сообщении в базу данных
    /// </summary>
    /// <param name="messageData">Данные о сообщении</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сохраненные данные о сообщении</returns>
    Task<TelegramMessageData> SaveMessageAsync(TelegramMessageData messageData, CancellationToken cancellationToken);
    
    /// <summary>
    /// Сохраняет несколько сообщений за один вызов
    /// </summary>
    /// <param name="messages">Список сообщений для сохранения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Количество сохраненных сообщений</returns>
    Task<int> SaveMessagesAsync(IEnumerable<TelegramMessageData> messages, CancellationToken cancellationToken);
    
    /// <summary>
    /// Получает список сообщений для указанной сессии и собеседника
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор собеседника</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список сообщений</returns>
    Task<List<TelegramMessageData>> GetMessagesAsync(
        string userId, 
        string sessionId, 
        long interlocutorId, 
        CancellationToken cancellationToken);
        
    /// <summary>
    /// Получает количество сообщений пользователя для указанной сессии и собеседника
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор собеседника</param>
    /// <param name="telegramUserId">Идентификатор пользователя Telegram</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Количество сообщений пользователя</returns>
    Task<int> GetUserMessageCountAsync(
        string userId,
        string sessionId,
        long interlocutorId,
        long telegramUserId,
        CancellationToken cancellationToken);
        
    /// <summary>
    /// Получает количество сообщений собеседника для указанной сессии и собеседника
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор собеседника</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Количество сообщений собеседника</returns>
    Task<int> GetInterlocutorMessageCountAsync(
        string userId,
        string sessionId,
        long interlocutorId,
        CancellationToken cancellationToken);
        
    /// <summary>
    /// Получает среднее время ответа пользователя для указанной сессии и собеседника
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор собеседника</param>
    /// <param name="telegramUserId">Идентификатор пользователя Telegram</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Среднее время ответа пользователя в секундах</returns>
    Task<double> GetUserAverageResponseTimeAsync(
        string userId,
        string sessionId,
        long interlocutorId,
        long telegramUserId,
        CancellationToken cancellationToken);
        
    /// <summary>
    /// Получает среднее время ответа собеседника для указанной сессии и собеседника
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор собеседника</param>
    /// <param name="telegramUserId">Идентификатор пользователя Telegram</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Среднее время ответа собеседника в секундах</returns>
    Task<double> GetInterlocutorAverageResponseTimeAsync(
        string userId,
        string sessionId,
        long interlocutorId,
        long telegramUserId,
        CancellationToken cancellationToken);
} 