using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TalkLens.Collector.Domain.Interfaces;

/// <summary>
/// Интерфейс для хранилища сессий Telegram
/// </summary>
public interface ITelegramSessionStorage
{
    /// <summary>
    /// Проверяет существование файла сессии Telegram
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <returns>True если файл существует, иначе False</returns>
    Task<bool> SessionExistsAsync(string userId, string sessionId);
    
    /// <summary>
    /// Получает путь к локальному файлу сессии для использования с WTelegramClient
    /// При необходимости скачивает файл из хранилища в локальный кэш
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <returns>Абсолютный путь к файлу сессии</returns>
    Task<string> GetSessionFilePathAsync(string userId, string sessionId);
    
    /// <summary>
    /// Сохраняет файл сессии в хранилище
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <param name="localFilePath">Путь к локальному файлу сессии для сохранения</param>
    Task SaveSessionAsync(string userId, string sessionId, string localFilePath);
    
    /// <summary>
    /// Удаляет файл сессии из хранилища
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    Task DeleteSessionAsync(string userId, string sessionId);
    
    /// <summary>
    /// Проверяет существование файла состояния обновлений
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <returns>True если файл существует, иначе False</returns>
    Task<bool> UpdatesStateExistsAsync(string userId, string sessionId);
    
    /// <summary>
    /// Получает путь к локальному файлу состояния обновлений для использования с WTelegramClient
    /// При необходимости скачивает файл из хранилища в локальный кэш
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <returns>Абсолютный путь к файлу состояния обновлений</returns>
    Task<string> GetUpdatesStateFilePathAsync(string userId, string sessionId);
    
    /// <summary>
    /// Сохраняет файл состояния обновлений в хранилище
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <param name="localFilePath">Путь к локальному файлу состояния обновлений для сохранения</param>
    Task SaveUpdatesStateAsync(string userId, string sessionId, string localFilePath);
    
    /// <summary>
    /// Удаляет файл состояния обновлений из хранилища
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    Task DeleteUpdatesStateAsync(string userId, string sessionId);
    
    /// <summary>
    /// Очищает локальные кэшированные файлы сессии
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    Task CleanupLocalCacheAsync(string userId, string sessionId);
} 