using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NutritionAmbition.Backend.API.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NutritionAmbition.Backend.API.Settings;

namespace NutritionAmbition.Backend.API.Repositories
{
    public class ChatMessageRepository
    {
        private readonly IMongoCollection<ChatMessage> _collection;
        private readonly MongoDBSettings _mongoDBSettings;
        private readonly ILogger<ChatMessageRepository> _logger;

        public ChatMessageRepository(IMongoDatabase database, ILogger<ChatMessageRepository> logger, MongoDBSettings mongoDbSettings)
        {
            _mongoDBSettings = mongoDbSettings;
            _collection = database.GetCollection<ChatMessage>(mongoDbSettings.ChatMessagesCollectionName);  
            _logger = logger;

            // Create indexes for efficient querying
            var indexKeysAccount = Builders<ChatMessage>.IndexKeys.Ascending(cm => cm.AccountId);
            var indexOptionsAccount = new CreateIndexOptions { Name = "AccountId_Index" };
            _collection.Indexes.CreateOne(new CreateIndexModel<ChatMessage>(indexKeysAccount, indexOptionsAccount));

            // Compound index for AccountId and FoodEntryId
            var indexKeysCompound = Builders<ChatMessage>.IndexKeys
                .Ascending(cm => cm.AccountId)
                .Ascending(cm => cm.FoodEntryId);
            var indexOptionsCompound = new CreateIndexOptions { Name = "AccountId_FoodEntryId_Index" };
            _collection.Indexes.CreateOne(new CreateIndexModel<ChatMessage>(indexKeysCompound, indexOptionsCompound));
            
            // Compound index for AccountId and LoggedDateUtc
            var indexKeysDate = Builders<ChatMessage>.IndexKeys
                .Ascending(cm => cm.AccountId)
                .Ascending(cm => cm.LoggedDateUtc);
            var indexOptionsDate = new CreateIndexOptions { Name = "AccountId_LoggedDateUtc_Index" };
            _collection.Indexes.CreateOne(new CreateIndexModel<ChatMessage>(indexKeysDate, indexOptionsDate));
        }

        public async Task<string> AddAsync(ChatMessage chatMessage)
        {
            try
            {
                _logger.LogInformation("Adding chat message for account {AccountId} and food entry {FoodEntryId}", 
                    chatMessage.AccountId, chatMessage.FoodEntryId);
                
                await _collection.InsertOneAsync(chatMessage);
                
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
                
                var messages = await _collection
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
                
                var messages = await _collection
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
        
        public async Task<List<ChatMessage>> GetByDateAsync(string accountId, DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            var filter = Builders<ChatMessage>.Filter.And(
                Builders<ChatMessage>.Filter.Eq(m => m.AccountId, accountId),
                Builders<ChatMessage>.Filter.Gte(m => m.LoggedDateUtc, startOfDay),
                Builders<ChatMessage>.Filter.Lt(m => m.LoggedDateUtc, endOfDay)
            );

            return await _collection.Find(filter)
                .SortBy(m => m.LoggedDateUtc)
                .ToListAsync();
        }
        
        public async Task<long> DeleteByDateAsync(string accountId, DateTime? date)
        {
            var filter = Builders<ChatMessage>.Filter.Eq(m => m.AccountId, accountId);

            if (date.HasValue)
            {
                var startOfDay = date.Value.Date;
                var endOfDay = startOfDay.AddDays(1);

                filter = Builders<ChatMessage>.Filter.And(
                    filter,
                    Builders<ChatMessage>.Filter.Gte(m => m.LoggedDateUtc, startOfDay),
                    Builders<ChatMessage>.Filter.Lt(m => m.LoggedDateUtc, endOfDay)
                );
            }

            var result = await _collection.DeleteManyAsync(filter);
            return result.DeletedCount;
        }

        public async Task<long> UpdateAccountReferencesAsync(string fromAccountId, string toAccountId)
        {
            var filter = Builders<ChatMessage>.Filter.Eq(x => x.AccountId, fromAccountId);
            var update = Builders<ChatMessage>.Update.Set(x => x.AccountId, toAccountId);
            var result = await _collection.UpdateManyAsync(filter, update);
            return result.ModifiedCount;
        }
    }
} 