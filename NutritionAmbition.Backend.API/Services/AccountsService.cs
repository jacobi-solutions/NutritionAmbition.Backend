using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Repositories;
using NutritionAmbition.Backend.API.DataContracts;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using System;
using Microsoft.Extensions.Logging;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IAccountsService
    {
        Task<Account?> GetAccountByIdAsync(string id);
        Task<Account> CreateAccountAsync(Account account);
        Task<bool> UpdateAccountAsync(string id, Account updatedAccount);
        Task<bool> DeleteAccountAsync(string id);
        Task<AccountResponse> CreateAccountAsync(AccountRequest request, string googleAuthUserId);
        Task<Account> GetAccountByGoogleAuthIdAsync(string googleAuthUserId);
        Task<Account> CreateAnonymousAccountAsync();
        Task<MergeAnonymousAccountResponse> MergeAnonymousAccountAsync(string anonymousAccountId, string userAccountId);
    }

    public class AccountsService : IAccountsService
    {
        private readonly AccountsRepository _accountsRepo;
        private readonly ChatMessageRepository _chatMessageRepository;
        private readonly FoodEntryRepository _foodEntryRepository;
        private readonly ILogger<AccountsService> _logger;

        public AccountsService(
            AccountsRepository accountsRepo,
            ChatMessageRepository chatMessageRepository,
            FoodEntryRepository foodEntryRepository,
            ILogger<AccountsService> logger)
        {
            _accountsRepo = accountsRepo;
            _chatMessageRepository = chatMessageRepository;
            _foodEntryRepository = foodEntryRepository;
            _logger = logger;
        }

        public async Task<Account?> GetAccountByIdAsync(string id)
        {
            var response = new Response();
            try
            {
                return await _accountsRepo.GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                response.AddError(ex.Message, "EXCEPTION");
                return null;
            }
        }

        public async Task<Account> CreateAccountAsync(Account account)
        {
            var response = new Response();
            try
            {
                return await _accountsRepo.CreateAsync(account);
            }
            catch (Exception ex)
            {
                response.AddError(ex.Message, "EXCEPTION");
                return null;
            }
        }
        public async Task<bool> UpdateAccountAsync(string id, Account updatedAccount)
        {
            var response = new Response();
            try
            {
                return await _accountsRepo.UpdateAsync(id, updatedAccount);
            }
            catch (Exception ex)
            {
                response.AddError(ex.Message, "EXCEPTION");
                return false;
            }
        }
        public async Task<bool> DeleteAccountAsync(string id)
        {
            var response = new Response();
            try
            {
                return await _accountsRepo.DeleteAsync(id);
            }
            catch (Exception ex)
            {
                response.AddError(ex.Message, "EXCEPTION");
                return false;
            }
        }

        public async Task<AccountResponse> CreateAccountAsync(AccountRequest request, string googleAuthUserId)
        {
            var response = new AccountResponse();
            try
            {
                var existingAccount = await _accountsRepo.GetAccountByGoogleAuthUserIdAsync(googleAuthUserId);
                if (existingAccount != null)
                {
                    response.AddError("User already exists", "USER_ALREADY_EXISTS");
                    return response;
                }

                var account = new Account
                {
                    Name = request.Username,
                    GoogleAuthUserId = googleAuthUserId,
                    Email = request.Email
                };

                response.Account = await _accountsRepo.CreateAsync(account);
            }
            catch (Exception ex)
            {
                response.AddError(ex.Message, "EXCEPTION");
            }

            return response;
        }

        public async Task<Account> GetAccountByGoogleAuthIdAsync(string googleAuthUserId)
        {
            if (string.IsNullOrEmpty(googleAuthUserId))
            {
                return null;
            }

            return await _accountsRepo.GetAccountByGoogleAuthUserIdAsync(googleAuthUserId);
        }

        /// <summary>
        /// Creates a new anonymous account without requiring Firebase authentication
        /// </summary>
        /// <returns>The created anonymous account</returns>
        public async Task<Account> CreateAnonymousAccountAsync()
        {
            try
            {
                // Generate a unique name for the anonymous user
                string anonymousName = $"AnonymousUser_{Guid.NewGuid().ToString().Substring(0, 8)}";
                
                var account = new Account
                {
                    Name = anonymousName,
                    Email = $"{anonymousName}@anonymous.user",
                    GoogleAuthUserId = null, // No Google auth for anonymous users
                    IsAnonymousUser = true
                };

                return await _accountsRepo.CreateAsync(account);
            }
            catch (Exception ex)
            {
                // Log error but propagate exception to middleware for handling
                _logger.LogError(ex, "Error creating anonymous account");
                throw;
            }
        }

        /// <summary>
        /// Merges an anonymous account into a user account
        /// </summary>
        /// <param name="anonymousAccountId">The anonymous account ID to merge from</param>
        /// <param name="userAccountId">The user account ID to merge into</param>
        /// <returns>A response indicating success and migration counts</returns>
        public async Task<MergeAnonymousAccountResponse> MergeAnonymousAccountAsync(string anonymousAccountId, string userAccountId)
        {
            var response = new MergeAnonymousAccountResponse();

            try
            {
                _logger.LogInformation("Merging anonymous account {AnonymousAccountId} to user account {UserAccountId}", 
                    anonymousAccountId, userAccountId);
                    
                if (string.IsNullOrEmpty(anonymousAccountId))
                {
                    _logger.LogWarning("Cannot merge accounts: Anonymous account ID is null or empty");
                    response.AddError("Anonymous account ID is required.");
                    return response;
                }
                
                if (string.IsNullOrEmpty(userAccountId))
                {
                    _logger.LogWarning("Cannot merge accounts: User account ID is null or empty");
                    response.AddError("User account ID is required.");
                    return response;
                }
                
                if (anonymousAccountId == userAccountId)
                {
                    _logger.LogWarning("Cannot merge accounts: Anonymous account ID and user account ID are the same");
                    response.AddError("Anonymous account and user account cannot be the same.");
                    return response;
                }

                long chatCount = await _chatMessageRepository.UpdateAccountReferencesAsync(anonymousAccountId, userAccountId);
                _logger.LogInformation("Migrated {Count} chat messages from anonymous account {AnonymousAccountId} to user account {UserAccountId}",
                    chatCount, anonymousAccountId, userAccountId);
                    
                long foodCount = await _foodEntryRepository.UpdateAccountReferencesAsync(anonymousAccountId, userAccountId);
                _logger.LogInformation("Migrated {Count} food entries from anonymous account {AnonymousAccountId} to user account {UserAccountId}",
                    foodCount, anonymousAccountId, userAccountId);
                    
                await DeleteAccountAsync(anonymousAccountId);
                _logger.LogInformation("Successfully deleted anonymous account {AnonymousAccountId} after migration", anonymousAccountId);

                response.IsSuccess = true;
                response.ChatMessagesMigrated = chatCount;
                response.FoodEntriesMigrated = foodCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error merging anonymous account {AnonymousAccountId} into user account {UserAccountId}: {ErrorMessage}", 
                    anonymousAccountId, userAccountId, ex.Message);
                response.AddError("Failed to merge anonymous account data.");
            }

            return response;
        }
    }
}
