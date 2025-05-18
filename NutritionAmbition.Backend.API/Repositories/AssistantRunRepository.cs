using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Settings;

namespace NutritionAmbition.Backend.API.Repositories
{
    public class AssistantRunRepository
    {
        private readonly IMongoCollection<AssistantRun> _collection;
        private readonly ILogger<AssistantRunRepository> _logger;

        public AssistantRunRepository(
            IMongoDatabase database,
            ILogger<AssistantRunRepository> logger)
        {
            _collection = database.GetCollection<AssistantRun>("AssistantRuns");
            _logger = logger;
        }

        public async Task<string> InsertRunAsync(AssistantRun run)
        {
            try
            {
                await _collection.InsertOneAsync(run);
                return run.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting run for account {AccountId}", run.AccountId);
                throw;
            }
        }

        public async Task<bool> UpdateRunStatusAsync(string runId, string status)
        {
            try
            {
                var updateDefinition = Builders<AssistantRun>.Update
                    .Set(r => r.Status, status);

                if (status == "completed" || status == "failed" || status == "cancelled" || status == "expired")
                {
                    updateDefinition = updateDefinition.Set(r => r.CompletedAt, DateTime.UtcNow);
                }

                var result = await _collection.UpdateOneAsync(r => r.RunId == runId, updateDefinition);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for run {RunId}", runId);
                throw;
            }
        }

        public async Task<AssistantRun> GetLatestActiveRunByAccountIdAsync(string accountId)
        {
            try
            {
                return await _collection
                    .Find(r => r.AccountId == accountId && r.Status == "in_progress")
                    .SortByDescending(r => r.StartedAt)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest active run for account {AccountId}", accountId);
                throw;
            }
        }

        public async Task<bool> HasActiveRunAsync(string accountId)
        {
            try
            {
                var count = await _collection
                    .CountDocumentsAsync(r => r.AccountId == accountId && r.Status == "in_progress");
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for active run for account {AccountId}", accountId);
                throw;
            }
        }

        public async Task ExpireStaleRunsAsync(string accountId, int timeoutMinutes)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-timeoutMinutes);
            var filter = Builders<AssistantRun>.Filter.And(
                Builders<AssistantRun>.Filter.Eq(r => r.AccountId, accountId),
                Builders<AssistantRun>.Filter.Eq(r => r.Status, "in_progress"),
                Builders<AssistantRun>.Filter.Lt(r => r.StartedAt, cutoff)
            );

            var update = Builders<AssistantRun>.Update
                .Set(r => r.Status, "expired")
                .Set(r => r.LastUpdatedDateUtc, DateTime.UtcNow);

            await _collection.UpdateManyAsync(filter, update);
        }

    }
} 