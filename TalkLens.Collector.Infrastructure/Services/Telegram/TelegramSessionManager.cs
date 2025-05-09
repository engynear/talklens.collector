using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Infrastructure.Configuration;
using TalkLens.Collector.Infrastructure.Messengers.Telegram;
using WTelegram;

namespace TalkLens.Collector.Infrastructure.Services.Telegram;

/// <summary>
/// Менеджер сессий Telegram - управляет хранением и загрузкой сессий
/// </summary>
public class TelegramSessionManager
{
    private readonly ITelegramSessionStorage _sessionStorage;
    private readonly ILogger<TelegramSessionManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly TelegramRateLimiter _rateLimiter;
    private readonly TelegramApiCache _apiCache;
    private readonly TelegramOptions _telegramOptions;
    
    private readonly string _apiId;
    private readonly string _apiHash;

    public TelegramSessionManager(
        ITelegramSessionStorage sessionStorage, 
        IConfiguration configuration,
        TelegramRateLimiter rateLimiter,
        TelegramApiCache apiCache,
        IOptions<TelegramOptions> telegramOptions,
        ILogger<TelegramSessionManager> logger)
    {
        _sessionStorage = sessionStorage;
        _configuration = configuration;
        _rateLimiter = rateLimiter;
        _apiCache = apiCache;
        _telegramOptions = telegramOptions.Value;
        _logger = logger;
        
        // Загружаем настройки API из конфигурации
        _apiId = _telegramOptions.ApiId.ToString();
        _apiHash = _telegramOptions.ApiHash;
        
        _logger.LogInformation("TelegramSessionManager инициализирован с API ID: {ApiId}", _apiId);
    }

    /// <summary>
    /// Создает новый WTelegram.Client с предварительно загруженной сессией из хранилища
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <param name="phone">Номер телефона (используется только для новой сессии)</param>
    /// <returns>Клиент Telegram</returns>
    public async Task<Client> CreateClientAsync(string userId, string sessionId, string phone = null)
    {
        try
        {
            // Получаем путь к файлу сессии из хранилища
            var sessionPath = await _sessionStorage.GetSessionFilePathAsync(userId, sessionId);
            
            // Создаем новый клиент
            var client = new Client(what =>
            {
                return what switch
                {
                    "api_id" => _apiId,
                    "api_hash" => _apiHash,
                    "phone_number" => phone,
                    "verification_code" => null, // Будет запрошен позже
                    "password" => null, // Будет запрошен позже
                    "session_pathname" => sessionPath,
                    _ => null
                };
            });
            
            _logger.LogDebug("Создан клиент Telegram для пользователя {UserId}, сессия {SessionId}", userId, sessionId);
            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании клиента Telegram: {ErrorMessage}", ex.Message);
            throw;
        }
    }
    
    /// <summary>
    /// Создает экземпляр TelegramSession с предварительно загруженной сессией из хранилища
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <param name="phone">Номер телефона (может быть null)</param>
    /// <returns>Экземпляр TelegramSession</returns>
    public async Task<TelegramSession> CreateSessionAsync(string userId, string sessionId, string phone = null)
    {
        try
        {
            // Получаем пути к файлам сессии и состояния обновлений
            var sessionFilePath = await _sessionStorage.GetSessionFilePathAsync(userId, sessionId);
            var updatesFilePath = await _sessionStorage.GetUpdatesStateFilePathAsync(userId, sessionId);
            
            // Создаем новую сессию с поддержкой рейт-лимитера и кэширования
            var session = new TelegramSession(
                userId, 
                sessionId, 
                sessionFilePath, 
                updatesFilePath, 
                _rateLimiter,
                _apiCache,
                phone,
                _apiId,
                _apiHash);
                
            _logger.LogDebug("Создана сессия Telegram для пользователя {UserId}, сессия {SessionId}", userId, sessionId);
            
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании сессии Telegram: {ErrorMessage}", ex.Message);
            throw;
        }
    }
    
