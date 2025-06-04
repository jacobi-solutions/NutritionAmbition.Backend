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
        Task<bool> UpdateAccountAsync(Account updatedAccount);
        Task<bool> DeleteAccountAsync(string id);
        Task<AccountResponse> CreateAccountAsync(AccountRequest request, string googleAuthUserId);
        Task<Account> GetAccountByGoogleAuthIdAsync(string googleAuthUserId);
        Task<Account> GetOrCreateByGoogleAuthIdAsync(string googleAuthUserId, string email);
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

        public async Task<bool> UpdateAccountAsync(Account updatedAccount)
        {
            var response = new Response();
            try
            {
                return await _accountsRepo.UpdateAsync(updatedAccount);
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

        

        public async Task<Account> GetOrCreateByGoogleAuthIdAsync(string googleAuthUserId, string email = null)
        {
            if (string.IsNullOrEmpty(googleAuthUserId))
            {
                _logger.LogWarning("Empty Google Auth User ID provided");
                return null;
            }

            try
            {
                var existingAccount = await _accountsRepo.GetAccountByGoogleAuthUserIdAsync(googleAuthUserId);

                if (existingAccount != null)
                {
                    // Upgrade account if it was anonymous and now has email
                    if (existingAccount.IsAnonymousUser && !string.IsNullOrEmpty(email))
                    {
                        _logger.LogInformation("Upgrading anonymous account to full account with email: {Email}", email);
                        existingAccount.Email = email;
                        existingAccount.IsAnonymousUser = false;
                        await _accountsRepo.UpdateAsync(existingAccount);
                    }

                    return existingAccount;
                }

                // New account
                var isAnonymous = string.IsNullOrEmpty(email);
                var name = isAnonymous
                    ? $"AnonymousUser_{Guid.NewGuid().ToString().Substring(0, 8)}"
                    : email.Split('@')[0];
                email = isAnonymous
                    ? $"{name}@anonymous.user"
                    : email;

                var newAccount = new Account
                {
                    Name = name,
                    Email = email,
                    GoogleAuthUserId = googleAuthUserId,
                    IsAnonymousUser = isAnonymous
                };

                return await _accountsRepo.CreateAsync(newAccount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrCreateByGoogleAuthIdAsync for Google Auth User ID: {GoogleAuthUserId}", googleAuthUserId);
                throw;
            }
        }

    }
}
