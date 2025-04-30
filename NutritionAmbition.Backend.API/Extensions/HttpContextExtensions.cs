using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Services;
using System.Linq;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Extensions
{
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Gets the Account object from HttpContext, handling both Firebase and anonymous authentication
        /// </summary>
        /// <param name="context">The HttpContext</param>
        /// <param name="accountsService">The AccountsService</param>
        /// <param name="logger">Optional logger for diagnostic information</param>
        /// <returns>The Account if found, otherwise null</returns>
        public static async Task<Account> GetAccountFromContextAsync(
            this HttpContext context, 
            AccountsService accountsService,
            ILogger logger = null)
        {
            // First, check if account is already in the context (set by AnonymousAuthMiddleware)
            if (context.Items.TryGetValue("Account", out var accountObj) && accountObj is Account account)
            {
                logger?.LogInformation("Found account in HttpContext.Items: {AccountId}", account.Id);
                return account;
            }

            // If not, try to get it from Firebase auth
            var googleAuthUserId = context.User?.Claims.FirstOrDefault(x => x.Type == "user_id")?.Value;
            if (!string.IsNullOrEmpty(googleAuthUserId))
            {
                var accountFromFirebase = await accountsService.GetAccountByGoogleAuthIdAsync(googleAuthUserId);
                if (accountFromFirebase != null)
                {
                    logger?.LogInformation("Found account using Firebase auth: {AccountId}", accountFromFirebase.Id);
                    return accountFromFirebase;
                }
                else
                {
                    logger?.LogWarning("Firebase user found but no matching account: {GoogleAuthId}", googleAuthUserId);
                }
            }

            logger?.LogWarning("No account found in context");
            return null;
        }
    }
} 