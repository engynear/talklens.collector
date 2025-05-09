using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Infrastructure.Configuration;

namespace TalkLens.Collector.Infrastructure.Services.Telegram;

/// <summary>
/// Реализация хранилища сессий Telegram на базе MinIO
/// </summary>
public class MinioTelegramSessionStorage : ITelegramSessionStorage
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioTelegramSessionStorage> _logger;
    private readonly string _bucketName;
    private readonly string _tempDirectory;
    private readonly bool _useRemoteStorage;
    private readonly bool _skipMinioSave;
    
    private const string SessionFilePrefix = "telegram/sessions/";
    private const string UpdatesStatePrefix = "telegram/updates/";

    public MinioTelegramSessionStorage(
        IOptions<StorageOptions> storageOptions, 
        ILogger<MinioTelegramSessionStorage> logger)
    {
        _logger = logger;
        var options = storageOptions.Value;
        
        // Проверяем, нужно ли использовать удаленное хранилище
        _useRemoteStorage = options.UseRemoteStorage;
        // Проверяем, нужно ли пропускать сохранение в MinIO
        _skipMinioSave = options.SkipMinioSave;
        
        var minioOptions = options.Minio;
        var endpoint = minioOptions.Endpoint;
        var accessKey = minioOptions.AccessKey;
        var secretKey = minioOptions.SecretKey;
        var withSSL = minioOptions.WithSSL;
        
        _bucketName = minioOptions.BucketName;
        
        // Создаем клиент MinIO только если используем удаленное хранилище
        if (_useRemoteStorage)
        {
            _minioClient = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey);
                
            if (withSSL)
            {
                _minioClient = _minioClient.WithSSL();
            }
            
            // Инициализация бакета при запуске (асинхронно)
            Task.Run(InitializeBucketAsync);
        }
        
        // Создаем временную директорию для кэширования файлов сессий
        var tempDir = minioOptions.TempDirectory;
        if (!Path.IsPathRooted(tempDir))
        {
            tempDir = Path.Combine(Path.GetTempPath(), tempDir);
        }
        
        _tempDirectory = tempDir;
        
        if (!Directory.Exists(_tempDirectory))
        {
            Directory.CreateDirectory(_tempDirectory);
        }
        
        var statusMessage = "Инициализировано хранилище сессий Telegram";
        if (_useRemoteStorage)
        {
            statusMessage += $" с MinIO: {endpoint}, удаленное хранилище: ВКЛ";
        }
        else
        {
            statusMessage += ", удаленное хранилище: ВЫКЛ";
        }
        
        if (_skipMinioSave)
        {
            statusMessage += ", пропуск сохранения в MinIO: ВКЛ";
        }
        
        logger.LogInformation(statusMessage);
    }
    
    private async Task InitializeBucketAsync()
    {
        if (!_useRemoteStorage)
            return;
            
        try
        {
            // Проверяем, существует ли бакет
            var bucketExistsArgs = new BucketExistsArgs().WithBucket(_bucketName);
            bool found = await _minioClient.BucketExistsAsync(bucketExistsArgs);
            if (!found)
            {
                // Если бакет не существует, создаем его
                var makeBucketArgs = new MakeBucketArgs().WithBucket(_bucketName);
                await _minioClient.MakeBucketAsync(makeBucketArgs);
                _logger.LogInformation("Создан бакет {BucketName} в MinIO", _bucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации бакета MinIO: {ErrorMessage}", ex.Message);
        }
    }
    
    private string GetSessionObjectName(string userId, string sessionId)
    {
        return $"{SessionFilePrefix}{userId}_{sessionId}.session";
    }
    
    private string GetUpdatesStateObjectName(string userId, string sessionId)
    {
        return $"{UpdatesStatePrefix}{userId}_{sessionId}.updates";
    }
    
    private string GetLocalSessionPath(string userId, string sessionId)
    {
        return Path.Combine(_tempDirectory, $"{userId}_{sessionId}.session");
    }
    
    private string GetLocalUpdatesPath(string userId, string sessionId)
    {
        return Path.Combine(_tempDirectory, $"{userId}_{sessionId}.updates");
    }

    /// <inheritdoc />
    public async Task<bool> SessionExistsAsync(string userId, string sessionId)
    {
        // Проверяем существование локального файла
        var localPath = GetLocalSessionPath(userId, sessionId);
        if (File.Exists(localPath))
        {
            return true;
        }
        
        // Если используем удаленное хранилище, проверяем в MinIO
        if (_useRemoteStorage)
        {
            var objectName = GetSessionObjectName(userId, sessionId);
            try
            {
                var statObjectArgs = new StatObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName);
                
                await _minioClient.StatObjectAsync(statObjectArgs);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        return false;
    }

    /// <inheritdoc />
    public async Task<string> GetSessionFilePathAsync(string userId, string sessionId)
    {
        var localPath = GetLocalSessionPath(userId, sessionId);
        
        // Если файл уже существует локально, просто возвращаем путь
        if (File.Exists(localPath))
        {
            return localPath;
        }
        
        // Если используем удаленное хранилище, пытаемся загрузить из MinIO
        if (_useRemoteStorage)
        {
            // Если файл не существует локально, проверяем в MinIO
            var objectName = GetSessionObjectName(userId, sessionId);
            try
            {
                var getObjectArgs = new GetObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithFile(localPath);
                
                await _minioClient.GetObjectAsync(getObjectArgs);
                _logger.LogDebug("Загружен файл сессии Telegram из MinIO: {ObjectName}", objectName);
                return localPath;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Файл сессии не найден в MinIO, будет создан новый: {ErrorMessage}", ex.Message);
            }
        }
        
        // Если файл не существует или не используется удаленное хранилище, создаем пустой локальный файл
        Directory.CreateDirectory(Path.GetDirectoryName(localPath));
        File.Create(localPath).Close();
        return localPath;
    }

    /// <inheritdoc />
    public async Task SaveSessionAsync(string userId, string sessionId, string localFilePath)
    {
        if (!File.Exists(localFilePath))
        {
            _logger.LogWarning("Попытка сохранить несуществующий файл сессии: {FilePath}", localFilePath);
            return;
        }
        
        // Если включен режим пропуска сохранения в MinIO, просто возвращаемся
        if (_skipMinioSave)
        {
            _logger.LogDebug("Пропущено сохранение файла сессии в MinIO (SkipMinioSave=true): {FilePath}", localFilePath);
            return;
        }
        
        // Если не используем удаленное хранилище, просто убеждаемся, что локальный файл существует
        if (!_useRemoteStorage)
        {
            _logger.LogDebug("Файл сессии Telegram сохранен локально: {FilePath}", localFilePath);
            return;
        }
        
        var objectName = GetSessionObjectName(userId, sessionId);
        try
        {
            // Создаем временную копию файла в другой директории
            var tempDir = Path.Combine(Path.GetTempPath(), "TelegramTemp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            
            var tempFilePath = Path.Combine(tempDir, Path.GetFileName(localFilePath));
            
            // Копируем файл сессии во временный каталог с повторными попытками
            bool copySuccess = false;
            Exception lastException = null;
            
            for (int i = 0; i < 5 && !copySuccess; i++)
            {
                try
                {
                    // Иногда файл может быть заблокирован, поэтому ждем немного между попытками
                    if (i > 0)
                    {
                        await Task.Delay(i * 200);
                    }
                    
                    // Простое копирование файла
                    File.Copy(localFilePath, tempFilePath, true);
                    copySuccess = true;
                }
                catch (IOException ex)
                {
                    lastException = ex;
                    _logger.LogWarning("Попытка {Attempt} копирования файла сессии не удалась: {Error}", 
                        i + 1, ex.Message);
                }
            }
            
            if (!copySuccess)
            {
                if (lastException != null)
                {
                    throw new IOException($"Не удалось создать копию файла сессии после 5 попыток", lastException);
                }
                throw new IOException("Не удалось создать копию файла сессии после 5 попыток");
            }
            
            try
            {
                // Загружаем файл в MinIO
                var contentType = "application/octet-stream";
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithFileName(tempFilePath)
                    .WithContentType(contentType);
                    
                await _minioClient.PutObjectAsync(putObjectArgs);
                _logger.LogDebug("Сессия Telegram сохранена в MinIO: {ObjectName}", objectName);
            }
            finally
            {
                // Очищаем временную директорию
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                    
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Не удалось очистить временные файлы: {Error}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении сессии Telegram в MinIO: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteSessionAsync(string userId, string sessionId)
    {
        // Удаляем локальный файл, если он существует
        var localPath = GetLocalSessionPath(userId, sessionId);
        if (File.Exists(localPath))
        {
            File.Delete(localPath);
            _logger.LogDebug("Удален локальный файл сессии: {FilePath}", localPath);
        }
        
        // Если используем удаленное хранилище, удаляем и из MinIO
        if (_useRemoteStorage)
        {
            var objectName = GetSessionObjectName(userId, sessionId);
            try
            {
                var removeObjectArgs = new RemoveObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName);
                    
                await _minioClient.RemoveObjectAsync(removeObjectArgs);
                _logger.LogDebug("Удалена сессия Telegram из MinIO: {ObjectName}", objectName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении сессии Telegram из MinIO: {ErrorMessage}", ex.Message);
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdatesStateExistsAsync(string userId, string sessionId)
    {
        // Проверяем существование локального файла
        var localPath = GetLocalUpdatesPath(userId, sessionId);
        if (File.Exists(localPath))
        {
            return true;
        }
        
        // Если используем удаленное хранилище, проверяем в MinIO
        if (_useRemoteStorage)
        {
            var objectName = GetUpdatesStateObjectName(userId, sessionId);
            try
            {
                var statObjectArgs = new StatObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName);
                
                await _minioClient.StatObjectAsync(statObjectArgs);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        return false;
    }

    /// <inheritdoc />
    public async Task<string> GetUpdatesStateFilePathAsync(string userId, string sessionId)
    {
        var localPath = GetLocalUpdatesPath(userId, sessionId);
        
        // Если файл уже существует локально, просто возвращаем путь
        if (File.Exists(localPath))
        {
            return localPath;
        }
        
        // Если используем удаленное хранилище, пытаемся загрузить из MinIO
        if (_useRemoteStorage)
        {
            // Если файл не существует локально, проверяем в MinIO
            var objectName = GetUpdatesStateObjectName(userId, sessionId);
            try
            {
                var getObjectArgs = new GetObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithFile(localPath);
                
                await _minioClient.GetObjectAsync(getObjectArgs);
                _logger.LogDebug("Загружен файл состояния обновлений из MinIO: {ObjectName}", objectName);
                return localPath;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Файл состояния обновлений не найден в MinIO, будет создан новый: {ErrorMessage}", ex.Message);
            }
        }
        
        // Если файл не существует или не используется удаленное хранилище, создаем пустой локальный файл
        Directory.CreateDirectory(Path.GetDirectoryName(localPath));
        File.Create(localPath).Close();
        return localPath;
    }

    /// <inheritdoc />
    public async Task SaveUpdatesStateAsync(string userId, string sessionId, string localFilePath)
    {
        if (!File.Exists(localFilePath))
        {
            _logger.LogWarning("Попытка сохранить несуществующий файл состояния обновлений: {FilePath}", localFilePath);
            return;
        }
        
        // Если включен режим пропуска сохранения в MinIO, просто возвращаемся
        if (_skipMinioSave)
        {
            _logger.LogDebug("Пропущено сохранение файла состояния обновлений в MinIO (SkipMinioSave=true): {FilePath}", localFilePath);
            return;
        }
        
        // Если не используем удаленное хранилище, просто убеждаемся, что локальный файл существует
        if (!_useRemoteStorage)
        {
            _logger.LogDebug("Файл состояния обновлений Telegram сохранен локально: {FilePath}", localFilePath);
            return;
        }
        
        var objectName = GetUpdatesStateObjectName(userId, sessionId);
        try
        {
            // Создаем временную копию файла в другой директории
            var tempDir = Path.Combine(Path.GetTempPath(), "TelegramTemp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            
            var tempFilePath = Path.Combine(tempDir, Path.GetFileName(localFilePath));
            
            // Копируем файл состояния обновлений во временный каталог с повторными попытками
            bool copySuccess = false;
            Exception lastException = null;
            
            for (int i = 0; i < 5 && !copySuccess; i++)
            {
                try
                {
                    // Иногда файл может быть заблокирован, поэтому ждем немного между попытками
                    if (i > 0)
                    {
                        await Task.Delay(i * 200);
                    }
                    
                    // Простое копирование файла
                    File.Copy(localFilePath, tempFilePath, true);
                    copySuccess = true;
                }
                catch (IOException ex)
                {
                    lastException = ex;
                    _logger.LogWarning("Попытка {Attempt} копирования файла состояния обновлений не удалась: {Error}", 
                        i + 1, ex.Message);
                }
            }
            
            if (!copySuccess)
            {
                if (lastException != null)
                {
                    throw new IOException($"Не удалось создать копию файла состояния обновлений после 5 попыток", lastException);
                }
                throw new IOException("Не удалось создать копию файла состояния обновлений после 5 попыток");
            }
            
            try
            {
                // Загружаем файл в MinIO
                var contentType = "application/octet-stream";
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithFileName(tempFilePath)
                    .WithContentType(contentType);
                    
                await _minioClient.PutObjectAsync(putObjectArgs);
                _logger.LogDebug("Файл состояния обновлений сохранен в MinIO: {ObjectName}", objectName);
            }
            finally
            {
                // Очищаем временную директорию
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                    
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Не удалось очистить временные файлы: {Error}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении файла состояния обновлений в MinIO: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteUpdatesStateAsync(string userId, string sessionId)
    {
        // Удаляем локальный файл, если он существует
        var localPath = GetLocalUpdatesPath(userId, sessionId);
        if (File.Exists(localPath))
        {
            File.Delete(localPath);
            _logger.LogDebug("Удален локальный файл состояния обновлений: {FilePath}", localPath);
        }
        
        // Если используем удаленное хранилище, удаляем и из MinIO
        if (_useRemoteStorage)
        {
            var objectName = GetUpdatesStateObjectName(userId, sessionId);
            try
            {
                var removeObjectArgs = new RemoveObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName);
                    
                await _minioClient.RemoveObjectAsync(removeObjectArgs);
                _logger.LogDebug("Удален файл состояния обновлений из MinIO: {ObjectName}", objectName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении файла состояния обновлений из MinIO: {ErrorMessage}", ex.Message);
            }
        }
    }

    /// <inheritdoc />
    public async Task CleanupLocalCacheAsync(string userId, string sessionId)
    {
        try
        {
            var sessionPath = GetLocalSessionPath(userId, sessionId);
            var updatesPath = GetLocalUpdatesPath(userId, sessionId);
            
            if (File.Exists(sessionPath))
            {
                File.Delete(sessionPath);
                _logger.LogDebug("Удален кэшированный файл сессии: {FilePath}", sessionPath);
            }
            
            if (File.Exists(updatesPath))
            {
                File.Delete(updatesPath);
                _logger.LogDebug("Удален кэшированный файл состояния обновлений: {FilePath}", updatesPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке локального кэша: {ErrorMessage}", ex.Message);
        }
    }
} 