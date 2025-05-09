using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TL;
using TalkLens.Collector.Infrastructure.Messengers.Telegram;
using System.Collections.Concurrent;
using TalkLens.Collector.Domain.Interfaces;
using TalkLens.Collector.Infrastructure.Database;

namespace TalkLens.Collector.Infrastructure.Services.Telegram;

/// <summary>
/// Фоновая служба для мониторинга обновлений всех сессий Telegram
/// </summary>
public class TelegramUpdateMonitorService : BackgroundService
{
    private readonly ILogger<TelegramUpdateMonitorService> _logger;
    private readonly RedisTelegramSessionCache _redisCache;
    private readonly ITelegramSubscriptionRepository _subscriptionRepository;
    private readonly TelegramMessageCollectorService _messageCollectorService;
    
    // Словарь для отслеживания подписанных клиентов
    private readonly ConcurrentDictionary<string, bool> _subscribedClients = new();
    private readonly ConcurrentDictionary<string, TelegramSession> _activeSessions = new();

    public TelegramUpdateMonitorService(
        ILogger<TelegramUpdateMonitorService> logger,
        RedisTelegramSessionCache redisCache,
        ITelegramSubscriptionRepository subscriptionRepository,
        TelegramMessageCollectorService messageCollectorService)
    {
        _logger = logger;
        _redisCache = redisCache;
        _subscriptionRepository = subscriptionRepository;
        _messageCollectorService = messageCollectorService;
        
        _logger.LogInformation("TelegramUpdateMonitorService инициализирован");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Служба мониторинга обновлений Telegram запущена");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Подписываемся на обновления всех клиентов в кэше
                    SubscribeToAllClients();
                    
                    // Проверяем каждые 5 минут для подписки на новые клиенты
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в TelegramUpdateMonitorService: {ErrorMessage}", ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение при остановке сервиса
        }
        
