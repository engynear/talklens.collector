using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using TalkLens.Collector.Infrastructure.Configuration;

namespace TalkLens.Collector.Infrastructure.Services.Telegram;

/// <summary>
/// Предоставляет функциональность кэширования для запросов к Telegram API
/// </summary>
public class TelegramApiCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<TelegramApiCache> _logger;
    private readonly ConcurrentDictionary<string, TimeSpan> _methodExpiration = new();
    private readonly TimeSpan _defaultExpiration;

    public TelegramApiCache(
        IMemoryCache cache, 
        IOptions<TelegramOptions> telegramOptions, 
        ILogger<TelegramApiCache> logger)
    {
        _cache = cache;
        _logger = logger;
        
        var cacheOptions = telegramOptions.Value.Cache;
        var defaultExpirationMinutes = cacheOptions.DefaultExpirationMinutes;
        _defaultExpiration = TimeSpan.FromMinutes(defaultExpirationMinutes);
        
        InitializeMethodExpirations(cacheOptions);
        
        _logger.LogInformation("TelegramApiCache инициализирован. Время жизни кэша по умолчанию: {Expiration} минут", defaultExpirationMinutes);
    }
    
    private void InitializeMethodExpirations(TelegramCacheOptions cacheOptions)
    {
        foreach (var methodEntry in cacheOptions.MethodExpirations)
        {
            var methodName = methodEntry.Key;
            var methodOptions = methodEntry.Value;
            var expirationMinutes = methodOptions.ExpirationMinutes;
            
            _methodExpiration[methodName] = TimeSpan.FromMinutes(expirationMinutes);
            _logger.LogDebug("Установлено специальное время жизни кэша для метода {Method}: {Expiration} минут", 
                methodName, expirationMinutes);
        }
    }
    
    /// <summary>
    /// Генерирует ключ кэша на основе метода и параметров
    /// </summary>
    /// <param name="methodName">Имя метода API</param>
    /// <param name="args">Аргументы метода</param>
    /// <returns>Ключ кэша</returns>
    private string GenerateCacheKey(string methodName, params object[] args)
    {
        var argsString = string.Join("_", args);
        return $"telegram_api:{methodName}:{argsString}";
    }
    
    /// <summary>
    /// Получает или создает элемент в кэше
    /// </summary>
    /// <typeparam name="T">Тип результата</typeparam>
    /// <param name="methodName">Имя метода API</param>
    /// <param name="factory">Фабрика для создания результата</param>
    /// <param name="forceRefresh">Принудительное обновление кэша</param>
    /// <param name="args">Аргументы метода для генерации ключа</param>
    /// <returns>Результат из кэша или вызова фабрики</returns>
    public async Task<T> GetOrCreateAsync<T>(string methodName, Func<Task<T>> factory, bool forceRefresh = false, params object[] args)
    {
        var cacheKey = GenerateCacheKey(methodName, args);
        
        if (forceRefresh)
        {
            _logger.LogDebug("Принудительное обновление для {MethodName}", methodName);
            Console.WriteLine($"[DEBUG] {methodName}: Force refresh requested, skipping cache");
            
            var freshResult = await factory();
            
            var expirationTime = _methodExpiration.TryGetValue(methodName, out var expTime) ? expTime : _defaultExpiration;
            var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(expirationTime);
            _cache.Set(cacheKey, freshResult, cacheOptions);
            
            return freshResult;
        }
        
        if (_cache.TryGetValue(cacheKey, out T cachedResult))
        {
            _logger.LogDebug("Кэш найден для {MethodName}", methodName);
            Console.WriteLine($"[DEBUG] {methodName}: Cache HIT, returning cached value");
            return cachedResult;
        }
        
        _logger.LogDebug("Кэш не найден для {MethodName}, выполняется запрос", methodName);
        Console.WriteLine($"[DEBUG] {methodName}: Cache MISS, executing factory");
        
        var result = await factory();
        
        var expiration = _methodExpiration.TryGetValue(methodName, out var exp) ? exp : _defaultExpiration;
        var options = new MemoryCacheEntryOptions().SetAbsoluteExpiration(expiration);
        _cache.Set(cacheKey, result, options);
        
        return result;
    }
    
    /// <summary>
    /// Удаляет элемент из кэша
    /// </summary>
    /// <param name="methodName">Имя метода API</param>
    /// <param name="args">Аргументы метода</param>
    public void InvalidateCache(string methodName, params object[] args)
    {
        var cacheKey = GenerateCacheKey(methodName, args);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Удалено из кэша: метод {Method}", methodName);
    }
    
    /// <summary>
    /// Удаляет все элементы из кэша для указанного метода
    /// </summary>
    /// <param name="methodName">Имя метода API</param>
    public void InvalidateCacheForMethod(string methodName)
    {
        _logger.LogWarning("Очистка всех записей кэша для метода {Method} не поддерживается", methodName);
    }
} 