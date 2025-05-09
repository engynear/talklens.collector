namespace TalkLens.Collector.Infrastructure.Configuration;

public class KafkaConfiguration
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string TopicName { get; set; } = "messages";
    public int BatchSize { get; set; } = 100;
    public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromSeconds(5);
} 