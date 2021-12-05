using MongoDB.Driver;

namespace NzCovidPassTelegramBot.Repositories.DataSources
{
    public interface IMongoDataSource
    {
        IMongoDatabase Database { get; }
    }

    public class MongoDataSource : IMongoDataSource
    {
        public IMongoDatabase Database { get; }
        private readonly MongoClient _client;

        public MongoDataSource(string connectionString, string databaseName)
        {
            _client = new MongoClient(connectionString);
            Database = _client.GetDatabase(databaseName);
        }
    }
}
