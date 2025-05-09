using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Domain.Interfaces;

public interface ITelegramSessionRepository
{
    Task<TelegramSessionData?> GetActiveSessionAsync(string userId, string sessionId, CancellationToken cancellationToken);
    
    Task<TelegramSessionData> SaveSessionAsync(TelegramSessionData session, CancellationToken cancellationToken);
    
    Task UpdateSessionStatusAsync(string userId, string sessionId, bool isActive, CancellationToken cancellationToken);
    
    Task<List<TelegramSessionData>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Получает список всех активных сессий для всех пользователей
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список активных сессий</returns>
    Task<List<TelegramSessionData>> GetAllActiveSessionsAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Проверяет существование активной сессии с указанным номером телефона
    /// </summary>
    /// <param name="phoneNumber">Номер телефона</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если существует активная сессия, иначе False</returns>
    Task<bool> ExistsActiveSessionWithPhoneAsync(string phoneNumber, CancellationToken cancellationToken);
    
    /// <summary>
    /// Удаляет сессию по ID пользователя и ID сессии
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>True если сессия удалена, иначе False</returns>
    Task<bool> DeleteSessionAsync(string userId, string sessionId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Удаляет все сессии указанного пользователя
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Количество удаленных сессий</returns>
    Task<int> DeleteAllUserSessionsAsync(string userId, CancellationToken cancellationToken);
} 