using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TalkLens.Collector.Infrastructure.Configuration;
using TalkLens.Collector.Infrastructure.Services;

namespace TalkLens.Collector.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKafka(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaConfiguration>(configuration.GetSection("Kafka"));
        services.AddSingleton<IKafkaMessageService, KafkaMessageService>();
        return services;
    }
} 