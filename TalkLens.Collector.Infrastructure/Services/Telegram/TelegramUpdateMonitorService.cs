using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TL;
using TalkLens.Collector.Infrastructure.Services;
using TalkLens.Collector.Infrastructure.Messengers.Telegram;
using TalkLens.Collector.Infrastructure.Services.Telegram;
using System.Collections.Concurrent;
using System.IO;
using TalkLens.Collector.Domain.Interfaces;
using System.Linq;

namespace TalkLens.Collector.Infrastructure.Services.Telegram;

/// <summary>
/// Фоновая служба для мониторинга обновлений всех сессий Telegram
/// </summary>
public class TelegramUpdateMonitorService : BackgroundService
{
    private readonly ILogger<TelegramUpdateMonitorService> _logger;
    private readonly RedisTelegramSessionCache _redisCache;
    private readonly ITelegramSubscriptionRepository _subscriptionRepository;
    
    // Словарь для отслеживания подписанных клиентов
    private readonly ConcurrentDictionary<string, bool> _subscribedClients = new();

    public TelegramUpdateMonitorService(
        ILogger<TelegramUpdateMonitorService> logger,
        RedisTelegramSessionCache redisCache,
        ITelegramSubscriptionRepository subscriptionRepository)
    {
        _logger = logger;
        _redisCache = redisCache;
        _subscriptionRepository = subscriptionRepository;
        
        _logger.LogInformation("TelegramUpdateMonitorService инициализирован");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelegramUpdateMonitorService запущен");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Подписываемся на обновления всех клиентов в кэше
                SubscribeToAllClients();
                
                // Проверяем каждые 5 минут для подписки на новые клиенты
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение при остановке сервиса
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в TelegramUpdateMonitorService: {ErrorMessage}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        
        _logger.LogInformation("TelegramUpdateMonitorService остановлен");
    }

