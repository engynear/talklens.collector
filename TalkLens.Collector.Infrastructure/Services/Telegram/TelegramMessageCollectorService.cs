using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Configuration;
using TalkLens.Collector.Infrastructure.Database;

namespace TalkLens.Collector.Infrastructure.Services.Telegram;

public class TelegramMessageCollectorService : BackgroundService
{
    private readonly ILogger<TelegramMessageCollectorService> _logger;
    private readonly ITelegramMessageRepository _messageRepository;
    private readonly IKafkaMessageService _kafkaService;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _redisDb;
    private readonly string _queueKeyPrefix;
    private readonly string _processedKeysPrefix;
    private readonly TimeSpan _processingInterval;
    private readonly int _maxQueueSizePerSession;

    public TelegramMessageCollectorService(
        ILogger<TelegramMessageCollectorService> logger,
        ITelegramMessageRepository messageRepository,
        IKafkaMessageService kafkaService,
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> redisOptions,
        IOptions<MessageCollectorOptions> messageCollectorOptions)
    {
        _logger = logger;
        _messageRepository = messageRepository;
        _kafkaService = kafkaService;
        _redis = redis;
        _redisDb = redis.GetDatabase();
        
        // Загружаем настройки из опций
        var redisConfig = redisOptions.Value;
        var messageCollectorConfig = messageCollectorOptions.Value;
        
        _queueKeyPrefix = redisConfig.MessageQueuePrefix ?? "telegram:message_queue:";
        _processedKeysPrefix = "telegram:processed_messages:";
        _processingInterval = TimeSpan.FromMinutes(messageCollectorConfig.SaveIntervalMinutes);
        _maxQueueSizePerSession = messageCollectorConfig.MaxQueueSizePerSession;
        
        _logger.LogInformation("TelegramMessageCollectorService инициализирован. Интервал сохранения: {SaveInterval}, " +
                            "максимальный размер очереди: {MaxQueueSize}", _processingInterval, _maxQueueSizePerSession);
    }