        _logger.LogInformation("Служба мониторинга обновлений Telegram остановлена");
    }

    /// <summary>
    /// Обрабатывает новое сообщение или редактирование сообщения
    /// </summary>
    private async Task ProcessNewMessageAsync(string clientInfo, string sessionId, MessageBase messageBase, bool isEdit)
    {
        try
        {
            _logger.LogTrace("Поиск сессии для {SessionId}. Активных сессий: {Count}", sessionId, _activeSessions.Count);
            TelegramSession? session = null;
            if (_activeSessions.ContainsKey(sessionId))
            {
                session = _activeSessions[sessionId];
                _logger.LogDebug("Сессия найдена по прямому ключу {SessionId}", sessionId);
            }
            else
            {
                session = _activeSessions.Values.FirstOrDefault(s => Path.GetFileNameWithoutExtension(Path.GetFileName(s.GetSessionFilePath())).EndsWith(sessionId));
                if (session != null)
                {
                    _logger.LogDebug("Сессия найдена по окончанию имени файла {SessionId}", sessionId);
                }
                else
                {
                    session = _activeSessions.Values.FirstOrDefault(s => s.GetSessionFilePath().Contains(sessionId));
                    if (session != null)
                    {
                        _logger.LogDebug("Сессия найдена по частичному совпадению в пути к файлу {SessionId}", sessionId);
                    }
                }
            }
            if (messageBase is Message message)
            {
                if (session != null)
                {
                    var telegramUserId = session.GetTelegramUserId();
                    var fromId = message.from_id?.ID ?? telegramUserId;
                    var chatId = message.peer_id?.ID ?? 0;
                    var hasSubscription = await _subscriptionRepository.ExistsAnySubscriptionAsync(sessionId, chatId, CancellationToken.None);
                    if (!hasSubscription)
                    {
                        _logger.LogDebug("Пропущено сообщение от {FromId}, т.к. нет подписки. Сессия: {SessionId}", fromId, sessionId);
                        return;
                    }
                    try
                    {
                        var userId = session.GetUserId();
                        var messageEntity = new TelegramMessageEntity
                        {
                            UserId = userId,
                            SessionId = sessionId,
                            TelegramUserId = telegramUserId,
                            TelegramInterlocutorId = chatId,
                            SenderId = fromId,
                            MessageTime = message.Date,
                            MessageText = message.message
                        };
                        _logger.LogInformation("[Kafka] Попытка отправки сообщения в Kafka для session {SessionId}, chat {ChatId}", sessionId, chatId);
                        await _messageCollectorService.EnqueueMessageAsync(messageEntity);
                        _logger.LogInformation("[Kafka] Сообщение отправлено в Kafka и добавлено в Redis для session {SessionId}, chat {ChatId}", sessionId, chatId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при добавлении сообщения в коллектор");
                    }
                }
            }
            LogMessageUpdate(clientInfo, messageBase, isEdit);
            if (session == null)
            {
                _logger.LogWarning("Не удалось найти активную сессию для {SessionId}, но сообщение обработано", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке подписки на сообщение: {ErrorMessage}", ex.Message);
            LogMessageUpdate(clientInfo, messageBase, isEdit);
        }
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
            var cacheKey = $"{session.Key}";
            
            try
            {
                // Добавляем сессию в словарь активных сессий по оригинальному ключу
                _activeSessions[cacheKey] = session.Value;
                
                // Если ключ содержит двоеточие (формат "UserId:SessionId"), 
                // извлекаем SessionId после двоеточия
                if (cacheKey.Contains(':'))
                {
                    var sessionId = cacheKey.Split(':').Last();
                    _activeSessions[sessionId] = session.Value;
                    _logger.LogDebug("Сессия добавлена с ключами {CacheKey} и {SessionId}", cacheKey, sessionId);
                }
                else
                {
                    _logger.LogDebug("Сессия добавлена только с ключом {CacheKey}", cacheKey);
                }
                
                // // Также пробуем получить sessionId из пути к файлу для дополнительной надежности
                // try {
                //     var filePath = session.Value.GetSessionFilePath();
                //     var fileName = Path.GetFileNameWithoutExtension(Path.GetFileName(filePath));
                //     
                //     // Если имя файла содержит подчеркивание (формат "UserId_SessionId")
                //     if (fileName.Contains('_'))
                //     {
                //         var fileSessionId = fileName.Split('_').Last();
                //         if (!_activeSessions.ContainsKey(fileSessionId))
                //         {
                //             _activeSessions[fileSessionId] = session.Value;
                //             _logger.LogDebug("Дополнительно сессия добавлена с ключом из имени файла {FileSessionId}", fileSessionId);
                //         }
                //     }
                // } catch (Exception fileEx) {
                //     _logger.LogWarning(fileEx, "Ошибка при обработке пути к файлу сессии, но продолжаем работу");
                // }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при извлечении sessionId из ключа кэша {CacheKey}", cacheKey);
                _activeSessions[cacheKey] = session.Value;
            }
            
            // Проверяем, подписаны ли мы уже на этого клиента
            if (_subscribedClients.ContainsKey(cacheKey)) continue;
            
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

    /// <summary>
    /// Обрабатывает полученные обновления от клиентов Telegram
    /// </summary>
    private async void OnUpdate(object sender, IObject update)
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
            string fullSessionId = Path.GetFileNameWithoutExtension(session.GetSessionFilePath().Split('/').Last());
            string sessionId = fullSessionId;
            if (fullSessionId.Contains('_'))
            {
                sessionId = fullSessionId.Split('_').Last();
            }
            switch (update)
            {
                case UpdateNewMessage unm:
                    await ProcessNewMessageAsync(clientInfo, sessionId, unm.message, false);
                    break;
                default:
                    _logger.LogDebug("Получено обновление типа {UpdateType} от {ClientInfo}", update.GetType().Name, clientInfo);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке обновления: {ErrorMessage}", ex.Message);
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