    /// <summary>
    /// Подписывается на обновления всех клиентов в кэше
    /// </summary>
    private void SubscribeToAllClients()
    {
        // Получаем все сессии из Redis кэша
        var sessions = _redisCache.GetAllSessions();
        
        _logger.LogInformation("Найдено {Count} сессий в кэше для подписки на обновления", sessions.Count);
        
        foreach (var session in sessions)
        {
            string cacheKey = $"{session.Key}";
            
            // Проверяем, подписаны ли мы уже на этого клиента
            if (!_subscribedClients.ContainsKey(cacheKey))
            {
                try
                {
                    _logger.LogInformation("Подписываемся на обновления клиента {ClientKey}", cacheKey);
                    
                    // Проверяем существование файла состояния обновлений
                    string updatesFilePath = session.Value.GetUpdatesFilePath();
                    _logger.LogDebug("Проверка файла состояния обновлений: {FilePath}", updatesFilePath);
                    
                    // Создаем директорию для файла состояния, если она не существует
                    string? directory = Path.GetDirectoryName(updatesFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        _logger.LogDebug("Создание директории для файла состояния: {Directory}", directory);
                        Directory.CreateDirectory(directory);
                    }
                    
                    // Создаем пустой файл состояния, если он не существует или пуст
                    if (!File.Exists(updatesFilePath) || new FileInfo(updatesFilePath).Length == 0)
                    {
                        _logger.LogInformation("Создание пустого файла состояния обновлений для клиента {ClientKey} по пути {FilePath}", 
                            cacheKey, updatesFilePath);
                        File.WriteAllText(updatesFilePath, "{}");
                    }
                    
                    // Подписываемся на обновления
                    session.Value.SubscribeToUpdates(OnUpdate);
                    
                    // Отмечаем клиента как подписанного
                    _subscribedClients[cacheKey] = true;
                    
                    _logger.LogInformation("Успешно подписались на обновления клиента {ClientKey}", cacheKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при подписке на обновления клиента {ClientKey}: {ErrorMessage}", 
                        cacheKey, ex.Message);
                    
                    // Пробуем создать обновления без существующего файла состояния
                    try
                    {
                        if (!_subscribedClients.ContainsKey(cacheKey))
                        {
                            _logger.LogInformation("Повторная попытка подписки на обновления клиента {ClientKey} без загрузки состояния", cacheKey);
                            
                            session.Value.SubscribeToUpdatesWithoutState(OnUpdate);
                            
                            _subscribedClients[cacheKey] = true;
                            _logger.LogInformation("Успешно подписались на обновления клиента {ClientKey} без загрузки состояния", cacheKey);
                        }
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Повторная попытка подписки на обновления клиента {ClientKey} также не удалась: {ErrorMessage}", 
                            cacheKey, innerEx.Message);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Обрабатывает полученные обновления от клиентов Telegram
    /// </summary>
    private void OnUpdate(object sender, IObject update)
    {
        try
        {
            var session = sender as TelegramSession;
            if (session == null)
            {
                _logger.LogWarning("Получено обновление от неизвестного отправителя");
                return;
            }

            string clientInfo = $"Клиент с номером: {session.PhoneNumber}";
            
            // Получаем sessionId из пути файла сессии
            string fullSessionId = Path.GetFileNameWithoutExtension(session.GetSessionFilePath().Split('/').Last());
            
            // Извлекаем только часть sessionId после подчеркивания (если оно есть)
            string sessionId = fullSessionId;
            if (fullSessionId.Contains('_'))
            {
                // Формат: "UserId_SessionId" -> берём только SessionId
                sessionId = fullSessionId.Split('_').Last();
            }
            
            // Обработка разных типов обновлений
            switch (update)
            {
                case UpdateNewMessage unm:
                    ProcessNewMessage(clientInfo, sessionId, unm.message, false);
                    break;
                
                // case UpdateEditMessage uem:
                //     ProcessNewMessage(clientInfo, sessionId, uem.message, true);
                //     break;
                
                default:
                    _logger.LogDebug("Получено обновление типа {UpdateType} от {ClientInfo}", 
                        update.GetType().Name, clientInfo);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке обновления: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    /// Обрабатывает новое сообщение или редактирование сообщения
    /// </summary>
    private void ProcessNewMessage(string clientInfo, string sessionId, MessageBase messageBase, bool isEdit)
    {
        try
        {
            // Проверяем, является ли сообщение от другого пользователя
            if (messageBase is Message message && message.from_id != null)
            {
                var fromId = message.from_id.ID;
                
                // Проверяем, есть ли подписка на данного отправителя по sessionId и interlocutorId
                var hasSubscription = _subscriptionRepository.ExistsAnySubscriptionAsync(
                    sessionId, 
                    fromId, 
                    CancellationToken.None).GetAwaiter().GetResult();
                
                // Если нет подписки, не обрабатываем сообщение
                if (!hasSubscription)
                {
                    _logger.LogDebug(
                        "Пропущено сообщение от {FromId}, т.к. нет подписки. Сессия: {SessionId}", 
                        fromId, sessionId);
                    return;
                }
            }
            
            // Логируем сообщение
            LogMessageUpdate(clientInfo, messageBase, isEdit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке подписки на сообщение: {ErrorMessage}", ex.Message);
            // В случае ошибки всё равно логируем сообщение
            LogMessageUpdate(clientInfo, messageBase, isEdit);
        }
    }

    /// <summary>
    /// Логирует информацию о сообщении
    /// </summary>
    private void LogMessageUpdate(string clientInfo, MessageBase messageBase, bool isEdit)
    {
        string action = isEdit ? "отредактировано" : "получено";
        DateTime timestamp = DateTime.UtcNow;
        
        switch (messageBase)
        {
            case Message message:
                var fromUser = message.from_id?.ID.ToString() ?? "неизвестно";
                var chatId = message.peer_id?.ID.ToString() ?? "неизвестно";
                
                _logger.LogInformation(
                    "{Timestamp:yyyy-MM-dd HH:mm:ss}: {Action} сообщение через {ClientInfo}. " +
                    "От: {FromUser}, Чат: {ChatId}, Текст: \"{MessageText}\"",
                    timestamp, action, clientInfo, fromUser, chatId, 
                    message.message.Length > 50 ? message.message.Substring(0, 50) + "..." : message.message);
                break;
            
            case MessageService messageService:
                var fromServiceUser = messageService.from_id?.ID.ToString() ?? "неизвестно";
                var serviceChatId = messageService.peer_id?.ID.ToString() ?? "неизвестно";
                
                _logger.LogInformation(
                    "{Timestamp:yyyy-MM-dd HH:mm:ss}: {Action} сервисное сообщение через {ClientInfo}. " +
                    "От: {FromUser}, Чат: {ChatId}, Действие: {ActionType}",
                    timestamp, action, clientInfo, fromServiceUser, serviceChatId, 
                    messageService.action.GetType().Name);
                break;
            
            default:
                _logger.LogInformation(
                    "{Timestamp:yyyy-MM-dd HH:mm:ss}: {Action} сообщение неизвестного типа {MessageType} через {ClientInfo}",
                    timestamp, action, messageBase.GetType().Name, clientInfo);
                break;
        }
    }
} 