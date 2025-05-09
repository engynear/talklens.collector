using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;
using TalkLens.Collector.Infrastructure.Configuration;

namespace TalkLens.Collector.Infrastructure.Services.Telegram;

/// <summary>
/// Предоставляет функциональность кэширования для запросов к Telegram API с использованием Redis
/// </summary>
public class RedisTelegramApiCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ILogger<RedisTelegramApiCache> _logger;
    private readonly ConcurrentDictionary<string, TimeSpan> _methodExpiration = new();
    private readonly TimeSpan _defaultExpiration;
    private readonly string _keyPrefix;

    public RedisTelegramApiCache(
        IConnectionMultiplexer redis, 
        IOptions<TelegramOptions> telegramOptions,
        IOptions<RedisOptions> redisOptions,
        IConfiguration configuration,
        ILogger<RedisTelegramApiCache> logger)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _logger = logger;
        
        // Получаем настройки из опций
        var cacheOptions = telegramOptions.Value.Cache ?? new TelegramCacheOptions();
        
        // Получаем настройки времени жизни кэша
        _defaultExpiration = TimeSpan.FromMinutes(cacheOptions.DefaultExpirationMinutes);
        
        // Получаем префикс ключей
        _keyPrefix = redisOptions.Value.KeyPrefix;
        
        // Загружаем специальные настройки для методов
        InitializeMethodExpirations(configuration);
        
        _logger.LogInformation("RedisTelegramApiCache инициализирован. Время жизни кэша по умолчанию: {Expiration} минут", 
            cacheOptions.DefaultExpirationMinutes);
    }
    
    private void InitializeMethodExpirations(IConfiguration configuration)
    {
        // Загружаем настройки для конкретных методов
        var methodsSection = configuration.GetSection("Telegram:Cache:MethodExpirations");
        foreach (var methodSection in methodsSection.GetChildren())
        {
            string methodName = methodSection.Key;
            
            // Получаем значение времени жизни для метода
            int expirationMinutes = (int)_defaultExpiration.TotalMinutes; // По умолчанию используем глобальное значение
            if (int.TryParse(methodSection["ExpirationMinutes"], out int configMinutes))
            {
                expirationMinutes = configMinutes;
            }
            
            _methodExpiration[methodName] = TimeSpan.FromMinutes(expirationMinutes);
            _logger.LogDebug("Установлено специальное время жизни кэша для метода {Method}: {Expiration} минут", 
                methodName, expirationMinutes);
        }
    }
    
    /// <summary>
    /// Генерирует ключ кэша на основе метода и параметров
    /// </summary>
    private string GenerateCacheKey(string methodName, params object[] args)
    {
        string argsString = string.Join("_", args);
        return $"{_keyPrefix}{methodName}:{argsString}";
    }
    
    /// <summary>
    /// Получает или создает элемент в кэше
    /// </summary>
    public async Task<T> GetOrCreateAsync<T>(string methodName, Func<Task<T>> factory, bool forceRefresh = false, params object[] args)
    {
        string cacheKey = GenerateCacheKey(methodName, args);
        
        // Если запрошено принудительное обновление, удаляем существующее значение
        if (forceRefresh)
        {
            await _db.KeyDeleteAsync(cacheKey);
            _logger.LogDebug("Принудительное обновление кэша для метода {Method}", methodName);
        }
        
        // Проверяем наличие значения в кэше
        RedisValue cachedValue = await _db.StringGetAsync(cacheKey);
        
        // Если значение найдено в кэше, десериализуем и возвращаем его
        if (!cachedValue.IsNullOrEmpty)
        {
            try
            {
                _logger.LogDebug("Кэш-попадание для метода {Method}", methodName);
                return JsonSerializer.Deserialize<T>(cachedValue!)!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при десериализации значения из кэша для метода {Method}: {Error}", 
                    methodName, ex.Message);
                // В случае ошибки десериализации продолжаем и получаем свежее значение
            }
        }
        
        // Получаем время жизни для метода
        var expiration = _methodExpiration.GetValueOrDefault(methodName, _defaultExpiration);
        
        try
        {
            // Выполняем фабричный метод для получения свежего значения
            _logger.LogDebug("Кэш-промах для метода {Method}, выполнение запроса", methodName);
            var result = await factory();
            
            // Сериализуем и сохраняем результат в кэше
            string serialized = JsonSerializer.Serialize(result);
            await _db.StringSetAsync(cacheKey, serialized, expiration);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при работе с кэшем для метода {Method}: {Error}", methodName, ex.Message);
            
            // В случае ошибки кэширования, просто выполняем запрос напрямую
            return await factory();
        }
    }
    
    /// <summary>
    /// Удаляет элемент из кэша
    /// </summary>
    public async Task InvalidateCacheAsync(string methodName, params object[] args)
    {
        string cacheKey = GenerateCacheKey(methodName, args);
        await _db.KeyDeleteAsync(cacheKey);
        _logger.LogDebug("Удалено из кэша: метод {Method}", methodName);
    }
    
    /// <summary>
    /// Удаляет все элементы из кэша для указанного метода
    /// </summary>
    public async Task InvalidateCacheForMethodAsync(string methodName)
    {
        string pattern = $"{_keyPrefix}{methodName}:*";
        _logger.LogDebug("Очистка всех записей кэша для метода {Method}", methodName);
        
        // Redis позволяет удалять ключи по шаблону, используя команду SCAN и DEL
        var server = _redis.GetServer(_redis.GetEndPoints()[0]);
        
        // Используем SCAN для поиска ключей по шаблону
        var keys = server.Keys(pattern: pattern);
        
        // Удаляем найденные ключи
        foreach (var key in keys)
        {
            await _db.KeyDeleteAsync(key);
        }
    }
} 