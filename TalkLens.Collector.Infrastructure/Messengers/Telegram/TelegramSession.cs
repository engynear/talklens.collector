using TalkLens.Collector.Domain.Enums.Telegram;
using WTelegram;
using TL;
using System.IO;
using TalkLens.Collector.Domain.Models.Telegram;

namespace TalkLens.Collector.Infrastructure.Messengers.Telegram;

public class TelegramSession : IDisposable
{
    private readonly string _userId;
    private readonly string _sessionId;
    
    private Client _client;
    private TelegramLoginStatus _status;
    private bool _disposed;
    private User? _user;
    
    private string? _phone = null;
    private string? _verificationCode = null;
    private string? _twoFactorPassword = null;

    public TelegramLoginStatus Status => _status;
    public string? PhoneNumber => _phone;
    private string SessionFilePath => $"{_userId}_{_sessionId}.session";

    public TelegramSession(string userId, string sessionId)
    {
        _userId = userId;
        _sessionId = sessionId;
        _status = TelegramLoginStatus.Pending;
        _disposed = false;

        InitializeClient();
    }

    public TelegramSession(string userId, string sessionId, string phone)
    {
        _userId = userId;
        _sessionId = sessionId;
        _phone = phone;
        _status = File.Exists(SessionFilePath) ? TelegramLoginStatus.Success : TelegramLoginStatus.Pending;
        _disposed = false;

        InitializeClient();
    }

    private void InitializeClient()
    {
        if (File.Exists(SessionFilePath))
        {
            try
            {
                // Проверяем, не заблокирован ли файл
                using var fs = new FileStream(SessionFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                // Файл доступен, закрываем его
                fs.Close();
            }
            catch (IOException)
            {
                // Файл заблокирован или недоступен, удаляем его
                try
                {
                    File.Delete(SessionFilePath);
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
                "session_pathname" => SessionFilePath,
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
        if (File.Exists(SessionFilePath))
        {
            try
            {
                File.Delete(SessionFilePath);
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
            _client?.Dispose();
        }
        catch
        {
            // Игнорируем ошибки при освобождении клиента
        }

        if (_status != TelegramLoginStatus.Success)
        {
            DeleteSessionFile();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public bool IsSessionFileValid()
    {
        try
        {
            return File.Exists(SessionFilePath);
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<TelegramContactResponse>> GetContactsAsync()
    {
        ThrowIfDisposed();
        
        try
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
                    LastName = user.last_name
                });
            }

            return contacts;
        }
        catch (RpcException ex)
        {
            throw new Exception($"Telegram error: {ex.Message}");
        }
    }
}