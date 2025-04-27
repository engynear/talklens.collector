using TalkLens.Collector.Domain.Models;

namespace TalkLens.Collector.Domain.Interfaces;

/// <summary>
/// Базовый интерфейс для всех сервисов, работающих с сессиями
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Получает список активных сессий пользователя
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список активных сессий</returns>
    Task<List<SessionData>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken);
}