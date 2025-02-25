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

        public async Task<List<FoodEntry>> GetByAccountIdAsync(string accountId, DateTime? date = null)
        {
            var filter = Builders<FoodEntry>.Filter.Eq(entry => entry.AccountId, accountId);
            if (date.HasValue)
            {
                filter &= Builders<FoodEntry>.Filter.Eq(entry => entry.LoggedDateUtc.Date, date.Value.Date);
            }
            return await _foodEntries.Find(filter).ToListAsync();
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