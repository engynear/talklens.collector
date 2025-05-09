using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TalkLens.Collector.Infrastructure.Configuration;
using TalkLens.Collector.Infrastructure.Storage.Abstractions;

namespace TalkLens.Collector.Infrastructure.Storage.Redis
{
    /// <summary>
    /// Реализация хранилища сессий Telegram с использованием Redis
    /// </summary>
    public class RedisTelegramSessionStorage : ITelegramSessionStorage
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly ILogger<RedisTelegramSessionStorage> _logger;
        private readonly RedisOptions _options;

        /// <summary>
        /// Конструктор хранилища сессий Telegram в Redis
        /// </summary>
        public RedisTelegramSessionStorage(
            IConnectionMultiplexer redis,
            IOptions<TelegramOptions> options,
            ILogger<RedisTelegramSessionStorage> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _options = options?.Value?.Redis ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _database = _redis.GetDatabase();
        }

        /// <summary>
        /// Проверяет существование сессии по идентификатору телефона
        /// </summary>
        public async Task<bool> ExistsAsync(string phoneNumber)
        {
            try
            {
                string key = GetSessionKey(phoneNumber);
                bool exists = await _database.KeyExistsAsync(key);
                
                _logger.LogDebug("Проверка существования сессии для {PhoneNumber}: {Exists}", phoneNumber, exists);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке существования сессии для {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        /// <summary>
        /// Получает сессию Telegram по идентификатору телефона и сохраняет её во временный файл
        /// </summary>
        public async Task<string> GetSessionFilePathAsync(string phoneNumber)
        {
            try
            {
                string key = GetSessionKey(phoneNumber);
                byte[] sessionData = await _database.StringGetAsync(key);
                
                if (sessionData == null || sessionData.Length == 0)
                {
                    _logger.LogWarning("Сессия для номера {PhoneNumber} не найдена в Redis", phoneNumber);
                    return null;
                }

                string localCachePath = Path.Combine(Directory.GetCurrentDirectory(), "sessions");
                Directory.CreateDirectory(localCachePath);
                
                string filePath = Path.Combine(localCachePath, $"{phoneNumber}.session");
                await File.WriteAllBytesAsync(filePath, sessionData);
                
                _logger.LogDebug("Сессия для {PhoneNumber} получена из Redis и сохранена в {FilePath}", phoneNumber, filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении сессии для {PhoneNumber}", phoneNumber);
                return null;
            }
        }

        /// <summary>
        /// Сохраняет сессию Telegram из файла в Redis
        /// </summary>
        public async Task SaveSessionAsync(string phoneNumber, string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Файл сессии {FilePath} не существует", filePath);
                    return;
                }

                byte[] sessionData = await File.ReadAllBytesAsync(filePath);
                string key = GetSessionKey(phoneNumber);
                
                // Сохраняем данные сессии в Redis с указанным временем жизни
                await _database.StringSetAsync(
                    key, 
                    sessionData, 
                    expiry: TimeSpan.FromSeconds(_options.ExpiryTimeSeconds)
                );
                
                _logger.LogDebug("Сессия для {PhoneNumber} сохранена в Redis", phoneNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении сессии для {PhoneNumber}", phoneNumber);
                throw;
            }
        }

        /// <summary>
        /// Удаляет сессию Telegram по идентификатору телефона
        /// </summary>
        public async Task DeleteSessionAsync(string phoneNumber)
        {
            try
            {
                string key = GetSessionKey(phoneNumber);
                bool deleted = await _database.KeyDeleteAsync(key);
                
                _logger.LogDebug("Удаление сессии для {PhoneNumber}: {Deleted}", phoneNumber, deleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении сессии для {PhoneNumber}", phoneNumber);
                throw;
            }
        }

        /// <summary>
        /// Формирует ключ для хранения сессии в Redis
        /// </summary>
        private string GetSessionKey(string phoneNumber)
        {
            return $"{_options.KeyPrefix}{phoneNumber}";
        }
    }
} 