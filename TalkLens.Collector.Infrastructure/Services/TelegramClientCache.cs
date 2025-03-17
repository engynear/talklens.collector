using Microsoft.Extensions.Caching.Memory;
using TalkLens.Collector.Infrastructure.Messengers.Telegram;

namespace TalkLens.Collector.Infrastructure.Services;

public static class TelegramClientCache
{
    private static readonly MemoryCache _globalCache = new(new MemoryCacheOptions());
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromHours(1);
    
    public static TelegramSession? GetSession(string userId, string sessionId)
    {
        var key = GetCacheKey(userId, sessionId);
        _globalCache.TryGetValue(key, out TelegramSession? session);
        return session;
    }
    
    public static void SetSession(string userId, string sessionId, TelegramSession session)
    {
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
            
        _globalCache.Set(key, session, options);
    }
    
    public static void RemoveSession(string userId, string sessionId)
    {
        var key = GetCacheKey(userId, sessionId);
        if (_globalCache.TryGetValue(key, out TelegramSession? session))
        {
            session?.Dispose();
            _globalCache.Remove(key);
        }
    }
    
    private static string GetCacheKey(string userId, string sessionId) => $"global_{userId}_{sessionId}";
} 