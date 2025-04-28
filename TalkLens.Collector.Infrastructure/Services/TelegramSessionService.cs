using Microsoft.Extensions.Caching.Memory;
using TalkLens.Collector.Domain.Enums.Telegram;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Database;
using TalkLens.Collector.Infrastructure.Messengers.Telegram;
using TalkLens.Collector.Infrastructure.Services.Telegram;
using TL;
using System.Collections.Concurrent;
using TalkLens.Collector.Domain.Models;

namespace TalkLens.Collector.Infrastructure.Services;

public class TelegramSessionService : ITelegramSessionService
{
    private readonly RedisTelegramSessionCache _redisCache;
    private readonly ITelegramSessionRepository _sessionRepository;
    private readonly ITelegramSubscriptionRepository _subscriptionRepository;
    private readonly TelegramSessionManager _sessionManager;
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(15);

    public TelegramSessionService(
        RedisTelegramSessionCache redisCache, 
        ITelegramSessionRepository sessionRepository,
        ITelegramSubscriptionRepository subscriptionRepository,
        TelegramSessionManager sessionManager)
    {
        _redisCache = redisCache;
        _sessionRepository = sessionRepository;
        _subscriptionRepository = subscriptionRepository;
        _sessionManager = sessionManager;
    }

    private async Task<TelegramSession?> GetOrRestoreSessionAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[DEBUG] Trying to restore session. UserId: {userId}, SessionId: {sessionId}");
        
        // Проверяем глобальный кэш через Redis
        var globalSession = _redisCache.GetSession(userId, sessionId);
        Console.WriteLine($"[DEBUG] Redis cache check: {(globalSession != null ? "Found" : "Not found")}");
        
        if (globalSession != null)
        {
            try 
            {
                // Проверяем статус сессии перед валидацией
                // Не выполняем валидацию, если требуется код подтверждения или 2FA
                if (globalSession.Status == TelegramLoginStatus.VerificationCodeRequired || 
                    globalSession.Status == TelegramLoginStatus.TwoFactorRequired)
                {
                    Console.WriteLine($"[DEBUG] Redis session is in {globalSession.Status} state, skipping validation");
                    return globalSession;
                }
                
                // Для сессии из Redis проверяем авторизацию только если она должна быть уже авторизована
                if (await globalSession.ValidateSessionAsync())
                {
                    Console.WriteLine("[DEBUG] Redis session is valid");
                    return globalSession;
                }
                Console.WriteLine("[DEBUG] Redis session is invalid");
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("[DEBUG] Redis session is disposed, removing from cache");
                _redisCache.RemoveSession(userId, sessionId);
                globalSession = null;
            }
            catch 
            {
                Console.WriteLine("[DEBUG] Error validating Redis session, removing");
                _redisCache.RemoveSession(userId, sessionId);
                globalSession = null;
            }
        }

        // Проверяем БД если сессия не найдена в Redis
        var dbSession = await _sessionRepository.GetActiveSessionAsync(userId, sessionId, cancellationToken);
        Console.WriteLine($"[DEBUG] DB session check: {(dbSession != null ? "Found" : "Not found")}");
        
        if (dbSession == null)
        {
            return null;
        }

        try
        {
            Console.WriteLine("[DEBUG] Creating new session from DB data");
            
            // Создаем сессию с помощью менеджера сессий
            var session = await _sessionManager.CreateSessionAsync(userId, sessionId, dbSession.PhoneNumber);
            
            try 
            {
                // Для сессий из БД проверяем авторизацию
                if (!await session.ValidateSessionAsync())
                {
                    Console.WriteLine("[DEBUG] New session from DB is invalid");
                    await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
                    session.Dispose();
                    return null;
                }
                
                Console.WriteLine("[DEBUG] New session from DB is valid, adding to Redis cache");
                // Если сессия валидна, помещаем её в Redis кэш
                _redisCache.SetSession(userId, sessionId, session);
                return session;
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("[DEBUG] New session from DB is disposed");
                session.Dispose();
                await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error validating new session from DB: {ex.Message}");
                session.Dispose();
                await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error creating session from DB data: {ex.Message}");
            return null;
        }
    }

