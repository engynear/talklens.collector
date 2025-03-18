using System.Collections.Concurrent;

namespace TalkLens.Collector.Infrastructure.Services;

public class TelegramSubscriptionManager
{
    // Ключ: "{userId}_{sessionId}_{interlocutorId}"
    private readonly ConcurrentDictionary<string, bool> _activeSubscriptions = new();

    private static string GetSubscriptionKey(string userId, string sessionId, long interlocutorId) 
        => $"{userId}_{sessionId}_{interlocutorId}";

    public bool TryAddSubscription(string userId, string sessionId, long interlocutorId)
    {
        var key = GetSubscriptionKey(userId, sessionId, interlocutorId);
        return _activeSubscriptions.TryAdd(key, true);
    }

    public void RemoveSubscription(string userId, string sessionId, long interlocutorId)
    {
        var key = GetSubscriptionKey(userId, sessionId, interlocutorId);
        _activeSubscriptions.TryRemove(key, out _);
    }

    public bool HasSubscription(string userId, string sessionId, long interlocutorId)
    {
        var key = GetSubscriptionKey(userId, sessionId, interlocutorId);
        return _activeSubscriptions.ContainsKey(key);
    }

    public bool HasAnySubscriptions(string userId, string sessionId)
    {
        var prefix = $"{userId}_{sessionId}_";
        return _activeSubscriptions.Keys.Any(key => key.StartsWith(prefix));
    }

    public IEnumerable<long> GetSubscribedInterlocutors(string userId, string sessionId)
    {
        var prefix = $"{userId}_{sessionId}_";
        return _activeSubscriptions.Keys
            .Where(key => key.StartsWith(prefix))
            .Select(key => long.Parse(key.Split('_').Last()));
    }
} 