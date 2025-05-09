using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Domain.Interfaces;

/// <summary>
/// Интерфейс сервиса для работы с метриками чата
/// </summary>
public interface IChatMetricsService
{
    /// <summary>
    /// Получает метрики чата для указанной сессии и собеседника
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор собеседника</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Метрики чата</returns>
    Task<ChatMetricsData> GetChatMetricsAsync(
        string userId, 
        string sessionId, 
        long interlocutorId, 
        CancellationToken cancellationToken);
} 