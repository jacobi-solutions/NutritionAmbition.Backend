using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Settings;
using NutritionAmbition.Backend.API.Constants;

namespace NutritionAmbition.Backend.API.Repositories
{
    public class FoodEntryRepository
    {
        private readonly IMongoCollection<FoodEntry> _collection;
        private readonly MongoDBSettings _mongoDBSettings;

        public FoodEntryRepository(IMongoDatabase database, MongoDBSettings mongoDbSettings)
        {
            _mongoDBSettings = mongoDbSettings;
            _collection = database.GetCollection<FoodEntry>(mongoDbSettings.FoodEntriesCollectionName);
        }

        public async Task<FoodEntry?> GetByIdAsync(string id)
        {
            return await _collection.Find(entry => entry.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<FoodEntry>> GetByAccountIdAsync(string accountId, DateTime? date = null, MealType? mealType = null)
        {
            var filter = Builders<FoodEntry>.Filter.Eq(entry => entry.AccountId, accountId);
            
            if (date.HasValue)
            {
                // Create a date range for the entire day
                var startOfDay = date.Value.Date;
                var endOfDay = startOfDay.AddDays(1).AddTicks(-1);
                
                filter &= Builders<FoodEntry>.Filter.Gte(entry => entry.LoggedDateUtc, startOfDay);
                filter &= Builders<FoodEntry>.Filter.Lte(entry => entry.LoggedDateUtc, endOfDay);
            }
            
            if (mealType.HasValue)
            {
                filter &= Builders<FoodEntry>.Filter.Eq(entry => entry.Meal, mealType.Value);
            }
            
            return await _collection.Find(filter)
                .SortByDescending(e => e.LoggedDateUtc)
                .ToListAsync();
        }

        public async Task<List<FoodEntry>> GetFoodEntriesByAccountAndDateAsync(string accountId, DateTime date)
        {
            // Normalize to start of UTC day to ensure all entries for the calendar date are included
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            // Use bitwise AND (&) to combine MongoDB filters
            var filter = Builders<FoodEntry>.Filter.Eq(f => f.AccountId, accountId) &
                Builders<FoodEntry>.Filter.Gte(f => f.LoggedDateUtc, startOfDay) &
                Builders<FoodEntry>.Filter.Lt(f => f.LoggedDateUtc, endOfDay);
                
            return await _collection.Find(filter).ToListAsync();
        }

        public async Task<List<FoodEntry>> GetByDateRangeAsync(string accountId, DateTime startDate, DateTime endDate, MealType? mealType = null)
        {
            var filter = Builders<FoodEntry>.Filter.Eq(entry => entry.AccountId, accountId);
            
            // Use start of day for start date and end of day for end date
            var startOfStartDay = startDate.Date;
            var endOfEndDay = endDate.Date.AddDays(1).AddTicks(-1);
            
            filter &= Builders<FoodEntry>.Filter.Gte(entry => entry.LoggedDateUtc, startOfStartDay);
            filter &= Builders<FoodEntry>.Filter.Lte(entry => entry.LoggedDateUtc, endOfEndDay);
            
            if (mealType.HasValue)
            {
                filter &= Builders<FoodEntry>.Filter.Eq(entry => entry.Meal, mealType.Value);
            }
            
            return await _collection.Find(filter)
                .SortByDescending(e => e.LoggedDateUtc)
                .ToListAsync();
        }

        public async Task<string> AddAsync(FoodEntry entry)
        {
            await _collection.InsertOneAsync(entry);
            return entry.Id;
        }

        public async Task UpdateAsync(FoodEntry entry)
        {
            await _collection.ReplaceOneAsync(e => e.Id == entry.Id, entry);
        }

        public async Task DeleteAsync(string id)
        {
            await _collection.DeleteOneAsync(entry => entry.Id == id);
        }

        public async Task<long> UpdateAccountReferencesAsync(string fromAccountId, string toAccountId)
        {
            var filter = Builders<FoodEntry>.Filter.Eq(x => x.AccountId, fromAccountId);
            var update = Builders<FoodEntry>.Update.Set(x => x.AccountId, toAccountId);
            var result = await _collection.UpdateManyAsync(filter, update);
            return result.ModifiedCount;
        }
    }
}