    public async Task<TelegramLoginResult> LoginWithPhoneAsync(string userId, string sessionId, string phone, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[DEBUG] Starting login with phone. UserId: {userId}, SessionId: {sessionId}");
        
        // Проверяем, существует ли уже сессия с таким номером телефона
        if (await _sessionRepository.ExistsActiveSessionWithPhoneAsync(phone, cancellationToken))
        {
            Console.WriteLine($"[DEBUG] Session with phone {phone} already exists");
            return new TelegramLoginResult
            {
                Status = TelegramLoginStatus.Failed,
                PhoneNumber = phone,
                Error = "Сессия с таким номером телефона уже существует"
            };
        }
        
        // Очищаем предыдущие сессии
        _redisCache.RemoveSession(userId, sessionId);

        // Создаем новую сессию с помощью менеджера сессий
        var session = await _sessionManager.CreateSessionAsync(userId, sessionId, phone);
        Console.WriteLine("[DEBUG] Created new session");
        
        // Сохраняем временную сессию в Redis с маркером "temporary"
        // Мы можем сохранить временную сессию в Redis, в отличие от MemoryCache она останется даже при перезапуске
        _redisCache.SetSession(userId, sessionId, session);
        Console.WriteLine("[DEBUG] Session added to Redis cache");
        
        try 
        {
            var nextStep = await session.StartLoginAsync(phone);
            var status = await HandleLoginResponseAsync(session, nextStep);
            Console.WriteLine($"[DEBUG] Login status: {status}");

            if (status == TelegramLoginStatus.Failed)
            {
                _redisCache.RemoveSession(userId, sessionId);
            }

            return new TelegramLoginResult
            {
                Status = status,
                PhoneNumber = phone
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error during login: {ex.Message}");
            _redisCache.RemoveSession(userId, sessionId);
            return new TelegramLoginResult 
            { 
                Status = TelegramLoginStatus.Failed,
                PhoneNumber = phone,
                Error = ex.Message
            };
        }
    }

    public async Task<TelegramLoginResult> SubmitVerificationCodeAsync(string userId, string sessionId, string code, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[DEBUG] Submitting verification code. UserId: {userId}, SessionId: {sessionId}");
        var session = await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
        if (session == null)
        {
            Console.WriteLine("[DEBUG] Session not found for verification");
            return new TelegramLoginResult 
            { 
                Status = TelegramLoginStatus.Expired,
                Error = "Session expired"
            };
        }

        Console.WriteLine("[DEBUG] Session found for verification");
        try
        {
            var nextStep = await session.SubmitVerificationCodeAsync(code);
            var status = await HandleLoginResponseAsync(session, nextStep);
            Console.WriteLine($"[DEBUG] Verification status: {status}");

            if (status == TelegramLoginStatus.Success)
            {
                Console.WriteLine("[DEBUG] Verification successful, saving to DB and global cache");
                var sessionData = new TelegramSessionData
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SessionId = sessionId,
                    PhoneNumber = session.PhoneNumber!,
                    CreatedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow,
                    IsActive = true
                };

                // Сначала сохраняем в базу данных
                await _sessionRepository.SaveSessionAsync(sessionData, cancellationToken);
                
                // Затем сохраняем файл сессии в хранилище и ждем завершения
                bool saveSuccess = await _sessionManager.SaveSessionAsync(userId, sessionId);
                if (!saveSuccess)
                {
                    Console.WriteLine("[DEBUG] Failed to save session file to storage");
                    // Если не удалось сохранить сессию, считаем авторизацию неуспешной
                    await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
                    return new TelegramLoginResult 
                    { 
                        Status = TelegramLoginStatus.Failed,
                        PhoneNumber = session.PhoneNumber,
                        Error = "Не удалось сохранить сессию в хранилище"
                    };
                }
                
                // Перемещаем сессию из временного в глобальный кэш только после успешного сохранения
                var cacheKey = GetCacheKey(userId, sessionId);
                _redisCache.RemoveSession(userId, sessionId);
                _redisCache.SetSession(userId, sessionId, session);
                Console.WriteLine("[DEBUG] Successfully saved session to storage and global cache");
            }
            else
            {
                Console.WriteLine("[DEBUG] Verification failed, updating DB status");
                await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
            }

            return new TelegramLoginResult 
            { 
                Status = status,
                PhoneNumber = session.PhoneNumber
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Verification error: {ex.Message}");
            await session.SetStatusAsync(TelegramLoginStatus.Failed);
            await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
            return new TelegramLoginResult 
            { 
                Status = TelegramLoginStatus.Failed,
                PhoneNumber = session.PhoneNumber,
                Error = ex.Message
            };
        }
    }

    public async Task<TelegramLoginResult> SubmitTwoFactorPasswordAsync(string userId, string sessionId, string password, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[DEBUG] Submitting 2FA password. UserId: {userId}, SessionId: {sessionId}");
        var session = await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
        if (session == null)
        {
            Console.WriteLine("[DEBUG] Session not found for 2FA");
            return new TelegramLoginResult 
            { 
                Status = TelegramLoginStatus.Expired,
                Error = "Session expired"
            };
        }

        Console.WriteLine("[DEBUG] Session found for 2FA");
        try
        {
            var nextStep = await session.SubmitTwoFactorPasswordAsync(password);
            var status = await HandleLoginResponseAsync(session, nextStep);
            Console.WriteLine($"[DEBUG] 2FA status: {status}");

            if (status == TelegramLoginStatus.Success)
            {
                Console.WriteLine("[DEBUG] 2FA successful, saving to DB and global cache");
                var sessionData = new TelegramSessionData
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SessionId = sessionId,
                    PhoneNumber = session.PhoneNumber!,
                    CreatedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow,
                    IsActive = true
                };

                // Сначала сохраняем в базу данных
                await _sessionRepository.SaveSessionAsync(sessionData, cancellationToken);
                
                // Затем сохраняем файл сессии в хранилище и ждем завершения
                bool saveSuccess = await _sessionManager.SaveSessionAsync(userId, sessionId);
                if (!saveSuccess)
                {
                    Console.WriteLine("[DEBUG] Failed to save session file to storage");
                    // Если не удалось сохранить сессию, считаем авторизацию неуспешной
                    await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
                    return new TelegramLoginResult 
                    { 
                        Status = TelegramLoginStatus.Failed,
                        PhoneNumber = session.PhoneNumber,
                        Error = "Не удалось сохранить сессию в хранилище"
                    };
                }
                
                // Перемещаем сессию из временного в глобальный кэш только после успешного сохранения
                var cacheKey = GetCacheKey(userId, sessionId);
                _redisCache.RemoveSession(userId, sessionId);
                _redisCache.SetSession(userId, sessionId, session);
                Console.WriteLine("[DEBUG] Successfully saved session to storage and global cache");
            }
            else
            {
                Console.WriteLine("[DEBUG] 2FA failed, updating DB status");
                await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
            }

            return new TelegramLoginResult 
            { 
                Status = status,
                PhoneNumber = session.PhoneNumber
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] 2FA error: {ex.Message}");
            await session.SetStatusAsync(TelegramLoginStatus.Failed);
            await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
            return new TelegramLoginResult 
            { 
                Status = TelegramLoginStatus.Failed,
                PhoneNumber = session.PhoneNumber,
                Error = ex.Message
            };
        }
    }

