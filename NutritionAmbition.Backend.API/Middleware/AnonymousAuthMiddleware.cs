using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Services;

namespace NutritionAmbition.Backend.API.Middleware
{
    public class AnonymousAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IAccountsService _accountsService;
        private readonly ILogger<AnonymousAuthMiddleware> _logger;

        public AnonymousAuthMiddleware(
            RequestDelegate next,
            IAccountsService accountsService,
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
                    // Enable buffering to allow multiple reads
                    context.Request.EnableBuffering();

                    // If there's no body or it's not JSON, skip parsing and create new account
                    if (context.Request.ContentLength == null || context.Request.ContentLength == 0 ||
                        !context.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var account = await _accountsService.CreateAnonymousAccountAsync();
                        context.Items["Account"] = account;
                        _logger.LogInformation("Created and attached new anonymous account {AccountId} to request context (no body or unsupported content type)", account.Id);
                        await _next(context);
                        return;
                    }

                    // Read and parse the body
                    string bodyContent;
                    using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
                    {
                        bodyContent = await reader.ReadToEndAsync();
                    }

                    context.Request.Body.Position = 0;

                    try
                    {
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
                                    account = await _accountsService.CreateAnonymousAccountAsync();
                                    context.Items["Account"] = account;
                                    _logger.LogInformation("Created and attached new anonymous account {AccountId} to request context", account.Id);
                                }
                            }
                            else
                            {
                                var account = await _accountsService.CreateAnonymousAccountAsync();
                                context.Items["Account"] = account;
                                _logger.LogInformation("Created and attached new anonymous account {AccountId} to request context (no accountId in body)", account.Id);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Invalid JSON in request body. Creating fallback anonymous account.");

                        var account = await _accountsService.CreateAnonymousAccountAsync();
                        context.Items["Account"] = account;
                        _logger.LogInformation("Created and attached new anonymous account {AccountId} to request context (fallback after parse failure)", account.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AnonymousAuthMiddleware");
            }

            await _next(context);
        }
    }
}
