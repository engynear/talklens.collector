using Microsoft.Extensions.Caching.Memory;
using TalkLens.Collector.Domain.Enums.Telegram;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Database;
using TalkLens.Collector.Infrastructure.Messengers.Telegram;
using TL;
using System.Collections.Concurrent;
using System.Linq;

namespace TalkLens.Collector.Infrastructure.Services;

public class TelegramSessionService : ITelegramSessionService
{
    private readonly IMemoryCache _cache;
    private readonly ITelegramSessionRepository _sessionRepository;
    private readonly TelegramMessageCollectorService _messageCollector;
    private readonly TelegramSubscriptionManager _subscriptionManager;
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(3);
    // Ключ: "{userId}_{sessionId}_{interlocutorId}"
    private readonly ConcurrentDictionary<string, bool> _activeSubscriptions = new();

    private static string GetSubscriptionKey(string userId, string sessionId, long interlocutorId) 
        => $"{userId}_{sessionId}_{interlocutorId}";

    public TelegramSessionService(
        IMemoryCache cache, 
        ITelegramSessionRepository sessionRepository,
        TelegramMessageCollectorService messageCollector,
        TelegramSubscriptionManager subscriptionManager)
    {
        _cache = cache;
        _sessionRepository = sessionRepository;
        _messageCollector = messageCollector;
        _subscriptionManager = subscriptionManager;
    }

    private async Task<TelegramSession?> GetOrRestoreSessionAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey(userId, sessionId);
        Console.WriteLine($"[DEBUG] Trying to restore session. UserId: {userId}, SessionId: {sessionId}");
        
        // Проверяем глобальный кэш
        var globalSession = TelegramClientCache.GetSession(userId, sessionId);
        Console.WriteLine($"[DEBUG] Global cache check: {(globalSession != null ? "Found" : "Not found")}");
        if (globalSession != null)
        {
            try 
            {
                // Для глобального кэша всегда проверяем авторизацию, т.к. там только успешные сессии
                if (await globalSession.ValidateSessionAsync())
                {
                    Console.WriteLine("[DEBUG] Global session is valid");
                    return globalSession;
                }
                Console.WriteLine("[DEBUG] Global session is invalid");
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("[DEBUG] Global session is disposed, removing from cache");
                TelegramClientCache.RemoveSession(userId, sessionId);
                globalSession = null;
            }
            catch 
            {
                Console.WriteLine("[DEBUG] Error validating global session, removing");
                TelegramClientCache.RemoveSession(userId, sessionId);
                globalSession = null;
            }
        }
        
        // Проверяем временный кэш (для процесса авторизации)
        if (_cache.TryGetValue(cacheKey, out TelegramSession? tempSession))
        {
            Console.WriteLine("[DEBUG] Found session in temporary cache");
            try 
            {
                // Для временного кэша просто проверяем существование файла
                if (tempSession.IsSessionFileValid())
                {
                    Console.WriteLine("[DEBUG] Temporary session file is valid");
                    return tempSession;
                }
                Console.WriteLine("[DEBUG] Temporary session file is invalid");
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("[DEBUG] Temporary session is disposed");
                _cache.Remove(cacheKey);
                tempSession = null;
            }
            catch 
            {
                Console.WriteLine("[DEBUG] Error checking temporary session file");
                _cache.Remove(cacheKey);
                tempSession = null;
            }
        }
        else
        {
            Console.WriteLine("[DEBUG] Session not found in temporary cache");
        }

        // Проверяем БД только если нет сессии в кэшах
        var dbSession = await _sessionRepository.GetActiveSessionAsync(userId, sessionId, cancellationToken);
        Console.WriteLine($"[DEBUG] DB session check: {(dbSession != null ? "Found" : "Not found")}");
        if (dbSession == null)
        {
            return null;
        }