    public async Task<List<TelegramContactResponse>> GetContactsAsync(
        string userId, 
        string sessionId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[DEBUG] Получение контактов для UserId: {userId}, SessionId: {sessionId}, ForceRefresh: {forceRefresh}");
        
        // Используем менеджер сессий для получения/создания сессии
        var session = await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
        if (session == null)
        {
            Console.WriteLine("[DEBUG] Сессия не найдена");
            throw new Exception("Сессия не найдена или истекла");
        }
        
        // Проверяем статус авторизации
        if (session.Status != TelegramLoginStatus.Success)
        {
            Console.WriteLine($"[DEBUG] Сессия не авторизована. Статус: {session.Status}");
            throw new Exception("Сессия не авторизована");
        }

        try
        {
            // Используем метод сессии для получения контактов, передавая параметр forceRefresh
            var contacts = await session.GetContactsAsync(forceRefresh, cancellationToken);
            Console.WriteLine($"[DEBUG] Успешно получено {contacts.Count()} контактов");
            return contacts;
        }
        catch (ObjectDisposedException)
        {
            Console.WriteLine("[DEBUG] Сессия была освобождена, пытаемся восстановить");
            _redisCache.RemoveSession(userId, sessionId);
            
            // Пытаемся восстановить сессию
            session = await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
            if (session == null)
            {
                Console.WriteLine("[DEBUG] Не удалось восстановить сессию");
                throw new Exception("Не удалось восстановить сессию");
            }
            
            // Повторяем попытку с новой сессией
            var contacts = await session.GetContactsAsync(forceRefresh, cancellationToken);
            Console.WriteLine($"[DEBUG] Успешно получено {contacts.Count()} контактов после восстановления сессии");
            return contacts;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Неожиданная ошибка при получении контактов: {ex.Message}");
            throw;
        }
    }

