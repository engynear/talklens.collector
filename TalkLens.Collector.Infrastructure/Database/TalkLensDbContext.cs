using LinqToDB;
using LinqToDB.Data;

namespace TalkLens.Collector.Infrastructure.Database;

public class TalkLensDbContext : DataConnection
{
    public ITable<TelegramSessionEntity> TelegramSessions => this.GetTable<TelegramSessionEntity>();

    public TalkLensDbContext(string connectionString) 
        : base(ProviderName.PostgreSQL, connectionString)
    {
    }
} 