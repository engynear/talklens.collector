using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using TalkLens.Collector.Infrastructure.Messengers.Telegram;
using TalkLens.Collector.Infrastructure.Cache;

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
    private readonly IServiceProvider _serviceProvider;
    
    // Хранилище для локальных ссылок на сессии
    // Необходимо, поскольку в Redis мы храним только информацию, но не сами объекты
    private static readonly Dictionary<string, TelegramSession> _localSessions = new();
    
    public RedisTelegramSessionCache(
        IConnectionMultiplexer redis, 
        IConfiguration configuration,
        ILogger<RedisTelegramSessionCache> logger,
        IServiceProvider serviceProvider)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _logger = logger;
        _serviceProvider = serviceProvider;
        
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
    /// Инициализирует кэш сессий из Redis при запуске приложения
    /// </summary>
    public async Task InitializeSessionsFromRedis()
    {
        try
        {
            _logger.LogInformation("Начата инициализация сессий из Redis");
            
            // Получаем экземпляр TelegramSessionManager из ServiceProvider
            var sessionManager = _serviceProvider.GetRequiredService<TelegramSessionManager>();
            
            // Получаем все ключи по паттерну
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{_keyPrefix}*").ToArray();
            
            _logger.LogInformation("Найдено {Count} ключей сессий в Redis", keys.Length);
            
            int successCounter = 0;
            
            // Восстанавливаем каждую сессию
            foreach (var key in keys)
            {
                try
                {
                    string redisKey = key.ToString();
                    var sessionInfoJson = _db.StringGet(redisKey);
                    
                    if (!sessionInfoJson.IsNullOrEmpty)
                    {
                        // Десериализуем информацию о сессии
                        var sessionInfo = JsonSerializer.Deserialize<TelegramSessionInfo>(sessionInfoJson!);
                        
                        if (sessionInfo != null)
                        {
                            // Извлекаем userId и sessionId из информации о сессии
                            string userId = sessionInfo.UserId;
                            string sessionId = sessionInfo.SessionId;
                            
                            _logger.LogDebug("Восстанавливаем сессию {UserId}:{SessionId}", userId, sessionId);
                            
                            // Создаем новую сессию через менеджер
                            var session = await sessionManager.CreateSessionAsync(
                                userId, sessionId, sessionInfo.PhoneNumber);
                            
                            // Проверяем валидность сессии
                            bool isValid = await session.ValidateSessionAsync();
                            
                            if (isValid)
                            {
                                // Добавляем в локальный кэш
                                var localKey = GetLocalKey(userId, sessionId);
                                _localSessions[localKey] = session;
                                
                                successCounter++;
                                _logger.LogInformation("Сессия {UserId}:{SessionId} успешно восстановлена", 
                                    userId, sessionId);
                            }
                            else
                            {
                                _logger.LogWarning("Сессия {UserId}:{SessionId} невалидна", userId, sessionId);
                                // Удаляем невалидную сессию из Redis
                                _db.KeyDelete(redisKey);
                                session.Dispose();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при восстановлении сессии по ключу {Key}", key);
                }
            }
            
            _logger.LogInformation("Восстановлено {Count} сессий из Redis", successCounter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации сессий из Redis");
        }
    }
    
    /// <summary>
    /// Получает все сессии Telegram из локального кэша
    /// </summary>
    /// <returns>Словарь с ключами и сессиями Telegram</returns>
    public Dictionary<string, TelegramSession> GetAllSessions()
    {
        try
        {
            // Создаем копию словаря для безопасного доступа
            var sessionsCopy = new Dictionary<string, TelegramSession>(_localSessions);
            _logger.LogDebug("Получено {Count} сессий из локального кэша", sessionsCopy.Count);
            return sessionsCopy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении всех сессий из кэша");
            return new Dictionary<string, TelegramSession>();
        }
    }
    
    /// <summary>
    /// Получает сессию Telegram из кэша
    /// </summary>
    public async Task<TelegramSession?> GetSessionAsync(string userId, string sessionId)
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
            
            // Восстанавливаем сессию из информации
            _logger.LogDebug("Восстанавливаем сессию {SessionId} из информации в Redis", sessionId);
            
            try
            {
                // Получаем экземпляр TelegramSessionManager из ServiceProvider
                var sessionManager = _serviceProvider.GetRequiredService<TelegramSessionManager>();
                
                // Создаем новую сессию через менеджер
                var restoredSession = await sessionManager.CreateSessionAsync(
                    userId, sessionId, sessionInfo.PhoneNumber);
                
                // Проверяем валидность сессии
                bool isValid = await restoredSession.ValidateSessionAsync();
                
                if (isValid)
                {
                    // Добавляем в локальный кэш
                    _localSessions[localKey] = restoredSession;
                    
                    _logger.LogInformation("Сессия {SessionId} успешно восстановлена из Redis", sessionId);
                    return restoredSession;
                }
                else
                {
                    _logger.LogWarning("Восстановленная сессия {SessionId} невалидна", sessionId);
                    restoredSession.Dispose();
                    
                    // Удаляем невалидную сессию из Redis
                    _db.KeyDelete(key);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при восстановлении сессии {SessionId} из Redis", sessionId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении сессии {SessionId} из Redis", sessionId);
            return null;
        }
    }
    
    /// <summary>
    /// Получает сессию Telegram из кэша (синхронная версия для совместимости)
    /// </summary>
    public TelegramSession? GetSession(string userId, string sessionId)
    {
        // Проверяем сначала локальное хранилище
        var localKey = GetLocalKey(userId, sessionId);
        if (_localSessions.TryGetValue(localKey, out var session))
        {
            // Существующая сессия в локальном кэше
            return session;
        }
        
        // Для обратной совместимости, пытаемся асинхронно восстановить сессию
        // Это блокирующий вызов, в идеале все обращения должны быть заменены на GetSessionAsync
        try
        {
            return GetSessionAsync(userId, sessionId).GetAwaiter().GetResult();
        }
        catch
        {
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
} 