using MongoDB.Driver;
using System;
using System.Threading.Tasks;
using NutritionAmbition.Backend.API.Models;
using Microsoft.Extensions.Logging;

namespace NutritionAmbition.Backend.API.Repositories
{
    public class DailyGoalRepository
    {
        private readonly IMongoCollection<DailyGoal> _dailyGoals;
        private readonly ILogger<DailyGoalRepository> _logger;

        public DailyGoalRepository(IMongoDatabase database, ILogger<DailyGoalRepository> logger)
        {
            _dailyGoals = database.GetCollection<DailyGoal>("DailyGoals");
            _logger = logger;
            
            // Create index on AccountId and EffectiveDateUtc for efficient queries
            var indexKeysDefinition = Builders<DailyGoal>.IndexKeys
                .Ascending(goal => goal.AccountId)
                .Ascending(goal => goal.EffectiveDateUtc);
                
            var indexOptions = new CreateIndexOptions { Unique = true };
            var indexModel = new CreateIndexModel<DailyGoal>(indexKeysDefinition, indexOptions);
            
            _dailyGoals.Indexes.CreateOne(indexModel);
        }

        public async Task<DailyGoal> GetByAccountIdAndDateAsync(string accountId, DateTime date)
        {
            try
            {
                var dateWithoutTime = date.Date;
                
                var filter = Builders<DailyGoal>.Filter.Eq(goal => goal.AccountId, accountId) &
                            Builders<DailyGoal>.Filter.Lte(goal => goal.EffectiveDateUtc, dateWithoutTime);
                
                // Get the most recent goal that is effective on or before the specified date
                return await _dailyGoals.Find(filter)
                    .SortByDescending(goal => goal.EffectiveDateUtc)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily goal for account {AccountId} and date {Date}", accountId, date);
                throw;
            }
        }

        public async Task<bool> UpsertAsync(DailyGoal goal)
        {
            try
            {
                // Ensure date portion only
                goal.EffectiveDateUtc = goal.EffectiveDateUtc.Date;
                
                // Update the LastUpdatedDateUtc
                goal.LastUpdatedDateUtc = DateTime.UtcNow;
                
                var filter = Builders<DailyGoal>.Filter.Eq(g => g.AccountId, goal.AccountId) &
                             Builders<DailyGoal>.Filter.Eq(g => g.EffectiveDateUtc, goal.EffectiveDateUtc);
                
                var options = new ReplaceOptions { IsUpsert = true };
                
                var result = await _dailyGoals.ReplaceOneAsync(filter, goal, options);
                
                return result.IsAcknowledged && (result.ModifiedCount > 0 || result.UpsertedId != null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting daily goal for account {AccountId}", goal.AccountId);
                throw;
            }
        }
    }
} 