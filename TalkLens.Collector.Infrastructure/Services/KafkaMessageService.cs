using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using TalkLens.Collector.Infrastructure.Configuration;

namespace TalkLens.Collector.Infrastructure.Services;

public class KafkaMessageService : IKafkaMessageService, IDisposable
{
    private readonly KafkaConfiguration _config;
    private readonly IProducer<string, string> _producer;
    private readonly List<Message<string, string>> _messageBuffer;
    private readonly SemaphoreSlim _bufferLock = new(1, 1);
    private readonly Timer _flushTimer;
    private bool _disposed;

    public KafkaMessageService(IOptions<KafkaConfiguration> config)
    {
        _config = config.Value;
        _messageBuffer = new List<Message<string, string>>();
        
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _config.BootstrapServers
        };
        
        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
        
        _flushTimer = new Timer(async _ => await FlushAsync(), null, 
            _config.BatchTimeout, _config.BatchTimeout);
    }

    public async Task AddMessageAsync<T>(T message)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaMessageService));

        var jsonMessage = JsonSerializer.Serialize(message);
        var kafkaMessage = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = jsonMessage
        };

        await _bufferLock.WaitAsync();
        try
        {
            _messageBuffer.Add(kafkaMessage);
            
            if (_messageBuffer.Count >= _config.BatchSize)
            {
                await FlushBufferAsync();
            }
        }
        finally
        {
            _bufferLock.Release();
        }
    }

    public async Task FlushAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaMessageService));

        await _bufferLock.WaitAsync();
        try
        {
            await FlushBufferAsync();
        }
        finally
        {
            _bufferLock.Release();
        }
    }

    private async Task FlushBufferAsync()
    {
        if (_messageBuffer.Count == 0)
            return;

        var messages = _messageBuffer.ToList();
        _messageBuffer.Clear();

        foreach (var message in messages)
        {
            try
            {
                await _producer.ProduceAsync(_config.TopicName, message);
            }
            catch (ProduceException<string, string> e)
            {
                // TODO: Добавить логирование ошибок
                Console.WriteLine($"Ошибка при отправке сообщения в Kafka: {e.Error.Reason}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _flushTimer.Dispose();
        _producer.Dispose();
        _bufferLock.Dispose();
    }
} 