    /// <summary>
    /// Сохраняет сессию в хранилище
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <returns>True если сохранение успешно, иначе False</returns>
    public async Task<bool> SaveSessionAsync(string userId, string sessionId)
    {
        try
        {
            // Получаем пути к локальным файлам
            var sessionPath = await _sessionStorage.GetSessionFilePathAsync(userId, sessionId);
            var updatesPath = await _sessionStorage.GetUpdatesStateFilePathAsync(userId, sessionId);
            
            // Запускаем задачу сохранения в отдельном потоке
            var sessionSaveTask = Task.Run(async () => {
                // Пытаемся сохранить файл сессии с повторными попытками
                int retries = 0;
                const int maxRetries = 5;
                bool sessionSaved = false;
                
                while (!sessionSaved && retries < maxRetries)
                {
                    try
                    {
                        await _sessionStorage.SaveSessionAsync(userId, sessionId, sessionPath);
                        sessionSaved = true;
                        _logger.LogDebug("Файл сессии Telegram сохранен в хранилище после {Attempts} попыток", retries + 1);
                    }
                    catch (IOException ex)
                    {
                        retries++;
                        _logger.LogWarning("Попытка {Attempt} сохранения файла сессии не удалась: {Error}. Повторная попытка через {Delay}мс", 
                            retries, ex.Message, retries * 200);
                        await Task.Delay(retries * 200);  // Увеличиваем задержку с каждой попыткой
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Неожиданная ошибка при сохранении файла сессии: {Error}", ex.Message);
                        return false;
                    }
                }
                
                return sessionSaved;
            });
            
            // Запускаем задачу сохранения файла состояния обновлений в отдельном потоке
            var updatesStateTask = Task.Run(async () => {
                // Пытаемся сохранить файл состояния обновлений с повторными попытками
                int retries = 0;
                const int maxRetries = 5;
                bool updatesSaved = false;
                
                while (!updatesSaved && retries < maxRetries)
                {
                    try
                    {
                        await _sessionStorage.SaveUpdatesStateAsync(userId, sessionId, updatesPath);
                        updatesSaved = true;
                        _logger.LogDebug("Файл состояния обновлений сохранен в хранилище после {Attempts} попыток", retries + 1);
                    }
                    catch (IOException ex)
                    {
                        retries++;
                        _logger.LogWarning("Попытка {Attempt} сохранения файла состояния обновлений не удалась: {Error}. Повторная попытка через {Delay}мс", 
                            retries, ex.Message, retries * 200);
                        await Task.Delay(retries * 200);  // Увеличиваем задержку с каждой попыткой
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Неожиданная ошибка при сохранении файла состояния обновлений: {Error}", ex.Message);
                        return false;
                    }
                }
                
                return updatesSaved;
            });
            
            // Ожидаем завершения обеих задач с таймаутом 30 секунд
            var combinedTask = Task.WhenAll(sessionSaveTask, updatesStateTask);
            if (await Task.WhenAny(combinedTask, Task.Delay(TimeSpan.FromSeconds(30))) == combinedTask)
            {
                bool[] results = await combinedTask;
                if (results.All(r => r)) // Проверяем, что оба файла были успешно сохранены
                {
                    _logger.LogDebug("Сессия Telegram сохранена в хранилище для пользователя {UserId}, сессия {SessionId}", userId, sessionId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Не удалось сохранить все файлы сессии Telegram");
                    return false;
                }
            }
            else
            {
                _logger.LogError("Превышено время ожидания при сохранении сессии Telegram");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении сессии Telegram: {ErrorMessage}", ex.Message);
            return false;
        }
    }
    
    /// <summary>
    /// Удаляет сессию из хранилища
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <returns>True если удаление успешно, иначе False</returns>
    public async Task<bool> DeleteSessionAsync(string userId, string sessionId)
    {
        try
        {
            // Удаляем файлы из хранилища
            await _sessionStorage.DeleteSessionAsync(userId, sessionId);
            await _sessionStorage.DeleteUpdatesStateAsync(userId, sessionId);
            
            // Очищаем локальный кэш
            await _sessionStorage.CleanupLocalCacheAsync(userId, sessionId);
            
            _logger.LogDebug("Сессия Telegram удалена из хранилища для пользователя {UserId}, сессия {SessionId}", userId, sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении сессии Telegram: {ErrorMessage}", ex.Message);
            return false;
        }
    }
    
    /// <summary>
    /// Проверяет существование сессии в хранилище
    /// </summary>
    /// <param name="userId">ID пользователя</param>
    /// <param name="sessionId">ID сессии</param>
    /// <returns>True если сессия существует, иначе False</returns>
    public async Task<bool> SessionExistsAsync(string userId, string sessionId)
    {
        return await _sessionStorage.SessionExistsAsync(userId, sessionId);
    }
} 