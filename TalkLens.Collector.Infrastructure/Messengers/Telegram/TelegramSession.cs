using TalkLens.Collector.Domain.Enums.Telegram;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Services.Telegram;
using WTelegram;
using TL;
using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace TalkLens.Collector.Infrastructure.Messengers.Telegram;

public class TelegramSession : IDisposable
{
    private readonly string _userId;
    private readonly string _sessionId;
    private readonly string _sessionFilePath;
    private readonly string _updatesFilePath;
    
    private Client _client;
    private TelegramLoginStatus _status;
    private bool _disposed;
    private User? _user;
    private UpdateManager? _updateManager;
    
    private string? _phone = null;
    private string? _verificationCode = null;
    private string? _twoFactorPassword = null;
    private EventHandler<IObject>? _updateHandler;

    // Добавляем поля для лимитера и кэша
    private readonly TelegramRateLimiter? _rateLimiter;
    private readonly TelegramApiCache? _apiCache;

    public TelegramLoginStatus Status => _status;
    public string? PhoneNumber => _phone;

    public void SubscribeToUpdates(EventHandler<IObject> handler)
    {
        UnsubscribeFromUpdates();
        _updateHandler = handler;
        
        _updateManager = _client.WithUpdateManager(
            onUpdate: update => 
            {
                if (_updateHandler != null)
                {
                    _updateHandler.Invoke(this, update);
                }
                return Task.CompletedTask;
            },
            statePath: _updatesFilePath
        );
    }

    public void UnsubscribeFromUpdates()
    {
        if (_updateManager != null)
        {
            // UpdateManager не имеет метода Dispose, просто обнуляем ссылку
            // чтобы позволить GC собрать объект
            _updateManager = null;
        }
        _updateHandler = null;
    }

    public long GetUserId()
    {
        return _user?.id ?? 0;
    }

    /// <summary>
    /// Создает новую сессию Telegram без лимитера и кэша (для обратной совместимости)
    /// </summary>
    public TelegramSession(string userId, string sessionId, string sessionFilePath, string updatesFilePath, string? phone = null)
        : this(userId, sessionId, sessionFilePath, updatesFilePath, null, null, phone)
    {
    }

    /// <summary>
    /// Создает новую сессию Telegram с лимитером и кэшем
    /// </summary>
    public TelegramSession(
        string userId, 
        string sessionId, 
        string sessionFilePath, 
        string updatesFilePath, 
        TelegramRateLimiter? rateLimiter = null,
        TelegramApiCache? apiCache = null,
        string? phone = null)
    {
        _userId = userId;
        _sessionId = sessionId;
        _sessionFilePath = sessionFilePath;
        _updatesFilePath = updatesFilePath;
        _phone = phone;
        _rateLimiter = rateLimiter;
        _apiCache = apiCache;
        
        _status = File.Exists(sessionFilePath) ? TelegramLoginStatus.Success : TelegramLoginStatus.Pending;
        _disposed = false;

        InitializeClient();
    }

