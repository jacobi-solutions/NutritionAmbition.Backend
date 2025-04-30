using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NutritionAmbition.Backend.API.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Repositories
{
    public class CoachMessageRepository
    {
        private readonly IMongoCollection<CoachMessage> _coachMessages;
        private readonly ILogger<CoachMessageRepository> _logger;

        public CoachMessageRepository(IMongoDatabase database, ILogger<CoachMessageRepository> logger)
        {
            _coachMessages = database.GetCollection<CoachMessage>("CoachMessages");
            _logger = logger;

            // Create indexes for efficient querying
            var indexKeysAccount = Builders<CoachMessage>.IndexKeys.Ascending(cm => cm.AccountId);
            var indexOptionsAccount = new CreateIndexOptions { Name = "AccountId_Index" };
            _coachMessages.Indexes.CreateOne(new CreateIndexModel<CoachMessage>(indexKeysAccount, indexOptionsAccount));

            // Compound index for AccountId and FoodEntryId
            var indexKeysCompound = Builders<CoachMessage>.IndexKeys
                .Ascending(cm => cm.AccountId)
                .Ascending(cm => cm.FoodEntryId);
            var indexOptionsCompound = new CreateIndexOptions { Name = "AccountId_FoodEntryId_Index" };
            _coachMessages.Indexes.CreateOne(new CreateIndexModel<CoachMessage>(indexKeysCompound, indexOptionsCompound));
            
            // Compound index for AccountId and LoggedDateUtc
            var indexKeysDate = Builders<CoachMessage>.IndexKeys
                .Ascending(cm => cm.AccountId)
                .Ascending(cm => cm.LoggedDateUtc);
            var indexOptionsDate = new CreateIndexOptions { Name = "AccountId_LoggedDateUtc_Index" };
            _coachMessages.Indexes.CreateOne(new CreateIndexModel<CoachMessage>(indexKeysDate, indexOptionsDate));
        }

        public async Task<string> AddAsync(CoachMessage coachMessage)
        {
            try
            {
                // Ensure timestamp is set if not already
                if (coachMessage.TimestampUtc == default)
                {
                    coachMessage.TimestampUtc = DateTime.UtcNow;
                }

                _logger.LogInformation("Adding coach message for account {AccountId} and food entry {FoodEntryId}", 
                    coachMessage.AccountId, coachMessage.FoodEntryId);
                
                await _coachMessages.InsertOneAsync(coachMessage);
                
                _logger.LogInformation("Successfully added coach message with ID {Id}", coachMessage.Id);
                
                return coachMessage.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding coach message for account {AccountId} and food entry {FoodEntryId}", 
                    coachMessage.AccountId, coachMessage.FoodEntryId);
                
                throw;
            }
        }

        public async Task<List<CoachMessage>> GetByAccountIdAsync(string accountId)
        {
            try
            {
                _logger.LogInformation("Retrieving coach messages for account {AccountId}", accountId);
                
                var messages = await _coachMessages
                    .Find(cm => cm.AccountId == accountId)
                    .SortByDescending(cm => cm.TimestampUtc)
                    .ToListAsync();
                
                _logger.LogInformation("Retrieved {Count} coach messages for account {AccountId}", 
                    messages.Count, accountId);
                
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving coach messages for account {AccountId}", accountId);
                
                throw;
            }
        }

        public async Task<List<CoachMessage>> GetByFoodEntryIdAsync(string foodEntryId)
        {
            try
            {
                _logger.LogInformation("Retrieving coach messages for food entry {FoodEntryId}", foodEntryId);
                
                var messages = await _coachMessages
                    .Find(cm => cm.FoodEntryId == foodEntryId)
                    .SortByDescending(cm => cm.TimestampUtc)
                    .ToListAsync();
                
                _logger.LogInformation("Retrieved {Count} coach messages for food entry {FoodEntryId}", 
                    messages.Count, foodEntryId);
                
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving coach messages for food entry {FoodEntryId}", foodEntryId);
                
                throw;
            }
        }
        
        public async Task<List<CoachMessage>> GetByDateAsync(string accountId, DateTime loggedDate)
        {
            try
            {
                _logger.LogInformation("Retrieving coach messages for account {AccountId} on date {LoggedDate}", 
                    accountId, loggedDate.Date);
                
                // Create a date range filter for the entire day
                var startOfDay = loggedDate.Date;
                var endOfDay = startOfDay.AddDays(1).AddTicks(-1);
                
                var filter = Builders<CoachMessage>.Filter.Eq(cm => cm.AccountId, accountId) &
                             Builders<CoachMessage>.Filter.Gte(cm => cm.LoggedDateUtc, startOfDay) &
                             Builders<CoachMessage>.Filter.Lte(cm => cm.LoggedDateUtc, endOfDay);
                
                var messages = await _coachMessages
                    .Find(filter)
                    .SortByDescending(cm => cm.TimestampUtc)
                    .ToListAsync();
                
                _logger.LogInformation("Retrieved {Count} coach messages for account {AccountId} on date {LoggedDate}", 
                    messages.Count, accountId, loggedDate.Date);
                
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving coach messages for account {AccountId} on date {LoggedDate}", 
                    accountId, loggedDate.Date);
                
                throw;
            }
        }
        
        public async Task<int> DeleteByDateAsync(string accountId, DateTime? loggedDate)
        {
            try
            {
                DeleteResult result;
                
                if (loggedDate.HasValue)
                {
                    // Create a date range filter for the entire day
                    var startOfDay = loggedDate.Value.Date;
                    var endOfDay = startOfDay.AddDays(1).AddTicks(-1);
                    
                    var filter = Builders<CoachMessage>.Filter.Eq(cm => cm.AccountId, accountId) &
                                 Builders<CoachMessage>.Filter.Gte(cm => cm.LoggedDateUtc, startOfDay) &
                                 Builders<CoachMessage>.Filter.Lte(cm => cm.LoggedDateUtc, endOfDay);
                    
                    _logger.LogInformation("Deleting coach messages for account {AccountId} on date {LoggedDate}", 
                        accountId, loggedDate.Value.Date);
                    
                    result = await _coachMessages.DeleteManyAsync(filter);
                    
                    _logger.LogInformation("Deleted {Count} coach messages for account {AccountId} on date {LoggedDate}", 
                        result.DeletedCount, accountId, loggedDate.Value.Date);
                }
                else
                {
                    // Delete all messages for this account
                    var filter = Builders<CoachMessage>.Filter.Eq(cm => cm.AccountId, accountId);
                    
                    _logger.LogInformation("Deleting ALL coach messages for account {AccountId}", accountId);
                    
                    result = await _coachMessages.DeleteManyAsync(filter);
                    
                    _logger.LogInformation("Deleted {Count} coach messages for account {AccountId}", 
                        result.DeletedCount, accountId);
                }
                
                return (int)result.DeletedCount;
            }
            catch (Exception ex)
            {
                if (loggedDate.HasValue)
                {
                    _logger.LogError(ex, "Error deleting coach messages for account {AccountId} on date {LoggedDate}", 
                        accountId, loggedDate.Value.Date);
                }
                else
                {
                    _logger.LogError(ex, "Error deleting ALL coach messages for account {AccountId}", accountId);
                }
                
                throw;
            }
        }
    }
} 