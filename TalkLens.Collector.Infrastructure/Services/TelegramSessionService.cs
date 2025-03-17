using Microsoft.Extensions.Caching.Memory;
using TalkLens.Collector.Domain.Enums.Telegram;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Messengers.Telegram;

namespace TalkLens.Collector.Infrastructure.Services;

public class TelegramSessionService : ITelegramSessionService
{
    private readonly IMemoryCache _cache;
    private readonly ITelegramSessionRepository _sessionRepository;
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(3);

    public TelegramSessionService(IMemoryCache cache, ITelegramSessionRepository sessionRepository)
    {
        _cache = cache;
        _sessionRepository = sessionRepository;
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
            catch 
            {
                Console.WriteLine("[DEBUG] Error validating global session, removing");
                TelegramClientCache.RemoveSession(userId, sessionId);
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
            catch 
            {
                Console.WriteLine("[DEBUG] Error checking temporary session file");
                tempSession.Dispose();
                _cache.Remove(cacheKey);
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
            catch 
            {
                Console.WriteLine("[DEBUG] Error validating new session from DB");
                await _sessionRepository.UpdateSessionStatusAsync(userId, sessionId, false, cancellationToken);
                session.Dispose();
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