    public async Task<List<TelegramSessionData>> GetActiveSessionsAsync(
        string userId, 
        CancellationToken cancellationToken)
    {
        return await _sessionRepository.GetActiveSessionsAsync(userId, cancellationToken);
    }

    // Реализация базового интерфейса ISessionService
    async Task<List<SessionData>> ISessionService.GetActiveSessionsAsync(string userId, CancellationToken cancellationToken)
    {
        var telegramSessions = await GetActiveSessionsAsync(userId, cancellationToken);
        // Преобразуем TelegramSessionData в базовый тип SessionData
        return telegramSessions.Cast<SessionData>().ToList();
    }

    private string GetCacheKey(string userId, string sessionId) => $"{userId}_{sessionId}";

    /// <summary>
    /// Обрабатывает ответ процесса авторизации
    /// </summary>
    private async Task<TelegramLoginStatus> HandleLoginResponseAsync(TelegramSession session, string loginResponse)
    {
        switch (loginResponse)
        {
            case "verification_code":
                await session.SetStatusAsync(TelegramLoginStatus.VerificationCodeRequired);
                // Устанавливаем время жизни сессии в Redis при необходимости ввода кода
                _redisCache.SetSessionExpiry(session.GetUserId(), session.GetSessionId(), SessionTimeout);
                Console.WriteLine($"[DEBUG] Set session expiry to {SessionTimeout.TotalMinutes} minutes for verification");
                break;
            case "password":
                await session.SetStatusAsync(TelegramLoginStatus.TwoFactorRequired);
                // Устанавливаем время жизни сессии в Redis при необходимости ввода 2FA
                _redisCache.SetSessionExpiry(session.GetUserId(), session.GetSessionId(), SessionTimeout);
                Console.WriteLine($"[DEBUG] Set session expiry to {SessionTimeout.TotalMinutes} minutes for 2FA");
                break;
            case "":
            case null:
                await session.SetStatusAsync(TelegramLoginStatus.Success);
                break;
            default:
                await session.SetStatusAsync(TelegramLoginStatus.Failed);
                break;
        }
        return session.Status;
    }

    /// <summary>
    /// Удаляет указанную сессию Telegram
    /// </summary>
    public async Task<bool> DeleteSessionAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Удаляем из Redis кэша
            _redisCache.RemoveSession(userId, sessionId);
            
            // Очищаем локальные файлы
            await _sessionManager.DeleteSessionAsync(userId, sessionId);
            
            // Удаляем из БД
            return await _sessionRepository.DeleteSessionAsync(userId, sessionId, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error deleting session: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Удаляет все сессии Telegram указанного пользователя
    /// </summary>
    public async Task<int> DeleteAllSessionsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Получаем список активных сессий
            var sessions = await _sessionRepository.GetActiveSessionsAsync(userId, cancellationToken);
            
            // Удаляем каждую сессию из кэша и файловой системы
            foreach (var session in sessions)
            {
                // Удаляем из Redis кэша
                _redisCache.RemoveSession(userId, session.SessionId);
                
                // Очищаем локальные файлы
                await _sessionManager.DeleteSessionAsync(userId, session.SessionId);
            }
            
            // Удаляем из БД все сессии пользователя
            return await _sessionRepository.DeleteAllUserSessionsAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error deleting all sessions: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Получает объект сессии Telegram для доступа к API
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Объект сессии или null, если сессия не найдена</returns>
    public async Task<object?> GetSessionAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[DEBUG] Getting session object for API access. UserId: {userId}, SessionId: {sessionId}");
        return await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
    }

