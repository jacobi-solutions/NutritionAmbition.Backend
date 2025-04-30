using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using NutritionAmbition.Backend.API.Models;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace NutritionAmbition.Backend.API.Services
{
    public class DataMigrationService
    {
        private readonly IMongoDatabase _database;
        private readonly ILogger<DataMigrationService> _logger;

        public DataMigrationService(
            IMongoDatabase database,
            ILogger<DataMigrationService> logger)
        {
            _database = database;
            _logger = logger;
        }

        public async Task MigrateToNewChatMessages()
        {
            try
            {
                _logger.LogInformation("Starting migration to new ChatMessages format");
                
                // Check if ChatMessages collection exists
                var collections = await (await _database.ListCollectionNamesAsync()).ToListAsync();
                if (!collections.Contains("ChatMessages"))
                {
                    _logger.LogInformation("No ChatMessages collection found, skipping migration");
                    return;
                }
                
                // Get all chat messages
                var chatMessagesCollection = _database.GetCollection<BsonDocument>("ChatMessages");
                var chatMessages = await chatMessagesCollection.Find(new BsonDocument()).ToListAsync();
                
                if (chatMessages.Count == 0)
                {
                    _logger.LogInformation("No ChatMessages found, skipping migration");
                    return;
                }
                
                _logger.LogInformation("Found {0} ChatMessages to migrate", chatMessages.Count);
                
                // Create ChatMessages collection if it doesn't exist
                var newChatMessagesCollection = _database.GetCollection<ChatMessage>("ChatMessages");
                
                // Create indexes for the ChatMessages collection
                var indexKeysAccount = Builders<ChatMessage>.IndexKeys.Ascending(cm => cm.AccountId);
                var indexOptionsAccount = new CreateIndexOptions { Name = "AccountId_Index" };
                await newChatMessagesCollection.Indexes.CreateOneAsync(new CreateIndexModel<ChatMessage>(indexKeysAccount, indexOptionsAccount));

                // Compound index for AccountId and FoodEntryId
                var indexKeysCompound = Builders<ChatMessage>.IndexKeys
                    .Ascending(cm => cm.AccountId)
                    .Ascending(cm => cm.FoodEntryId);
                var indexOptionsCompound = new CreateIndexOptions { Name = "AccountId_FoodEntryId_Index" };
                await newChatMessagesCollection.Indexes.CreateOneAsync(new CreateIndexModel<ChatMessage>(indexKeysCompound, indexOptionsCompound));
                
                // Compound index for AccountId and LoggedDateUtc
                var indexKeysDate = Builders<ChatMessage>.IndexKeys
                    .Ascending(cm => cm.AccountId)
                    .Ascending(cm => cm.LoggedDateUtc);
                var indexOptionsDate = new CreateIndexOptions { Name = "AccountId_LoggedDateUtc_Index" };
                await newChatMessagesCollection.Indexes.CreateOneAsync(new CreateIndexModel<ChatMessage>(indexKeysDate, indexOptionsDate));
                
                int migratedCount = 0;
                // Migrate each chat message
                foreach (var oldMessage in chatMessages)
                {
                    try
                    {
                        var chatMessage = new ChatMessage
                        {
                            Id = oldMessage["_id"].AsObjectId.ToString(),
                            AccountId = oldMessage["accountId"].AsString,
                            FoodEntryId = oldMessage.Contains("foodEntryId") ? oldMessage["foodEntryId"].AsString : null,
                            Content = oldMessage.Contains("message") ? oldMessage["message"].AsString : "",
                            Role = MessageRole.Assistant, // Assume all coach messages are from the assistant
                            LoggedDateUtc = oldMessage.Contains("loggedDateUtc") ? 
                                oldMessage["loggedDateUtc"].ToLocalTime().ToUniversalTime() : 
                                (oldMessage.Contains("timestampUtc") ? 
                                    oldMessage["timestampUtc"].ToLocalTime().ToUniversalTime() : 
                                    DateTime.UtcNow),
                            IsRead = oldMessage.Contains("isRead") ? oldMessage["isRead"].AsBoolean : false
                        };
                        
                        await newChatMessagesCollection.InsertOneAsync(chatMessage);
                        migratedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error migrating message {0}", oldMessage["_id"]);
                    }
                }
                
                _logger.LogInformation("Successfully migrated {0} messages", migratedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during ChatMessages migration");
            }
        }
    }
} 