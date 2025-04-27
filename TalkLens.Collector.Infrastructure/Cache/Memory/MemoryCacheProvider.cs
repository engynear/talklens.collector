using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading.Tasks;

namespace TalkLens.Collector.Infrastructure.Cache.Memory;

/// <summary>
/// Реализация кэш-провайдера, использующая IMemoryCache
/// </summary>
public class MemoryCacheProvider<T> : ICacheProvider<T>
{
    private readonly IMemoryCache _cache;
    
    public MemoryCacheProvider(IMemoryCache cache)
    {
        _cache = cache;
    }
    
    /// <inheritdoc />
    public Task<T> GetOrCreateAsync(string key, Func<Task<T>> factory, TimeSpan expirationTime)
    {
        return _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.SetAbsoluteExpiration(expirationTime);
            return await factory();
        });
    }
    
    /// <inheritdoc />
    public Task<T?> GetAsync(string key)
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }
    
    /// <inheritdoc />
    public Task SetAsync(string key, T value, TimeSpan expirationTime)
    {
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(expirationTime);
            
        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }
    
    /// <inheritdoc />
    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
    
    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }
} 