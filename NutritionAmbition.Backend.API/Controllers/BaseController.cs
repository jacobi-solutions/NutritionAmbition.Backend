using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Controllers
{
    public abstract class BaseController : ControllerBase, IAsyncActionFilter
    {
        protected readonly IAccountsService _accountsService;
        protected readonly ILogger _logger;
        protected Account _account;

        protected BaseController(IAccountsService accountsService, ILogger logger)
        {
            _accountsService = accountsService;
            _logger = logger;
        }

        /// <summary>
        /// Automatically initializes the account before the action is executed.
        /// If the account can't be initialized, returns a 401 Unauthorized response.
        /// </summary>
        [NonAction]
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Skip initialization for endpoints that don't require authentication
            var endpoint = context.HttpContext.GetEndpoint();
            var allowAnonymous = endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>();
            
            if (allowAnonymous == null)
            {
                // Extract user_id from claims
                var googleAuthUserId = User?.Claims.FirstOrDefault(x => x.Type == "user_id")?.Value;
                if (string.IsNullOrEmpty(googleAuthUserId))
                {
                    _logger.LogWarning("Missing Google Auth User ID in token");
                    context.Result = Unauthorized();
                    return;
                }

                var email = User?.Claims.FirstOrDefault(x => x.Type == "email")?.Value;

                _account = await _accountsService.GetOrCreateByGoogleAuthIdAsync(googleAuthUserId, email);
                if (_account == null)
                {
                    _logger.LogWarning("Failed to get or create account for Google Auth User ID: {GoogleAuthUserId}", googleAuthUserId);
                    context.Result = Unauthorized();
                    return;
                }
            }

            // Account initialized successfully or skipped, proceed with the action
            await next();
        }

        
    }
} 