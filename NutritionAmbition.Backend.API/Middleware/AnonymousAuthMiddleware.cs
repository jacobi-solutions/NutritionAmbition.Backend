using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Services;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.Middleware
{
    public class AnonymousAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AnonymousAuthMiddleware> _logger;

        public AnonymousAuthMiddleware(RequestDelegate next, ILogger<AnonymousAuthMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, AccountsService accountsService)
        {
            try
            {
                // Skip middleware for non-API requests or if there's no request body
                if (!context.Request.Path.StartsWithSegments("/api") || 
                    context.Request.Method != "POST" || 
                    !context.Request.ContentType.Contains("application/json"))
                {
                    await _next(context);
                    return;
                }

                // Only proceed if we might need to handle anonymous auth
                // Don't interfere with Firebase auth if it's already set up
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    await _next(context);
                    return;
                }

                // We need to read the request body and then reset it for downstream middleware
                context.Request.EnableBuffering();
                
                string requestBody;
                using (var reader = new StreamReader(
                    context.Request.Body,
                    encoding: Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true))
                {
                    requestBody = await reader.ReadToEndAsync();
                    context.Request.Body.Position = 0; // Reset for downstream middleware
                }

                // Check if the request has IsAnonymousUser flag
                if (string.IsNullOrEmpty(requestBody))
                {
                    await _next(context);
                    return;
                }

                var requestData = JsonSerializer.Deserialize<Request>(requestBody, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (requestData?.IsAnonymousUser != true)
                {
                    await _next(context);
                    return;
                }

                _logger.LogInformation("Processing anonymous user request");

                // Handle existing anonymous account
                if (!string.IsNullOrEmpty(requestData.AccountId))
                {
                    var account = await accountsService.GetAccountByIdAsync(requestData.AccountId);
                    if (account != null)
                    {
                        // Attach account to context for controllers to use
                        context.Items["Account"] = account;
                        _logger.LogInformation("Found existing anonymous account: {AccountId}", account.Id);
                        await _next(context);
                        return;
                    }
                    else
                    {
                        _logger.LogWarning("Anonymous account not found: {AccountId}", requestData.AccountId);
                    }
                }

                // Create a new anonymous account
                var newAccount = await accountsService.CreateAnonymousAccountAsync();
                
                _logger.LogInformation("Created new anonymous account: {AccountId}", newAccount.Id);

                // Return account info and short-circuit the pipeline
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                
                var response = new
                {
                    accountId = newAccount.Id,
                    message = "Anonymous account created"
                };
                
                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                
                // Don't call next middleware (short-circuit the pipeline)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in anonymous auth middleware: {ErrorMessage}", ex.Message);
                await _next(context);
            }
        }
    }
} 