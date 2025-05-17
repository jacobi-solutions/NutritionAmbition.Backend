using MongoDB.Driver;
using System;
using System.Threading.Tasks;
using NutritionAmbition.Backend.API.Models;
using Microsoft.Extensions.Logging;

namespace NutritionAmbition.Backend.API.Repositories
{
    public class DefaultGoalProfileRepository
    {
        private readonly IMongoCollection<DefaultGoalProfile> _defaultGoalProfiles;
        private readonly ILogger<DefaultGoalProfileRepository> _logger;

        public DefaultGoalProfileRepository(IMongoDatabase database, ILogger<DefaultGoalProfileRepository> logger)
        {
            _defaultGoalProfiles = database.GetCollection<DefaultGoalProfile>("DefaultGoalProfiles");
            _logger = logger;
            
            // Create unique index on AccountId for efficient queries
            var indexKeysDefinition = Builders<DefaultGoalProfile>.IndexKeys.Ascending(profile => profile.AccountId);
            var indexOptions = new CreateIndexOptions { Unique = true };
            var indexModel = new CreateIndexModel<DefaultGoalProfile>(indexKeysDefinition, indexOptions);
            
            _defaultGoalProfiles.Indexes.CreateOne(indexModel);
        }

        public async Task<DefaultGoalProfile?> GetByAccountIdAsync(string accountId)
        {
            try
            {
                return await _defaultGoalProfiles.Find(x => x.AccountId == accountId).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default goal profile for account {AccountId}", accountId);
                throw;
            }
        }

        public async Task<bool> UpsertAsync(DefaultGoalProfile profile)
        {
            try
            {
                // Update the LastUpdatedDateUtc
                profile.LastUpdatedDateUtc = DateTime.UtcNow;
                
                // If this is a new profile, set the creation date
                if (profile.CreatedDateUtc == default)
                {
                    profile.CreatedDateUtc = DateTime.UtcNow;
                }
                
                var filter = Builders<DefaultGoalProfile>.Filter.Eq(p => p.AccountId, profile.AccountId);
                var options = new ReplaceOptions { IsUpsert = true };
                
                var result = await _defaultGoalProfiles.ReplaceOneAsync(filter, profile, options);
                
                return result.IsAcknowledged && (result.ModifiedCount > 0 || result.UpsertedId != null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting default goal profile for account {AccountId}", profile.AccountId);
                throw;
            }
        }
    }
} 