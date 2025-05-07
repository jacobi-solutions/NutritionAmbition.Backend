using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Services;
using NutritionAmbition.Backend.API.Attributes;

namespace NutritionAmbition.Backend.API.Extensions
{
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Gets the Account object from HttpContext, handling both Firebase and anonymous authentication
        /// </summary>
        /// <param name="context">The HttpContext</param>
        /// <param name="accountsService">The AccountsService</param>
        /// <returns>The Account if found, otherwise null</returns>
        public static async Task<Account> GetAccountFromContextAsync(this HttpContext context, AccountsService accountsService)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<FlexibleAuthorizeAttribute>>();

            try
            {
                // Get the account ID from the claims
                var accountIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (accountIdClaim == null)
                {
                    logger.LogWarning("No account ID claim found in the token");
                    return null;
                }

                var accountId = accountIdClaim.Value;
                logger.LogInformation("Retrieved account ID from token: {AccountId}", accountId);

                // Get the account from the database
                var account = await accountsService.GetAccountByIdAsync(accountId);
                if (account == null)
                {
                    logger.LogWarning("Account not found for ID: {AccountId}", accountId);
                    return null;
                }

                logger.LogInformation("Successfully retrieved account for ID: {AccountId}", accountId);
                return account;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving account from context");
                return null;
            }
        }
    }
} 