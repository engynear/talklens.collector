using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace TalkLens.Collector.Infrastructure.Cache.Redis;

/// <summary>
/// Реализация кэш-провайдера, использующая Redis
/// </summary>
public class RedisCacheProvider<T> : ICacheProvider<T>
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheProvider<T>> _logger;
    private readonly string _keyPrefix;
    
    public RedisCacheProvider(
        IConnectionMultiplexer redis, 
        IConfiguration configuration,
        ILogger<RedisCacheProvider<T>> logger)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _logger = logger;
        
        // Получаем префикс для ключей из конфигурации
        _keyPrefix = configuration["Redis:KeyPrefix"] ?? "talklens:";
    }
    
    /// <summary>
    /// Генерирует полный ключ для Redis с учетом префикса
    /// </summary>
    /// <param name="key">Исходный ключ</param>
    /// <returns>Полный ключ с префиксом</returns>
    private string GetFullKey(string key) => $"{_keyPrefix}{key}";
    
    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync(string key, Func<Task<T>> factory, TimeSpan expirationTime)
    {
        string fullKey = GetFullKey(key);
        
        // Проверяем наличие в кэше
        var cachedValue = await _db.StringGetAsync(fullKey);
        if (!cachedValue.IsNullOrEmpty)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(cachedValue!)!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка десериализации значения из Redis для ключа {Key}: {Error}", key, ex.Message);
            }
        }
        
        // Если нет в кэше или произошла ошибка десериализации, создаем новое значение
        var value = await factory();
        
        try
        {
            string serialized = JsonSerializer.Serialize(value);
            await _db.StringSetAsync(fullKey, serialized, expirationTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сохранения значения в Redis для ключа {Key}: {Error}", key, ex.Message);
        }
        
        return value;
    }
    
    /// <inheritdoc />
    public async Task<T?> GetAsync(string key)
    {
        string fullKey = GetFullKey(key);
        
        var cachedValue = await _db.StringGetAsync(fullKey);
        if (cachedValue.IsNullOrEmpty)
        {
            return default;
        }
        
        try
        {
            return JsonSerializer.Deserialize<T>(cachedValue!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка десериализации значения из Redis для ключа {Key}: {Error}", key, ex.Message);
            return default;
        }
    }
    
    /// <inheritdoc />
    public async Task SetAsync(string key, T value, TimeSpan expirationTime)
    {
        string fullKey = GetFullKey(key);
        
        try
        {
            string serialized = JsonSerializer.Serialize(value);
            await _db.StringSetAsync(fullKey, serialized, expirationTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сохранения значения в Redis для ключа {Key}: {Error}", key, ex.Message);
        }
    }
    
    /// <inheritdoc />
    public async Task RemoveAsync(string key)
    {
        string fullKey = GetFullKey(key);
        await _db.KeyDeleteAsync(fullKey);
    }
    
    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key)
    {
        string fullKey = GetFullKey(key);
        return await _db.KeyExistsAsync(fullKey);
    }
} 