using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Domain.Interfaces;

/// <summary>
/// Интерфейс репозитория для работы с рекомендациями пользователей Telegram
/// </summary>
public interface ITelegramUserRecommendationRepository
{
    /// <summary>
    /// Получает рекомендации для пользователя и его собеседника
    /// </summary>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор собеседника</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список рекомендаций</returns>
    Task<IEnumerable<TelegramUserRecommendationData>> GetRecommendationsAsync(
        string sessionId,
        long interlocutorId,
        CancellationToken cancellationToken);
        
    /// <summary>
    /// Получает последнюю рекомендацию для пользователя и его собеседника
    /// </summary>
    /// <param name="sessionId">Идентификатор сессии</param>
    /// <param name="interlocutorId">Идентификатор собеседника</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Последняя рекомендация или null, если рекомендаций нет</returns>
    Task<TelegramUserRecommendationData?> GetLastRecommendationAsync(
        string sessionId,
        long interlocutorId,
        CancellationToken cancellationToken);
} 