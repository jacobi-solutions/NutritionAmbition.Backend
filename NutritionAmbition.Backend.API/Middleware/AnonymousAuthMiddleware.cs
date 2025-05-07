using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Services;

namespace NutritionAmbition.Backend.API.Middleware
{
    public class AnonymousAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly AccountsService _accountsService;
        private readonly ILogger<AnonymousAuthMiddleware> _logger;

        public AnonymousAuthMiddleware(
            RequestDelegate next,
            AccountsService accountsService,
            ILogger<AnonymousAuthMiddleware> logger)
        {
            _next = next;
            _accountsService = accountsService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Check if user is Firebase-authenticated
                var firebaseUser = context.User;
                if (firebaseUser?.Identity?.IsAuthenticated == true)
                {
                    // Load real account and attach to context
                    var googleAuthUserId = firebaseUser.FindFirst("user_id")?.Value;
                    var account = await _accountsService.GetAccountByGoogleAuthIdAsync(googleAuthUserId);
                    if (account != null)
                    {
                        context.Items["Account"] = account;
                        _logger.LogInformation("Attached authenticated account {AccountId} to request context", account.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AnonymousAuthMiddleware");
            }

            // Always continue to next middleware
            await _next(context);
        }
    }
}
