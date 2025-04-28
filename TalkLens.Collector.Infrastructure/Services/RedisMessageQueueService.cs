using System.Text.Json;
using LinqToDB.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Infrastructure.Database;

namespace TalkLens.Collector.Infrastructure.Services;

/// <summary>
/// Сервис для хранения и обработки очереди сообщений в Redis
/// </summary>
public class RedisMessageQueueService : BackgroundService
{
    private readonly ILogger<RedisMessageQueueService> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly string _queueKeyPrefix;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _processingInterval;
    private readonly int _batchSize;
    
    /// <summary>
    /// Конструктор сервиса очереди сообщений в Redis
    /// </summary>
    public RedisMessageQueueService(
        ILogger<RedisMessageQueueService> logger,
        IConnectionMultiplexer redis,
        IConfiguration configuration, 
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _redis = redis;
        _db = redis.GetDatabase();
        _serviceProvider = serviceProvider;
        
        // Префикс ключей для очереди сообщений
        _queueKeyPrefix = configuration["Redis:MessageQueuePrefix"] ?? "telegram:message_queue:";
        
        // Интервал обработки очереди (по умолчанию 2 минуты)
        int saveIntervalMinutes = 2;
        if (int.TryParse(configuration["MessageCollector:SaveIntervalMinutes"], out int configMinutes) && configMinutes > 0)
        {
            saveIntervalMinutes = configMinutes;
        }
        _processingInterval = TimeSpan.FromMinutes(saveIntervalMinutes);
        
        // Размер пакета для обработки (по умолчанию 1000 сообщений)
        _batchSize = 1000;
        if (int.TryParse(configuration["MessageCollector:BatchSize"], out int configBatchSize) && configBatchSize > 0)
        {
            _batchSize = configBatchSize;
        }
        
        _logger.LogInformation(
            "RedisMessageQueueService инициализирован. Интервал сохранения: {SaveInterval}, размер пакета: {BatchSize}", 
            _processingInterval, _batchSize);
    }
    
    /// <summary>
    /// Добавляет сообщение в очередь Redis
    /// </summary>
    /// <param name="message">Сообщение для добавления в очередь</param>
    /// <param name="sessionKey">Ключ сессии для группировки сообщений</param>
    public async Task EnqueueMessageAsync(TelegramMessageEntity message, string sessionKey)
    {
        try
        {
            // Получаем актуальный sessionId (без UserId в составном идентификаторе)
            string sessionId = message.SessionId;
            if (sessionId.Contains('_'))
            {
                // Формат: "UserId_SessionId" -> берём только SessionId
                sessionId = sessionId.Split('_').Last();
            }
            
            // Создаем ключ для Redis
            string queueKey = $"{_queueKeyPrefix}{sessionKey}";
            
            // Сериализуем сообщение в JSON
            string messageJson = JsonSerializer.Serialize(message);
            
            // Добавляем сообщение в список Redis
            await _db.ListRightPushAsync(queueKey, messageJson);
            
            // Увеличиваем счетчик размера очереди
            long queueSize = await _db.ListLengthAsync(queueKey);
            
            _logger.LogDebug("Сообщение добавлено в очередь {QueueKey}, текущий размер: {QueueSize}", queueKey, queueSize);
            
            // Если размер очереди превышает заданный предел, инициируем сохранение
            if (queueSize >= _batchSize)
            {
                _logger.LogInformation("Очередь {QueueKey} достигла максимального размера {MaxSize}, запуск сохранения", 
                    queueKey, _batchSize);
                
                // Запускаем сохранение в отдельном потоке
                _ = Task.Run(async () => 
                {
                    try 
                    {
                        await ProcessQueueAsync(queueKey, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при обработке очереди {QueueKey}", queueKey);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при добавлении сообщения в очередь Redis");
        }
    }
    
    /// <summary>
    /// Фоновый обработчик очереди сообщений
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Сервис обработки очереди сообщений запущен с интервалом {Interval}", _processingInterval);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAllQueuesAsync(stoppingToken);
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке очередей сообщений");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        
        _logger.LogInformation("Сервис обработки очереди сообщений остановлен");
    }
    
    /// <summary>
    /// Обрабатывает все очереди сообщений
    /// </summary>
    private async Task ProcessAllQueuesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Получаем все ключи, соответствующие шаблону очереди сообщений
            var queueKeys = GetQueueKeys();
            
            if (!queueKeys.Any())
            {
                return; // Нет очередей для обработки
            }
            
            _logger.LogDebug("Начало обработки очередей сообщений. Найдено {Count} очередей", queueKeys.Count);
            
            foreach (var queueKey in queueKeys)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                await ProcessQueueAsync(queueKey, cancellationToken);
            }
            
            _logger.LogDebug("Обработка всех очередей завершена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении списка очередей");
        }
    }
    
    /// <summary>
    /// Получает список ключей очередей сообщений
    /// </summary>
    private List<string> GetQueueKeys()
    {
        var queueKeys = new List<string>();
        
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            
            // Получаем все ключи, соответствующие шаблону очереди сообщений
            foreach (var key in server.Keys(pattern: $"{_queueKeyPrefix}*"))
            {
                queueKeys.Add(key.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении ключей очередей");
        }
        
        return queueKeys;
    }
    
    /// <summary>
    /// Обрабатывает отдельную очередь сообщений
    /// </summary>
    private async Task ProcessQueueAsync(string queueKey, CancellationToken cancellationToken)
    {
        try
        {
            // Проверяем существование очереди
            long queueLength = await _db.ListLengthAsync(queueKey);
            
            if (queueLength == 0)
            {
                return; // Очередь пуста
            }
            
            _logger.LogInformation("Обработка очереди {QueueKey}, размер: {Size}", queueKey, queueLength);
            
            // Ограничиваем количество сообщений для обработки за один раз
            int batchSize = (int)Math.Min(queueLength, _batchSize);
            var messages = new List<TelegramMessageEntity>();
            
            // Извлекаем сообщения из очереди без их удаления
            var messageJsonValues = await _db.ListRangeAsync(queueKey, 0, batchSize - 1);
            
            foreach (var messageJson in messageJsonValues)
            {
                try
                {
                    var message = JsonSerializer.Deserialize<TelegramMessageEntity>(messageJson.ToString());
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
                return; // Нет валидных сообщений
            }
            
            // Сохраняем сообщения в базу данных
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbFactory = scope.ServiceProvider.GetRequiredService<Func<TalkLensDbContext>>();
                using var dbContext = dbFactory();
                try
                {
                    // Сохраняем пакетом все сообщения
                    await dbContext.BulkCopyAsync(messages, cancellationToken);
                    
                    // Если сохранение прошло успешно, удаляем сообщения из очереди
                    // (удаляем слева, так как очередь FIFO)
                    await _db.ListTrimAsync(queueKey, batchSize, -1);
                    
                    _logger.LogInformation("Успешно сохранено {Count} сообщений из очереди {QueueKey}", 
                        messages.Count, queueKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при сохранении сообщений из очереди {QueueKey} в БД", queueKey);
                    // Не удаляем сообщения из очереди, чтобы попробовать еще раз позже
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке очереди {QueueKey}", queueKey);
        }
    }
} 