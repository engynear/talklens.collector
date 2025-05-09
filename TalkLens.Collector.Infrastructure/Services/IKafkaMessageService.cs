namespace TalkLens.Collector.Infrastructure.Services;

public interface IKafkaMessageService
{
    Task AddMessageAsync<T>(T message);
    Task FlushAsync();
} 