    private void InitializeClient()
    {
        // Проверяем, существует ли директория для файла сессии
        var directory = Path.GetDirectoryName(_sessionFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Проверяем доступность файла сессии перед созданием клиента
        if (File.Exists(_sessionFilePath))
        {
            try
            {
                // Проверяем, не заблокирован ли файл
                using var fs = new FileStream(_sessionFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                // Файл доступен, закрываем его
                fs.Close();
            }
            catch (IOException)
            {
                // Файл заблокирован или недоступен, удаляем его
                try
                {
                    File.Delete(_sessionFilePath);
                }
                catch
                {
                    // Игнорируем ошибки при удалении
                }
            }
        }

        _client = new Client(what =>
        {
            return what switch
            {
                "api_id" => "23252333",
                "api_hash" => "2bfce46015419239292eeaed12562231",
                "phone_number" => _phone,
                "verification_code" => _verificationCode,
                "password" => _twoFactorPassword,
                "session_pathname" => _sessionFilePath,
                _ => null
            };
        });
    }

    public async Task<string> StartLoginAsync(string phone)
    {
        ThrowIfDisposed();
        _phone = phone;
        try 
        {
            return await _client.Login(_phone);
        }
        catch (RpcException ex)
        {
            throw new Exception($"Telegram error: {ex.Message}");
        }
    }

    public async Task<string> SubmitVerificationCodeAsync(string code)
    {
        ThrowIfDisposed();
        _verificationCode = code;
        try 
        {
            var response = await _client.Login(_phone);
            if (string.IsNullOrEmpty(response))
            {
                _user = _client.User;
            }
            return response;
        }
        catch (RpcException ex) when (ex.Message.Contains("PHONE_CODE_INVALID"))
        {
            throw new Exception("Неверный код подтверждения");
        }
        catch (RpcException ex)
        {
            throw new Exception($"Telegram error: {ex.Message}");
        }
    }

    public async Task<string> SubmitTwoFactorPasswordAsync(string password)
    {
        ThrowIfDisposed();
        _twoFactorPassword = password;
        try 
        {
            var response = await _client.Login(_phone);
            if (string.IsNullOrEmpty(response))
            {
                _user = _client.User;
            }
            return response;
        }
        catch (RpcException ex) when (ex.Message.Contains("PASSWORD_HASH_INVALID"))
        {
            throw new Exception("Неверный пароль двухфакторной аутентификации");
        }
        catch (RpcException ex)
        {
            throw new Exception($"Telegram error: {ex.Message}");
        }
    }

    public async Task<bool> ValidateSessionAsync()
    {
        try
        {
            if (_client.User != null)
                return true;

            await _client.Login(_phone);
            _user = _client.User;
            return _user != null;
        }
        catch
        {
            return false;
        }
    }

    public Task SetStatusAsync(TelegramLoginStatus status)
    {
        ThrowIfDisposed();
        _status = status;
        return Task.CompletedTask;
    }

    public void DeleteSessionFile()
    {
        if (File.Exists(_sessionFilePath))
        {
            try
            {
                File.Delete(_sessionFilePath);
            }
            catch
            {
                // Игнорируем ошибки при удалении файла
            }
        }
    }

    private void DeleteUpdateStateFile()
    {
        if (File.Exists(_updatesFilePath))
        {
            try
            {
                File.Delete(_updatesFilePath);
            }
            catch
            {
                // Игнорируем ошибки при удалении файла
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TelegramSession));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            UnsubscribeFromUpdates();
            _client?.Dispose();
        }
        catch
        {
            // Игнорируем ошибки при освобождении клиента
        }

        if (_status != TelegramLoginStatus.Success)
        {
            DeleteSessionFile();
            DeleteUpdateStateFile();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public bool IsSessionFileValid()
    {
        try
        {
            return File.Exists(_sessionFilePath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Выполняет API запрос с учетом рейт-лимита и кэширования
    /// </summary>
    /// <typeparam name="T">Тип результата</typeparam>
    /// <param name="methodName">Имя метода API</param>
    /// <param name="factory">Фабрика для выполнения запроса</param>
    /// <param name="forceRefresh">Принудительное обновление кэша</param>
    /// <param name="args">Аргументы для кэширования</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат выполнения запроса</returns>
    private async Task<T> ExecuteApiMethod<T>(
        string methodName, 
        Func<Task<T>> factory, 
        bool forceRefresh = false, 
        object[]? args = null, 
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        // Функция, которая выполняет метод с учетом рейт-лимита
        async Task<T> ExecuteWithRateLimit()
        {
            if (_rateLimiter != null)
            {
                // Используем рейт-лимитер для ограничения запросов
                return await _rateLimiter.ExecuteWithRateLimitAsync(methodName, factory, cancellationToken);
            }
            else
            {
                // Если рейт-лимитер не предоставлен, просто выполняем метод
                return await factory();
            }
        }
        
        // Решаем, применять ли кэширование
        if (_apiCache != null && args != null)
        {
            // Используем кэш для запроса
            return await _apiCache.GetOrCreateAsync(
                methodName,
                ExecuteWithRateLimit,
                forceRefresh,
                args);
        }
        else
        {
            // Если кэш не предоставлен или аргументы отсутствуют, просто выполняем запрос
            return await ExecuteWithRateLimit();
        }
    }

    /// <summary>
    /// Получает список всех контактов пользователя
    /// </summary>
    /// <param name="forceRefresh">Принудительное обновление кэша</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список контактов</returns>
    public async Task<List<TelegramContactResponse>> GetContactsAsync(
        bool forceRefresh = false, 
        CancellationToken cancellationToken = default)
    {
        return await ExecuteApiMethod(
            "Messages_GetAllDialogs",
            async () =>
            {
                var dialogs = await _client.Messages_GetAllDialogs();
                var contacts = new List<TelegramContactResponse>();
                
                foreach (var dialog in dialogs.dialogs)
                {
                    // Пропускаем все, кроме личных чатов
                    if (dialog.Peer is not TL.PeerUser)
                        continue;

                    var userId = ((TL.PeerUser)dialog.Peer).user_id;
                    var user = dialogs.users[userId];
                    
                    // Пропускаем ботов и удаленных пользователей
                    if (user.flags.HasFlag(TL.User.Flags.bot) || user.flags.HasFlag(TL.User.Flags.deleted))
                        continue;

                    contacts.Add(new TelegramContactResponse
                    {
                        Id = userId,
                        FirstName = user.first_name ?? string.Empty,
                        LastName = user.last_name,
                        Username = user.username,
                        Phone = user.phone,
                        HasPhoto = user.photo != null,
                        LastSeen = user.status != null ? user.status.ToString() : null
                    });
                }

                return contacts;
            },
            forceRefresh,
            new object[] { "all" },
            cancellationToken);
    }

    /// <summary>
    /// Получает список последних 10 контактов с сортировкой по дате последнего сообщения
    /// </summary>
    /// <param name="forceRefresh">Принудительное обновление кэша</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список последних контактов</returns>
    public async Task<List<TelegramContactResponse>> GetRecentContactsAsync(
        bool forceRefresh = false, 
        CancellationToken cancellationToken = default)
    {
        return await ExecuteApiMethod(
            "GetRecentContacts",
            async () =>
            {
                var dialogs = await _client.Messages_GetAllDialogs();
                var contacts = new List<TelegramContactWithLastMessage>();
                
                foreach (var dialog in dialogs.dialogs)
                {
                    // Пропускаем все, кроме личных чатов
                    if (dialog.Peer is not TL.PeerUser)
                        continue;

                    var userId = ((TL.PeerUser)dialog.Peer).user_id;
                    var user = dialogs.users[userId];
                    
                    // Пропускаем ботов и удаленных пользователей
                    if (user.flags.HasFlag(TL.User.Flags.bot) || user.flags.HasFlag(TL.User.Flags.deleted))
                        continue;

                    // Получаем время последнего сообщения
                    DateTime lastMessageTime = DateTime.MinValue;
                    if (dialog is TL.Dialog tlDialog && tlDialog.top_message != 0)
                    {
                        var message = dialogs.messages.FirstOrDefault(m => m.ID == tlDialog.top_message);
                        if (message != null && message is Message msg)
                        {
                            lastMessageTime = msg.Date;
                        }
                    }

                    contacts.Add(new TelegramContactWithLastMessage
                    {
                        Contact = new TelegramContactResponse
                        {
                            Id = userId,
                            FirstName = user.first_name ?? string.Empty,
                            LastName = user.last_name,
                            Username = user.username,
                            Phone = user.phone,
                            HasPhoto = user.photo != null,
                            LastSeen = user.status != null ? user.status.ToString() : null
                        },
                        LastMessageTime = lastMessageTime
                    });
                }

                // Сортируем по времени последнего сообщения (самые новые вверху) и берем 10 элементов
                return contacts
                    .OrderByDescending(c => c.LastMessageTime)
                    .Take(10)
                    .Select(c => c.Contact)
                    .ToList();
            },
            forceRefresh,
            new object[] { "recent" },
            cancellationToken);
    }

    // Вспомогательный класс для сортировки контактов
    private class TelegramContactWithLastMessage
    {
        public TelegramContactResponse Contact { get; set; }
        public DateTime LastMessageTime { get; set; }
    }

    /// <summary>
    /// Получает путь к файлу сессии
    /// </summary>
    public string GetSessionFilePath() => _sessionFilePath;
    
    /// <summary>
    /// Получает путь к файлу состояния обновлений
    /// </summary>
    public string GetUpdatesFilePath() => _updatesFilePath;
    
    /// <summary>
    /// Освобождает ресурсы клиента Telegram
    /// Метод для обратной совместимости, вызывает Dispose()
    /// </summary>
    public void ReleaseClient()
    {
        Dispose();
    }
}