using Microsoft.Extensions.Caching.Memory;
using TalkLens.Collector.Infrastructure.Messengers.Telegram;
using TalkLens.Collector.Infrastructure.Services.Telegram;

namespace TalkLens.Collector.Infrastructure.Services;

/// <summary>
/// Статический класс для кэширования сессий Telegram
/// Поддерживает обе реализации - MemoryCache (для обратной совместимости) и Redis
/// </summary>
public static class TelegramClientCache
{
    private static readonly MemoryCache _localCache = new(new MemoryCacheOptions());
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromHours(1);
    
    // Провайдер Redis для кэширования (внедряется через инициализацию)
    private static RedisTelegramSessionCache? _redisCache;
    
    // Флаг, указывающий, использовать ли Redis
    private static bool _useRedis = false;
    
    /// <summary>
    /// Инициализирует Redis кэш
    /// </summary>
    public static void InitializeRedisCache(RedisTelegramSessionCache redisCache)
    {
        _redisCache = redisCache;
        _useRedis = true;
    }
    
    /// <summary>
    /// Получает сессию из кэша
    /// </summary>
    public static TelegramSession? GetSession(string userId, string sessionId)
    {
        // Если настроен Redis, используем его
        if (_useRedis && _redisCache != null)
        {
            return _redisCache.GetSession(userId, sessionId);
        }
        
        // Иначе используем локальный кэш (для обратной совместимости)
        var key = GetCacheKey(userId, sessionId);
        _localCache.TryGetValue(key, out TelegramSession? session);
        return session;
    }
    
    /// <summary>
    /// Сохраняет сессию в кэш
    /// </summary>
    public static void SetSession(string userId, string sessionId, TelegramSession session)
    {
        // Если настроен Redis, используем его
        if (_useRedis && _redisCache != null)
        {
            _redisCache.SetSession(userId, sessionId, session);
            return;
        }
        
        // Иначе используем локальный кэш (для обратной совместимости)
        var key = GetCacheKey(userId, sessionId);
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(_defaultTimeout)
            .RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                if (value is TelegramSession telegramSession)
                {
                    telegramSession.Dispose();
                }
            });
            
        _localCache.Set(key, session, options);
    }
    
    /// <summary>
    /// Удаляет сессию из кэша
    /// </summary>
    public static void RemoveSession(string userId, string sessionId)
    {
        // Если настроен Redis, используем его
        if (_useRedis && _redisCache != null)
        {
            _redisCache.RemoveSession(userId, sessionId);
            return;
        }
        
        // Иначе используем локальный кэш (для обратной совместимости)
        var key = GetCacheKey(userId, sessionId);
        if (_localCache.TryGetValue(key, out TelegramSession? session))
        {
            session?.Dispose();
            _localCache.Remove(key);
        }
    }
    
    private static string GetCacheKey(string userId, string sessionId) => $"global_{userId}_{sessionId}";
} 