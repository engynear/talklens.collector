using System.Collections.Concurrent;
using LinqToDB.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Domain.Models.Telegram;
using TalkLens.Collector.Infrastructure.Configuration;
using TalkLens.Collector.Infrastructure.Database;
using TL;

namespace TalkLens.Collector.Infrastructure.Services;

public class TelegramMessageCollectorService : BackgroundService
{
    private readonly ILogger<TelegramMessageCollectorService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITelegramSubscriptionRepository _subscriptionRepository;
    private readonly ConcurrentDictionary<string, ConcurrentBag<TelegramMessageEntity>> _messageQueue;
    private readonly TimeSpan _processingInterval;
    private readonly int _maxQueueSizePerSession;

    public TelegramMessageCollectorService(
        ILogger<TelegramMessageCollectorService> logger,
        IServiceProvider serviceProvider,
        ITelegramSubscriptionRepository subscriptionRepository,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _subscriptionRepository = subscriptionRepository;
        _messageQueue = new ConcurrentDictionary<string, ConcurrentBag<TelegramMessageEntity>>();
        
        // Загрузка настроек из конфигурации
        int saveIntervalMinutes = 5; // По умолчанию 5 минут
        if (int.TryParse(configuration["MessageCollector:SaveIntervalMinutes"], out int configMinutes) && configMinutes > 0)
        {
            saveIntervalMinutes = configMinutes;
        }
        _processingInterval = TimeSpan.FromMinutes(saveIntervalMinutes);
        
        // Максимальный размер очереди
        _maxQueueSizePerSession = 1000; // По умолчанию 1000 сообщений
        if (int.TryParse(configuration["MessageCollector:MaxQueueSizePerSession"], out int configMaxSize) && configMaxSize > 0)
        {
            _maxQueueSizePerSession = configMaxSize;
        }
        
        _logger.LogInformation("TelegramMessageCollectorService инициализирован. Интервал сохранения: {SaveInterval}, " +
                            "максимальный размер очереди: {MaxQueueSize}", _processingInterval, _maxQueueSizePerSession);
    }

    public void EnqueueMessage(TelegramMessageEntity message)
    {
        // Получаем актуальный sessionId (без UserId в составном идентификаторе)
        string sessionId = message.SessionId;
        if (sessionId.Contains('_'))
        {
            // Формат: "UserId_SessionId" -> берём только SessionId
            sessionId = sessionId.Split('_').Last();
        }
        
        // Проверяем наличие подписки на данный контакт
        var hasSubscription = _subscriptionRepository.ExistsAnySubscriptionAsync(
            sessionId,
            message.TelegramInterlocutorId,
            CancellationToken.None).GetAwaiter().GetResult();
            
        // Если нет подписки, пропускаем сообщение
        if (!hasSubscription)
        {
            _logger.LogDebug(
                "Пропущено сообщение от {InterlocutorId}, т.к. нет подписки. Сессия: {SessionId}, Пользователь: {UserId}", 
                message.TelegramInterlocutorId, sessionId, message.UserId);
            return;
        }
        
        var key = $"{message.UserId}_{message.SessionId}";
        var messages = _messageQueue.GetOrAdd(key, _ => new ConcurrentBag<TelegramMessageEntity>());
        messages.Add(message);
        
        // Если размер очереди превысил порог, сохраняем сообщения немедленно
        if (messages.Count >= _maxQueueSizePerSession)
        {
            _logger.LogInformation("Очередь для {Key} достигла максимального размера {MaxSize}, запуск принудительного сохранения", 
                key, _maxQueueSizePerSession);
            
            // Запускаем сохранение в отдельном потоке, чтобы не блокировать текущий
            Task.Run(async () => 
            {
                try 
                {
                    await SaveMessagesForKey(key, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при принудительном сохранении сообщений для {Key}", key);
                }
            });
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Сервис сбора сообщений Telegram запущен с интервалом сохранения {Interval}", _processingInterval);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMessageQueue(stoppingToken);
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке очереди сообщений");
                // В случае ошибки делаем паузу перед следующей попыткой
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        
        _logger.LogInformation("Сервис сбора сообщений Telegram остановлен");
    }

    private async Task ProcessMessageQueue(CancellationToken cancellationToken)
    {
        var keys = _messageQueue.Keys.ToList();
        if (!keys.Any())
        {
            return; // Нет сообщений для обработки
        }
        
        _logger.LogDebug("Начало обработки очереди сообщений. Сессий для обработки: {Count}", keys.Count);
        
        foreach (var key in keys)
        {
            await SaveMessagesForKey(key, cancellationToken);
        }
        
        _logger.LogDebug("Обработка очереди сообщений завершена");
    }
    
    private async Task SaveMessagesForKey(string key, CancellationToken cancellationToken)
    {
        if (_messageQueue.TryRemove(key, out var messages))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TalkLensDbContext>();

                var messagesList = messages.ToList();
                if (messagesList.Any())
                {
                    await dbContext.BulkCopyAsync(messagesList, cancellationToken);
                    _logger.LogInformation("Сохранено {Count} сообщений для сессии {Key}", messagesList.Count, key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении сообщений для сессии {Key}", key);
                // Возвращаем сообщения в очередь для повторной попытки
                var newBag = new ConcurrentBag<TelegramMessageEntity>(messages);
                _messageQueue.TryAdd(key, newBag);
            }
        }
    }
} 