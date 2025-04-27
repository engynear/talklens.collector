using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using TalkLens.Collector.Infrastructure.Messengers.Telegram;

namespace TalkLens.Collector.Infrastructure.Services.Telegram;

/// <summary>
/// Реализация кэша для сессий Telegram на базе Redis
/// </summary>
public class RedisTelegramSessionCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ILogger<RedisTelegramSessionCache> _logger;
    private readonly TimeSpan _defaultExpiry;
    private readonly string _keyPrefix;
    
    // Хранилище для локальных ссылок на сессии
    // Необходимо, поскольку в Redis мы храним только информацию, но не сами объекты
    private static readonly Dictionary<string, TelegramSession> _localSessions = new();
    
    public RedisTelegramSessionCache(
        IConnectionMultiplexer redis, 
        IConfiguration configuration,
        ILogger<RedisTelegramSessionCache> logger)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _logger = logger;
        
        // Парсим настройки времени жизни кэша (часы)
        int sessionExpiryHours = 1; // значение по умолчанию - 1 час
        if (int.TryParse(configuration["Redis:SessionExpiryHours"], out int configHours))
        {
            sessionExpiryHours = configHours;
        }
        _defaultExpiry = TimeSpan.FromHours(sessionExpiryHours);
        
        // Получаем префикс ключей
        _keyPrefix = configuration["Redis:KeyPrefix"] ?? "telegram:session:";
        
        _logger.LogInformation("RedisTelegramSessionCache инициализирован с временем жизни {Expiry}", _defaultExpiry);
    }
    
    /// <summary>
    /// Получает сессию Telegram из кэша
    /// </summary>
    public TelegramSession? GetSession(string userId, string sessionId)
    {
        var key = GetRedisKey(userId, sessionId);
        
        // Проверяем сначала локальное хранилище
        var localKey = GetLocalKey(userId, sessionId);
        if (_localSessions.TryGetValue(localKey, out var session))
        {
            _logger.LogDebug("Сессия {SessionId} найдена в локальном кэше", sessionId);
            
            // Проверяем существование в Redis для продления срока жизни
            if (_db.KeyExists(key))
            {
                // Продлеваем срок жизни ключа в Redis
                _db.KeyExpire(key, _defaultExpiry);
                _logger.LogDebug("Срок жизни ключа в Redis продлён для {SessionId}", sessionId);
                return session;
            }
            else
            {
                // Если в Redis ключа нет, удаляем и из локального хранилища
                _logger.LogWarning("Сессия {SessionId} не найдена в Redis, удаляем из локального кэша", sessionId);
                RemoveSession(userId, sessionId);
                return null;
            }
        }
        
        // Проверяем наличие информации о сессии в Redis
        var sessionInfoJson = _db.StringGet(key);
        if (sessionInfoJson.IsNullOrEmpty)
        {
            _logger.LogDebug("Сессия {SessionId} не найдена в Redis", sessionId);
            return null;
        }
        
        try
        {
            // Десериализуем информацию о сессии
            var sessionInfo = JsonSerializer.Deserialize<TelegramSessionInfo>(sessionInfoJson!);
            
            if (sessionInfo == null)
            {
                _logger.LogError("Ошибка десериализации информации о сессии {SessionId}", sessionId);
                return null;
            }
            
            // Поскольку мы не можем хранить объект TelegramSession в Redis,
            // мы должны воссоздать его на основе сохраненной информации
            _logger.LogDebug("Воссоздаем сессию {SessionId} из информации в Redis", sessionId);
            
            // Здесь нужно воссоздать объект TelegramSession
            // Для этого нам нужен доступ к TelegramSessionManager
            // Это сложнее реализовать в такой модели кэширования
            
            // Данная реализация должна быть доработана для интеграции с TelegramSessionManager
            // В текущем виде это заглушка
            _logger.LogWarning("Воссоздание сессии из Redis не реализовано полностью");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении сессии {SessionId} из Redis", sessionId);
            return null;
        }
    }
    
    /// <summary>
    /// Сохраняет сессию Telegram в кэш
    /// </summary>
    public void SetSession(string userId, string sessionId, TelegramSession session)
    {
        var key = GetRedisKey(userId, sessionId);
        var localKey = GetLocalKey(userId, sessionId);
        
        try
        {
            // Создаем информацию о сессии для хранения в Redis
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
            
            // Сериализуем информацию в JSON
            var sessionInfoJson = JsonSerializer.Serialize(sessionInfo);
            
            // Сохраняем в Redis
            _db.StringSet(key, sessionInfoJson, _defaultExpiry);
            
            // Сохраняем ссылку на объект сессии в локальном хранилище
            _localSessions[localKey] = session;
            
            _logger.LogDebug("Сессия {SessionId} сохранена в Redis и локальном кэше", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении сессии {SessionId} в Redis", sessionId);
        }
    }
    
    /// <summary>
    /// Удаляет сессию Telegram из кэша
    /// </summary>
    public void RemoveSession(string userId, string sessionId)
    {
        var key = GetRedisKey(userId, sessionId);
        var localKey = GetLocalKey(userId, sessionId);
        
        try
        {
            // Удаляем из Redis
            _db.KeyDelete(key);
            
            // Удаляем из локального хранилища и освобождаем ресурсы
            if (_localSessions.TryGetValue(localKey, out var session))
            {
                session.Dispose();
                _localSessions.Remove(localKey);
            }
            
            _logger.LogDebug("Сессия {SessionId} удалена из Redis и локального кэша", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении сессии {SessionId} из Redis", sessionId);
        }
    }
    
    /// <summary>
    /// Получает ключ для хранения сессии в Redis
    /// </summary>
    private string GetRedisKey(string userId, string sessionId) => $"{_keyPrefix}{userId}:{sessionId}";
    
    /// <summary>
    /// Получает ключ для локального хранилища сессий
    /// </summary>
    private string GetLocalKey(string userId, string sessionId) => $"{userId}:{sessionId}";
    
    /// <summary>
    /// Класс для хранения информации о сессии в Redis
    /// </summary>
    private class TelegramSessionInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public string SessionFilePath { get; set; } = string.Empty;
        public string UpdatesFilePath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
} 