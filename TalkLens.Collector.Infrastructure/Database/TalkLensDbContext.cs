using LinqToDB;
using LinqToDB.Data;

namespace TalkLens.Collector.Infrastructure.Database;

public class TalkLensDbContext : DataConnection
{
    public ITable<TelegramSessionEntity> TelegramSessions => this.GetTable<TelegramSessionEntity>();
    public ITable<TelegramSubscriptionEntity> TelegramSubscriptions => this.GetTable<TelegramSubscriptionEntity>();
    public ITable<TelegramMessageEntity> TelegramMessages => this.GetTable<TelegramMessageEntity>();

    public TalkLensDbContext(string connectionString) 
        : base(ProviderName.PostgreSQL, connectionString)
    {
    }
}