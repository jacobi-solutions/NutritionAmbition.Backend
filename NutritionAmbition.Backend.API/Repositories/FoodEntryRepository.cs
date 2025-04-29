using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.Repositories
{
    public class FoodEntryRepository
    {
        private readonly IMongoCollection<FoodEntry> _foodEntries;

        public FoodEntryRepository(IMongoDatabase database)
        {
            _foodEntries = database.GetCollection<FoodEntry>("FoodEntries");
        }

        public async Task<FoodEntry?> GetByIdAsync(string id)
        {
            return await _foodEntries.Find(entry => entry.Id == id).FirstOrDefaultAsync();
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
            
            return await _foodEntries.Find(filter)
                .SortByDescending(e => e.LoggedDateUtc)
                .ToListAsync();
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
            
            return await _foodEntries.Find(filter)
                .SortByDescending(e => e.LoggedDateUtc)
                .ToListAsync();
        }

        public async Task AddAsync(FoodEntry entry)
        {
            await _foodEntries.InsertOneAsync(entry);
        }

        public async Task UpdateAsync(FoodEntry entry)
        {
            await _foodEntries.ReplaceOneAsync(e => e.Id == entry.Id, entry);
        }

        public async Task DeleteAsync(string id)
        {
            await _foodEntries.DeleteOneAsync(entry => entry.Id == id);
        }
    }
}