    /// <summary>
    /// Получает список последних 10 контактов с сортировкой по дате последнего сообщения
    /// </summary>
    public async Task<List<TelegramContactResponse>> GetRecentContactsAsync(
        string userId, 
        string sessionId, 
        bool forceRefresh = false, 
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[DEBUG] Получение последних контактов для UserId: {userId}, SessionId: {sessionId}, ForceRefresh: {forceRefresh}");
        
        // Используем менеджер сессий для получения/создания сессии
        var session = await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
        if (session == null)
        {
            Console.WriteLine("[DEBUG] Сессия не найдена");
            throw new Exception("Сессия не найдена или истекла");
        }
        
        // Проверяем статус авторизации
        if (session.Status != TelegramLoginStatus.Success)
        {
            Console.WriteLine($"[DEBUG] Сессия не авторизована. Статус: {session.Status}");
            throw new Exception("Сессия не авторизована");
        }

        try
        {
            // Используем метод сессии для получения последних контактов
            var contacts = await session.GetRecentContactsAsync(forceRefresh, cancellationToken);
            Console.WriteLine($"[DEBUG] Успешно получено {contacts.Count()} последних контактов");
            return contacts;
        }
        catch (ObjectDisposedException)
        {
            Console.WriteLine("[DEBUG] Сессия была освобождена, пытаемся восстановить");
            _redisCache.RemoveSession(userId, sessionId);
            
            // Пытаемся восстановить сессию
            session = await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
            if (session == null)
            {
                Console.WriteLine("[DEBUG] Не удалось восстановить сессию");
                throw new Exception("Не удалось восстановить сессию");
            }
            
            // Повторяем попытку с новой сессией
            var contacts = await session.GetRecentContactsAsync(forceRefresh, cancellationToken);
            Console.WriteLine($"[DEBUG] Успешно получено {contacts.Count()} последних контактов после восстановления сессии");
            return contacts;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Неожиданная ошибка при получении контактов: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TelegramSubscriptionData> AddSubscriptionAsync(
        string userId, 
        string sessionId, 
        long interlocutorId, 
        CancellationToken cancellationToken)
    {
        // Проверяем существование сессии
        var session = await _sessionRepository.GetActiveSessionAsync(userId, sessionId, cancellationToken);
        if (session == null)
        {
            throw new InvalidOperationException("Сессия не найдена");
        }
        
        // Проверяем, существует ли уже подписка
        if (await _subscriptionRepository.ExistsSubscriptionAsync(userId, sessionId, interlocutorId, cancellationToken))
        {
            throw new InvalidOperationException("Подписка уже существует");
        }
        
        // Создаем новую подписку
        var subscription = new TelegramSubscriptionData
        {
            UserId = userId,
            SessionId = sessionId,
            TelegramInterlocutorId = interlocutorId,
            CreatedAt = DateTime.UtcNow
        };
        
        return await _subscriptionRepository.AddSubscriptionAsync(subscription, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<bool> RemoveSubscriptionAsync(
        string userId, 
        string sessionId, 
        long interlocutorId, 
        CancellationToken cancellationToken)
    {
        // Проверяем существование сессии
        var session = await _sessionRepository.GetActiveSessionAsync(userId, sessionId, cancellationToken);
        if (session == null)
        {
            throw new InvalidOperationException("Сессия не найдена");
        }
        
        return await _subscriptionRepository.RemoveSubscriptionAsync(userId, sessionId, interlocutorId, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<List<TelegramSubscriptionData>> GetSubscriptionsAsync(
        string userId, 
        string sessionId, 
        CancellationToken cancellationToken)
    {
        // Проверяем существование сессии
        var session = await _sessionRepository.GetActiveSessionAsync(userId, sessionId, cancellationToken);
        if (session == null)
        {
            throw new InvalidOperationException("Сессия не найдена");
        }
        
        return await _subscriptionRepository.GetSessionSubscriptionsAsync(userId, sessionId, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<bool> HasSubscriptionAsync(
        string userId, 
        string sessionId, 
        long interlocutorId, 
        CancellationToken cancellationToken)
    {
        return await _subscriptionRepository.ExistsSubscriptionAsync(userId, sessionId, interlocutorId, cancellationToken);
    }
}