        try
        {
            Console.WriteLine("[DEBUG] Creating new session from DB data");
            var session = new TelegramSession(userId, sessionId, dbSession.PhoneNumber);
            
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
                
                Console.WriteLine("[DEBUG] New session from DB is valid, adding to global cache");
                // Если сессия валидна, помещаем её в глобальный кэш
                TelegramClientCache.SetSession(userId, sessionId, session);
                return session;
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("[DEBUG] New session from DB is disposed");
                session.Dispose();
                await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
                return null;
            }
            catch 
            {
                Console.WriteLine("[DEBUG] Error validating new session from DB");
                session.Dispose();
                await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
                return null;
            }
        }
        catch (Exception)
        {
            Console.WriteLine("[DEBUG] Error creating session from DB");
            await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
            return null;
        }
    }

    public async Task<TelegramLoginResult> LoginWithPhoneAsync(string userId, string sessionId, string phone, CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey(userId, sessionId);
        Console.WriteLine($"[DEBUG] Starting login with phone. UserId: {userId}, SessionId: {sessionId}");
        
        // Очищаем предыдущие сессии
        if (_cache.TryGetValue(cacheKey, out TelegramSession? existingSession))
        {
            Console.WriteLine("[DEBUG] Found and removing existing temporary session");
            existingSession?.Dispose();
            _cache.Remove(cacheKey);
        }
        TelegramClientCache.RemoveSession(userId, sessionId);

        var session = new TelegramSession(userId, sessionId);
        Console.WriteLine("[DEBUG] Created new session");
        
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(SessionTimeout)
            .RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                if (value is TelegramSession telegramSession)
                {
                    Console.WriteLine($"[DEBUG] Session evicted from temporary cache. Reason: {reason}");
                    telegramSession.Dispose();
                }
            });
            
        _cache.Set(cacheKey, session, cacheEntryOptions);
        Console.WriteLine("[DEBUG] Session added to temporary cache");
        
        try 
        {
            var nextStep = await session.StartLoginAsync(phone);
            var status = await HandleLoginResponseAsync(session, nextStep);
            Console.WriteLine($"[DEBUG] Login status: {status}");

            if (status == TelegramLoginStatus.Failed)
            {
                Console.WriteLine("[DEBUG] Login failed, removing session");
                session.Dispose();
                _cache.Remove(cacheKey);
            }

            return new TelegramLoginResult 
            { 
                Status = status,
                PhoneNumber = session.PhoneNumber
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Login error: {ex.Message}");
            await session.SetStatusAsync(TelegramLoginStatus.Failed);
            session.Dispose();
            _cache.Remove(cacheKey);
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

                await _sessionRepository.SaveSessionAsync(sessionData, cancellationToken);
                
                // Перемещаем сессию из временного в глобальный кэш
                var cacheKey = GetCacheKey(userId, sessionId);
                _cache.Remove(cacheKey);
                TelegramClientCache.SetSession(userId, sessionId, session);
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

                await _sessionRepository.SaveSessionAsync(sessionData, cancellationToken);
                
                // Перемещаем сессию из временного в глобальный кэш
                var cacheKey = GetCacheKey(userId, sessionId);
                _cache.Remove(cacheKey);
                TelegramClientCache.SetSession(userId, sessionId, session);
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

    public async Task<TelegramLoginResult> GetSessionStatusAsync(
        string userId, 
        string sessionId, 
        CancellationToken cancellationToken)
    {
        var session = await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
        if (session == null)
            return new TelegramLoginResult 
            { 
                Status = TelegramLoginStatus.Expired,
                Error = "Session expired"
            };

        // Проверяем авторизацию только для успешных сессий
        if (session.Status == TelegramLoginStatus.Success && !await session.ValidateSessionAsync())
        {
            await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
            session.Dispose();
            return new TelegramLoginResult 
            { 
                Status = TelegramLoginStatus.Expired,
                Error = "Session expired"
            };
        }

        return new TelegramLoginResult 
        { 
            Status = session.Status,
            PhoneNumber = session.PhoneNumber
        };
    }

    public async Task<List<TelegramContactResponse>> GetContactsAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        var session = await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
        if (session == null)
            throw new Exception("Session expired");

        return await session.GetContactsAsync();
    }

    public async Task<List<TelegramSessionData>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken)
    {
        return await _sessionRepository.GetActiveSessionsAsync(userId, cancellationToken);
    }

    public async Task SubscribeToUpdatesAsync(string userId, string sessionId, long interlocutorId, CancellationToken cancellationToken)
    {
        // Проверяем, нет ли уже активной подписки
        if (!_subscriptionManager.TryAddSubscription(userId, sessionId, interlocutorId))
        {
            throw new InvalidOperationException($"Подписка на обновления для этой сессии и собеседника (ID: {interlocutorId}) уже существует");
        }

        var session = await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
        if (session == null)
        {
            _subscriptionManager.RemoveSubscription(userId, sessionId, interlocutorId);
            throw new Exception("Session not found or expired");
        }

        try
        {
            // Проверяем, нужно ли создавать новую подписку
            bool needsSubscription = !_subscriptionManager.HasAnySubscriptions(userId, sessionId) || 
                                    _subscriptionManager.GetSubscribedInterlocutors(userId, sessionId).Count() == 1;
            
            if (needsSubscription)
            {
                // Сначала отписываемся от текущих обновлений
                session.UnsubscribeFromUpdates();
                
                // Подписываемся на новые обновления
                session.SubscribeToUpdates((sender, update) =>
                {
                    // Приводим IObject к Update
                    if (update is not Update updateBase)
                        return;

                    Message? message = null;

                    // Проверяем различные типы обновлений
                    switch (updateBase)
                    {
                        case UpdateNewMessage updateNewMessage:
                            message = updateNewMessage.message as Message;
                            break;
                        case UpdateEditMessage:
                        case UpdateDeleteMessages:
                            // Пропускаем отредактированные и удаленные сообщения
                            return;
                        default:
                            return;
                    }

                    if (message == null)
                        return;

                    // Получаем ID собеседника из сообщения
                    long msgInterlocutorId = 0;
                    if (message.peer_id is PeerUser peerUser)
                    {
                        msgInterlocutorId = peerUser.user_id;
                    }
                    else
                    {
                        // Не личное сообщение, пропускаем
                        return;
                    }

                    // Проверяем, есть ли активная подписка для этого собеседника
                    if (!_subscriptionManager.HasSubscription(userId, sessionId, msgInterlocutorId))
                        return;
                    
                    var telegramMessage = new TelegramMessageEntity
                    {
                        UserId = userId,
                        SessionId = sessionId,
                        TelegramUserId = session.GetUserId(),
                        TelegramInterlocutorId = msgInterlocutorId,
                        SenderId = message.from_id?.ID ?? 0,
                        MessageTime = message.Date
                    };

                    _messageCollector.EnqueueMessage(telegramMessage);
                });
            }
        }
        catch
        {
            _subscriptionManager.RemoveSubscription(userId, sessionId, interlocutorId);
            throw;
        }
    }

    public async Task UnsubscribeFromUpdatesAsync(string userId, string sessionId, long interlocutorId, CancellationToken cancellationToken)
    {
        _subscriptionManager.RemoveSubscription(userId, sessionId, interlocutorId);

        // Проверяем, остались ли ещё активные подписки для этой сессии
        if (!_subscriptionManager.HasAnySubscriptions(userId, sessionId))
        {
            var session = await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
            if (session == null)
            {
                throw new Exception("Session not found or expired");
            }

            session.UnsubscribeFromUpdates();
        }
    }

    public async Task<List<TelegramContactResponse>> GetSubscribedContactsAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[DEBUG] Getting subscribed contacts for UserId: {userId}, SessionId: {sessionId}");

        // Сначала проверяем наличие активной сессии
        var session = await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
        if (session == null)
        {
            Console.WriteLine("[DEBUG] No active session found");
            return new List<TelegramContactResponse>();
        }

        // Проверяем статус авторизации
        if (session.Status != TelegramLoginStatus.Success)
        {
            Console.WriteLine($"[DEBUG] Session is not authorized. Status: {session.Status}");
            return new List<TelegramContactResponse>();
        }
        
        // Получаем список подписанных ID
        var subscribedIds = _subscriptionManager.GetSubscribedInterlocutors(userId, sessionId);
        if (!subscribedIds.Any())
        {
            Console.WriteLine("[DEBUG] No subscribed IDs found");
            return new List<TelegramContactResponse>();
        }

        Console.WriteLine($"[DEBUG] Found {subscribedIds.Count()} subscribed IDs");
        
        int retryCount = 0;
        const int maxRetries = 2;
        
        while (retryCount <= maxRetries)
        {
            try
            {
                var contacts = await session.GetContactsAsync();
                Console.WriteLine($"[DEBUG] Retrieved {contacts.Count()} contacts");
                
                // Фильтруем контакты по списку подписанных ID
                return contacts.Where(c => subscribedIds.Contains(c.Id)).ToList();
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine($"[DEBUG] Session disposed, attempt {retryCount + 1} of {maxRetries + 1}");
                TelegramClientCache.RemoveSession(userId, sessionId);
                
                // Пытаемся восстановить сессию
                session = await GetOrRestoreSessionAsync(userId, sessionId, cancellationToken);
                if (session == null)
                {
                    Console.WriteLine("[DEBUG] Failed to restore session");
                    return new List<TelegramContactResponse>();
                }
                
                retryCount++;
                if (retryCount > maxRetries)
                {
                    Console.WriteLine("[DEBUG] Max retries reached");
                    return new List<TelegramContactResponse>();
                }
                
                // Небольшая задержка перед повторной попыткой
                await Task.Delay(500, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Unexpected error: {ex.Message}");
                return new List<TelegramContactResponse>();
            }
        }

        return new List<TelegramContactResponse>();
    }

    private string GetCacheKey(string userId, string sessionId) => $"{userId}_{sessionId}";

    private async Task<TelegramLoginStatus> HandleLoginResponseAsync(TelegramSession session, string loginResponse)
    {
        switch (loginResponse)
        {
            case "verification_code":
                await session.SetStatusAsync(TelegramLoginStatus.VerificationCodeRequired);
                break;
            case "password":
                await session.SetStatusAsync(TelegramLoginStatus.TwoFactorRequired);
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
}