    /// <summary>
    /// Добавляет сообщение в очередь для последующей обработки
    /// </summary>
    /// <param name="message">Сообщение для добавления</param>
    public async Task EnqueueMessageAsync(TelegramMessageEntity message)
    {
        var key = $"{message.UserId}_{message.SessionId}";
        var queueKey = $"{_queueKeyPrefix}{key}";
        
        // Создаем уникальный идентификатор сообщения
        var messageId = $"{message.UserId}_{message.SessionId}_{message.TelegramInterlocutorId}_{message.SenderId}_{message.MessageTime.Ticks}";
        var processedKey = $"{_processedKeysPrefix}{messageId}";
        
        // Проверяем, не обрабатывалось ли уже это сообщение
        if (await _redisDb.KeyExistsAsync(processedKey))
        {
            _logger.LogDebug("[Skip] Сообщение {MessageId} уже было обработано ранее", messageId);
            return;
        }
        
        // Преобразуем сущность в DTO
        var messageData = new TelegramMessageData
        {
            UserId = message.UserId,
            SessionId = message.SessionId,
            TelegramUserId = message.TelegramUserId,
            TelegramInterlocutorId = message.TelegramInterlocutorId,
            SenderId = message.SenderId,
            MessageTime = message.MessageTime,
            MessageText = message.MessageText
        };
        
        // Сохраняем сообщение в Redis
        var messageJson = System.Text.Json.JsonSerializer.Serialize(messageData);
        await _redisDb.ListRightPushAsync(queueKey, messageJson);
        
        // Отмечаем сообщение как обрабатываемое с временем жизни 24 часа
        await _redisDb.StringSetAsync(processedKey, "1", TimeSpan.FromHours(24));
        
        _logger.LogInformation("[Redis] Сообщение {MessageId} добавлено в Redis очередь {QueueKey}", messageId, queueKey);
        
        // Отправляем сообщение в Kafka
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("[Kafka] Попытка отправки сообщения в Kafka через IKafkaMessageService");
                await _kafkaService.AddMessageAsync(message);
                _logger.LogInformation("[Kafka] Сообщение успешно отправлено в Kafka");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Kafka] Ошибка при отправке сообщения в Kafka. Сообщение: {Message}", message);
            }
        });
        
        // Проверяем размер очереди
        var queueLength = await _redisDb.ListLengthAsync(queueKey);
        if (queueLength >= _maxQueueSizePerSession)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessQueueAsync(key, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обработке очереди {QueueKey}", key);
                }
            });
        }
    }

    private async Task ProcessQueueAsync(string key, CancellationToken cancellationToken)
    {
        var queueKey = $"{_queueKeyPrefix}{key}";
        var queueLength = await _redisDb.ListLengthAsync(queueKey);
        
        if (queueLength == 0)
        {
            return;
        }

        try
        {
            // Получаем все сообщения из очереди
            var messages = new List<TelegramMessageData>();
            var messageJsonValues = await _redisDb.ListRangeAsync(queueKey, 0, -1);
            
            foreach (var messageJson in messageJsonValues)
            {
                try
                {
                    var message = System.Text.Json.JsonSerializer.Deserialize<TelegramMessageData>(messageJson.ToString());
                    if (message != null)
                    {
                        messages.Add(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при десериализации сообщения: {Message}", messageJson);
                }
            }
            
            if (!messages.Any())
            {
                return;
            }

            // Отфильтруем сообщения, которые уже могли быть сохранены другим сервисом
            var filteredMessages = new List<TelegramMessageData>();
            foreach (var message in messages)
            {
                var messageId = $"{message.UserId}_{message.SessionId}_{message.TelegramInterlocutorId}_{message.SenderId}_{message.MessageTime.Ticks}";
                var processedKey = $"{_processedKeysPrefix}{messageId}";
                
                // Если ключ существует, значит сообщение обрабатывается/обработано
                // Но мы проверяем дополнительно, не было ли оно уже сохранено в БД
                try
                {
                    // Проверяем существование сообщения в БД по уникальным полям
                    var exists = await CheckMessageExistsInDb(message, cancellationToken);
                    if (!exists)
                    {
                        filteredMessages.Add(message);
                    }
                    else
                    {
                        _logger.LogDebug("[Skip] Сообщение {MessageId} уже существует в БД", messageId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при проверке существования сообщения {MessageId}", messageId);
                    // При ошибке проверки добавляем сообщение для обработки
                    filteredMessages.Add(message);
                }
            }

            if (!filteredMessages.Any())
            {
                _logger.LogInformation("[DB] Все сообщения из очереди {QueueKey} уже обработаны", queueKey);
                // Удаляем обработанные сообщения из очереди
                await _redisDb.ListTrimAsync(queueKey, messages.Count, -1);
                return;
            }

            // Сохраняем отфильтрованные сообщения батчем через репозиторий
            _logger.LogInformation("[DB] Попытка батчевого сохранения {Count} сообщений для ключа {Key}", filteredMessages.Count, key);
            var savedCount = await _messageRepository.SaveMessagesAsync(filteredMessages, cancellationToken);
            _logger.LogInformation("[DB] Успешно сохранено {Count} сообщений для ключа {Key}", savedCount, key);
            
            // Удаляем обработанные сообщения из очереди
            await _redisDb.ListTrimAsync(queueKey, messages.Count, -1);
            _logger.LogInformation("[Redis] Очищено {Count} сообщений из очереди {QueueKey}", messages.Count, queueKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении сообщений для ключа {Key}", key);
        }
    }

    /// <summary>
    /// Проверяет существование сообщения в базе данных по уникальным полям
    /// </summary>
    private async Task<bool> CheckMessageExistsInDb(TelegramMessageData message, CancellationToken cancellationToken)
    {
        try
        {
            // Получаем сообщения с теми же параметрами
            var messages = await _messageRepository.GetMessagesAsync(
                message.UserId,
                message.SessionId,
                message.TelegramInterlocutorId,
                cancellationToken);

            // Проверяем наличие сообщения с теми же полями
            return messages.Any(m => 
                m.UserId == message.UserId &&
                m.SessionId == message.SessionId &&
                m.TelegramInterlocutorId == message.TelegramInterlocutorId &&
                m.SenderId == message.SenderId &&
                Math.Abs((m.MessageTime - message.MessageTime).TotalSeconds) < 1); // Допускаем разницу в 1 секунду
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке существования сообщения в БД");
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Сервис сбора сообщений Telegram запущен с интервалом сохранения {Interval}", _processingInterval);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Получаем все ключи очередей
                var keys = _redis.GetServer(_redis.GetEndPoints().First())
                    .Keys(pattern: $"{_queueKeyPrefix}*")
                    .Select(k => k.ToString().Replace(_queueKeyPrefix, ""))
                    .ToList();
                
                foreach (var key in keys)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;
                    
                    await ProcessQueueAsync(key, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке очередей сообщений");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }
        
        _logger.LogInformation("Сервис сбора сообщений Telegram остановлен");
    }
} 