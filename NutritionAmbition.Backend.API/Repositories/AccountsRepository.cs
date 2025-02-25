using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Repositories
{
    public class AccountsRepository
    {
        private readonly IMongoCollection<Account> _collection;
        private readonly MongoDBSettings _mongoDBSettings;

        public AccountsRepository(IMongoDatabase database, MongoDBSettings mongoDbSettings)
        {
            _mongoDBSettings = mongoDbSettings;
            _collection = database.GetCollection<Account>(mongoDbSettings.AccountsCollectionName);      
        }

        public async Task<List<Account>> GetAllAsync()
        {
            return await _collection.Find(_ => true).ToListAsync();
        }

        public async Task<Account?> GetByIdAsync(string id)
        {
            return await _collection.Find(a => a.Id == id).FirstOrDefaultAsync();
        }

        public async Task<Account> CreateAsync(Account account)
        {
            await _collection.InsertOneAsync(account);
            return account;
        }

        public async Task<bool> UpdateAsync(string id, Account updatedAccount)
        {
            var result = await _collection.ReplaceOneAsync(a => a.Id == id, updatedAccount);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _collection.DeleteOneAsync(a => a.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<Account?> GetAccountByGoogleAuthUserIdAsync(string googleAuthUserId)
        {
            return await _collection.Find(a => a.GoogleAuthUserId == googleAuthUserId).FirstOrDefaultAsync();
        }
    }
}
