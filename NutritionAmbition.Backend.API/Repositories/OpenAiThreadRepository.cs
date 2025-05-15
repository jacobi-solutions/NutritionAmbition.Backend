using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Settings;

namespace NutritionAmbition.Backend.API.Repositories
{
    public class OpenAiThreadRepository
    {
        private readonly IMongoCollection<OpenAiThreadRecord> _collection;

        public OpenAiThreadRepository(IMongoDatabase database, MongoDBSettings settings)
        {
            _collection = database.GetCollection<OpenAiThreadRecord>(settings.OpenAiThreadsCollectionName);
        }

        public async Task<OpenAiThreadRecord?> GetThreadByAccountIdAndDateAsync(string accountId, DateTime date)
        {
            // Create start and end of the day for a date range query
            var startOfDay = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            var filter = Builders<OpenAiThreadRecord>.Filter.And(
                Builders<OpenAiThreadRecord>.Filter.Eq(x => x.AccountId, accountId),
                Builders<OpenAiThreadRecord>.Filter.Gte(x => x.Date, startOfDay),
                Builders<OpenAiThreadRecord>.Filter.Lte(x => x.Date, endOfDay)
            );

            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task InsertThreadAsync(OpenAiThreadRecord record)
        {
            await _collection.InsertOneAsync(record);
        }
    }
} 