using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NutritionAmbition.Backend.API.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Repositories
{
    public class ChatMessageRepository
    {
        private readonly IMongoCollection<ChatMessage> _chatMessages;
        private readonly ILogger<ChatMessageRepository> _logger;

        public ChatMessageRepository(IMongoDatabase database, ILogger<ChatMessageRepository> logger)
        {
            _chatMessages = database.GetCollection<ChatMessage>("ChatMessages");
            _logger = logger;

            // Create indexes for efficient querying
            var indexKeysAccount = Builders<ChatMessage>.IndexKeys.Ascending(cm => cm.AccountId);
            var indexOptionsAccount = new CreateIndexOptions { Name = "AccountId_Index" };
            _chatMessages.Indexes.CreateOne(new CreateIndexModel<ChatMessage>(indexKeysAccount, indexOptionsAccount));

            // Compound index for AccountId and FoodEntryId
            var indexKeysCompound = Builders<ChatMessage>.IndexKeys
                .Ascending(cm => cm.AccountId)
                .Ascending(cm => cm.FoodEntryId);
            var indexOptionsCompound = new CreateIndexOptions { Name = "AccountId_FoodEntryId_Index" };
            _chatMessages.Indexes.CreateOne(new CreateIndexModel<ChatMessage>(indexKeysCompound, indexOptionsCompound));
            
            // Compound index for AccountId and LoggedDateUtc
            var indexKeysDate = Builders<ChatMessage>.IndexKeys
                .Ascending(cm => cm.AccountId)
                .Ascending(cm => cm.LoggedDateUtc);
            var indexOptionsDate = new CreateIndexOptions { Name = "AccountId_LoggedDateUtc_Index" };
            _chatMessages.Indexes.CreateOne(new CreateIndexModel<ChatMessage>(indexKeysDate, indexOptionsDate));
        }

        public async Task<string> AddAsync(ChatMessage chatMessage)
        {
            try
            {
                _logger.LogInformation("Adding chat message for account {AccountId} and food entry {FoodEntryId}", 
                    chatMessage.AccountId, chatMessage.FoodEntryId);
                
                await _chatMessages.InsertOneAsync(chatMessage);
                
                _logger.LogInformation("Successfully added chat message with ID {Id}", chatMessage.Id);
                
                return chatMessage.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding chat message for account {AccountId} and food entry {FoodEntryId}", 
                    chatMessage.AccountId, chatMessage.FoodEntryId);
                
                throw;
            }
        }

        public async Task<List<ChatMessage>> GetByAccountIdAsync(string accountId)
        {
            try
            {
                _logger.LogInformation("Retrieving chat messages for account {AccountId}", accountId);
                
                var messages = await _chatMessages
                    .Find(cm => cm.AccountId == accountId)
                    .SortByDescending(cm => cm.LoggedDateUtc)
                    .ToListAsync();
                
                _logger.LogInformation("Retrieved {Count} chat messages for account {AccountId}", 
                    messages.Count, accountId);
                
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat messages for account {AccountId}", accountId);
                
                throw;
            }
        }

        public async Task<List<ChatMessage>> GetByFoodEntryIdAsync(string foodEntryId)
        {
            try
            {
                _logger.LogInformation("Retrieving chat messages for food entry {FoodEntryId}", foodEntryId);
                
                var messages = await _chatMessages
                    .Find(cm => cm.FoodEntryId == foodEntryId)
                    .SortByDescending(cm => cm.LoggedDateUtc)
                    .ToListAsync();
                
                _logger.LogInformation("Retrieved {Count} chat messages for food entry {FoodEntryId}", 
                    messages.Count, foodEntryId);
                
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat messages for food entry {FoodEntryId}", foodEntryId);
                
                throw;
            }
        }
        
        public async Task<List<ChatMessage>> GetByDateAsync(string accountId, DateTime loggedDate)
        {
            try
            {
                _logger.LogInformation("Retrieving chat messages for account {AccountId} on date {LoggedDate}", 
                    accountId, loggedDate.Date);
                
                // Create a date range filter for the entire day
                var startOfDay = loggedDate.Date;
                var endOfDay = startOfDay.AddDays(1).AddTicks(-1);
                
                var filter = Builders<ChatMessage>.Filter.Eq(cm => cm.AccountId, accountId) &
                             Builders<ChatMessage>.Filter.Gte(cm => cm.LoggedDateUtc, startOfDay) &
                             Builders<ChatMessage>.Filter.Lte(cm => cm.LoggedDateUtc, endOfDay);
                
                var messages = await _chatMessages
                    .Find(filter)
                    .SortByDescending(cm => cm.LoggedDateUtc)
                    .ToListAsync();
                
                _logger.LogInformation("Retrieved {Count} chat messages for account {AccountId} on date {LoggedDate}", 
                    messages.Count, accountId, loggedDate.Date);
                
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat messages for account {AccountId} on date {LoggedDate}", 
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
                    
                    var filter = Builders<ChatMessage>.Filter.Eq(cm => cm.AccountId, accountId) &
                                 Builders<ChatMessage>.Filter.Gte(cm => cm.LoggedDateUtc, startOfDay) &
                                 Builders<ChatMessage>.Filter.Lte(cm => cm.LoggedDateUtc, endOfDay);
                    
                    _logger.LogInformation("Deleting chat messages for account {AccountId} on date {LoggedDate}", 
                        accountId, loggedDate.Value.Date);
                    
                    result = await _chatMessages.DeleteManyAsync(filter);
                    
                    _logger.LogInformation("Deleted {Count} chat messages for account {AccountId} on date {LoggedDate}", 
                        result.DeletedCount, accountId, loggedDate.Value.Date);
                }
                else
                {
                    // Delete all messages for this account
                    var filter = Builders<ChatMessage>.Filter.Eq(cm => cm.AccountId, accountId);
                    
                    _logger.LogInformation("Deleting ALL chat messages for account {AccountId}", accountId);
                    
                    result = await _chatMessages.DeleteManyAsync(filter);
                    
                    _logger.LogInformation("Deleted {Count} chat messages for account {AccountId}", 
                        result.DeletedCount, accountId);
                }
                
                return (int)result.DeletedCount;
            }
            catch (Exception ex)
            {
                if (loggedDate.HasValue)
                {
                    _logger.LogError(ex, "Error deleting chat messages for account {AccountId} on date {LoggedDate}", 
                        accountId, loggedDate.Value.Date);
                }
                else
                {
                    _logger.LogError(ex, "Error deleting ALL chat messages for account {AccountId}", accountId);
                }
                
                throw;
            }
        }
    }
} 