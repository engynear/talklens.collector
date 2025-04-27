using System.Collections.Concurrent;
using LinqToDB.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Infrastructure.Database;
using TL;

namespace TalkLens.Collector.Infrastructure.Services;

public class TelegramMessageCollectorService : BackgroundService
{
    private readonly ILogger<TelegramMessageCollectorService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITelegramSubscriptionRepository _subscriptionRepository;
    private readonly ConcurrentDictionary<string, ConcurrentBag<TelegramMessageEntity>> _messageQueue;
    private readonly TimeSpan _processingInterval = TimeSpan.FromMinutes(1);

    public TelegramMessageCollectorService(
        ILogger<TelegramMessageCollectorService> logger,
        IServiceProvider serviceProvider,
        ITelegramSubscriptionRepository subscriptionRepository)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _subscriptionRepository = subscriptionRepository;
        _messageQueue = new ConcurrentDictionary<string, ConcurrentBag<TelegramMessageEntity>>();
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
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
            }
        }
    }

    private async Task ProcessMessageQueue(CancellationToken cancellationToken)
    {
        var keys = _messageQueue.Keys.ToList();
        foreach (var key in keys)
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
} 