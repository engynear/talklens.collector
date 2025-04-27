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
} 