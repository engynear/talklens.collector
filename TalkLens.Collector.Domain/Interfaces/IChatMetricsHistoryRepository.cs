using TalkLens.Collector.Domain.Enums.Telegram;
using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Domain.Interfaces;

/// <summary>
/// Интерфейс репозитория для работы с историей метрик чата
/// </summary>
public interface IChatMetricsHistoryRepository
{
    /// <summary>
    /// Получает последние метрики для пользователя и собеседника
    /// </summary>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="telegramUserId">Идентификатор пользователя Telegram</param>
    /// <param name="interlocutorId">Идентификатор собеседника</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Коллекция последних метрик чата для пользователя и собеседника</returns>
    Task<IEnumerable<ChatMetricsHistoryData>> GetLatestMetricsAsync(
        string sessionId, 
        long telegramUserId, 
        long interlocutorId, 
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Получает последние метрики для указанной роли
    /// </summary>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="telegramUserId">Идентификатор пользователя Telegram</param>
    /// <param name="interlocutorId">Идентификатор собеседника</param>
    /// <param name="role">Роль (пользователь или собеседник)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Последние метрики чата для указанной роли</returns>
    Task<ChatMetricsHistoryData?> GetLatestMetricsForRoleAsync(
        string sessionId, 
        long telegramUserId, 
        long interlocutorId, 
        ChatRole role, 
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Сохраняет новую запись метрик чата
    /// </summary>
    /// <param name="metrics">Данные метрик чата</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сохраненные данные метрик</returns>
    Task<ChatMetricsHistoryData> SaveMetricsAsync(
        ChatMetricsHistoryData metrics, 
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Получает историю метрик чата за указанный период
    /// </summary>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="telegramUserId">Идентификатор пользователя Telegram</param>
    /// <param name="interlocutorId">Идентификатор собеседника</param>
    /// <param name="role">Роль (пользователь или собеседник)</param>
    /// <param name="fromDate">Начальная дата</param>
    /// <param name="toDate">Конечная дата</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>История метрик чата за указанный период</returns>
    Task<IEnumerable<ChatMetricsHistoryData>> GetMetricsHistoryAsync(
        string sessionId, 
        long telegramUserId, 
        long interlocutorId, 
        ChatRole? role, 
        DateTime fromDate, 
        DateTime toDate, 
        CancellationToken cancellationToken);
} 