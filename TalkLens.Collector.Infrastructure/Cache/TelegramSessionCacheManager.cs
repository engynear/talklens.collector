using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using TalkLens.Collector.Infrastructure.Configuration;
using TalkLens.Collector.Infrastructure.Messengers.Telegram;

namespace TalkLens.Collector.Infrastructure.Cache;

/// <summary>
/// Менеджер кэширования сессий Telegram
/// </summary>
public class TelegramSessionCacheManager
{
    private readonly ICacheProvider<TelegramSessionInfo> _cacheProvider;
    private readonly TimeSpan _defaultExpiry;
    private readonly ILogger<TelegramSessionCacheManager> _logger;
    
    // Локальный кэш для хранения ссылок на сессии
    private static readonly ConcurrentDictionary<string, TelegramSession> _localSessions = new();
    
    public TelegramSessionCacheManager(
        ICacheProvider<TelegramSessionInfo> cacheProvider,
        IOptions<RedisOptions> redisOptions,
        ILogger<TelegramSessionCacheManager> logger)
    {
        _cacheProvider = cacheProvider;
        _logger = logger;
        
        // Получаем время жизни сессии из опций
        _defaultExpiry = TimeSpan.FromHours(redisOptions.Value.SessionExpirationHours);
        
        _logger.LogInformation("TelegramSessionCacheManager инициализирован с временем жизни сессии {DefaultExpiry}", _defaultExpiry);
    }
    
    /// <summary>
    /// Получает сессию из кэша
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <returns>Объект сессии или null</returns>
    public async Task<TelegramSession?> GetSessionAsync(string userId, string sessionId)
    {
        var cacheKey = GetCacheKey(userId, sessionId);
        
        // Проверяем локальный кэш
        if (_localSessions.TryGetValue(cacheKey, out var localSession))
        {
            _logger.LogDebug("Сессия найдена в локальном кэше для {UserId}:{SessionId}", userId, sessionId);
            
            // Продлеваем срок жизни сессии в кэше
            await RefreshSessionExpiryAsync(userId, sessionId);
            
            return localSession;
        }
        
        // Пытаемся получить информацию о сессии из кэша
        var sessionInfo = await _cacheProvider.GetAsync(cacheKey);
        
        if (sessionInfo == null)
        {
            _logger.LogDebug("Сессия не найдена в кэше для {UserId}:{SessionId}", userId, sessionId);
            return null;
        }
        
        // Здесь нужна логика реконструкции сессии из информации в кэше
        // Это более сложная задача, которую нужно реализовать в интеграции с TelegramSessionManager
        _logger.LogWarning("Реконструкция сессии из кэша не реализована полностью");
        return null;
    }
    
    /// <summary>
    /// Сохраняет сессию в кэш
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <param name="session">Объект сессии</param>
    public async Task SetSessionAsync(string userId, string sessionId, TelegramSession session)
    {
        var cacheKey = GetCacheKey(userId, sessionId);
        
        // Создаем информацию о сессии для хранения в кэше
        var sessionInfo = new TelegramSessionInfo
        {
            UserId = userId,
            SessionId = sessionId,
            PhoneNumber = session.PhoneNumber,
            Status = session.Status.ToString(),
            SessionFilePath = session.GetSessionFilePath(),
            UpdatesFilePath = session.GetUpdatesFilePath(),
            CreatedAt = DateTime.UtcNow
        };
        
        // Сохраняем информацию в кэше
        await _cacheProvider.SetAsync(cacheKey, sessionInfo, _defaultExpiry);
        
        // Сохраняем ссылку на объект сессии в локальном кэше
        _localSessions[cacheKey] = session;
        
        _logger.LogDebug("Сессия сохранена в кэше для {UserId}:{SessionId}", userId, sessionId);
    }
    
    /// <summary>
    /// Удаляет сессию из кэша
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    public async Task RemoveSessionAsync(string userId, string sessionId)
    {
        var cacheKey = GetCacheKey(userId, sessionId);
        
        // Удаляем из кэша
        await _cacheProvider.RemoveAsync(cacheKey);
        
        // Удаляем из локального кэша и освобождаем ресурсы
        if (_localSessions.TryRemove(cacheKey, out var session))
        {
            try
            {
                session.Dispose();
                _logger.LogDebug("Сессия удалена из кэша и освобождена для {UserId}:{SessionId}", userId, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при освобождении сессии для {UserId}:{SessionId}: {Error}", 
                    userId, sessionId, ex.Message);
            }
        }
    }
    
    /// <summary>
    /// Продлевает срок жизни сессии в кэше
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    private async Task RefreshSessionExpiryAsync(string userId, string sessionId)
    {
        var cacheKey = GetCacheKey(userId, sessionId);
        
        // Получаем информацию о сессии
        var sessionInfo = await _cacheProvider.GetAsync(cacheKey);
        
        if (sessionInfo != null)
        {
            // Обновляем время и сохраняем обратно
            await _cacheProvider.SetAsync(cacheKey, sessionInfo, _defaultExpiry);
            _logger.LogDebug("Обновлено время жизни сессии в кэше для {UserId}:{SessionId}", userId, sessionId);
        }
    }
    
    /// <summary>
    /// Формирует ключ кэша для сессии
    /// </summary>
    private static string GetCacheKey(string userId, string sessionId) => $"session:{userId}:{sessionId}";
} 