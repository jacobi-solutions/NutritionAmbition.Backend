using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Repositories;
using NutritionAmbition.Backend.API.DataContracts;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using System;

namespace NutritionAmbition.Backend.API.Services
{
    public class AccountsService
    {
        private readonly AccountsRepository _accountsRepo;

        public AccountsService(AccountsRepository accountsRepo)
        {
            _accountsRepo = accountsRepo;
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
                Console.WriteLine($"Error creating anonymous account: {ex.Message}");
                throw;
            }
        }
    }
}
