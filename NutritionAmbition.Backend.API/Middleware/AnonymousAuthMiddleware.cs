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
            // Check if this is an endpoint that should skip authentication
            var path = context.Request.Path.Value?.ToLower();
            if (path != null && path.Contains("firsttimeintroductiontoappprompt"))
            {
                _logger.LogDebug("Skipping account resolution for path: {Path}", path);
                await _next(context);
                return;
            }

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
                else
                {
                    // Enable request body buffering for reading
                    context.Request.EnableBuffering();

                    // Read the body stream
                    string bodyContent;
                    using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
                    {
                        bodyContent = await reader.ReadToEndAsync();
                    }

                    // Reset the stream position for subsequent reads
                    context.Request.Body.Position = 0;

                    // Parse the JSON body
                    using (var jsonDoc = JsonDocument.Parse(bodyContent))
                    {
                        var root = jsonDoc.RootElement;
                        if (root.TryGetProperty("accountId", out var accountIdElement) && 
                            accountIdElement.ValueKind == JsonValueKind.String)
                        {
                            var accountId = accountIdElement.GetString();
                            var account = await _accountsService.GetAccountByIdAsync(accountId);
                            
                            if (account != null)
                            {
                                context.Items["Account"] = account;
                                _logger.LogInformation("Attached existing anonymous account {AccountId} to request context", account.Id);
                            }
                            else
                            {
                                // Create new anonymous account if specified account not found
                                account = await _accountsService.CreateAnonymousAccountAsync();
                                context.Items["Account"] = account;
                                _logger.LogInformation("Created and attached new anonymous account {AccountId} to request context", account.Id);
                            }
                        }
                        else
                        {
                            // Create new anonymous account if no AccountId provided
                            var account = await _accountsService.CreateAnonymousAccountAsync();
                            context.Items["Account"] = account;
                            _logger.LogInformation("Created and attached new anonymous account {AccountId} to request context", account.Id);
                        }
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
