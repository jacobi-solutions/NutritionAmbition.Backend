using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.Attributes
{
    /// <summary>
    /// Authorization attribute that allows access to both Firebase-authenticated users
    /// and anonymous users who have been processed by AnonymousAuthMiddleware.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class FlexibleAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly ILogger<FlexibleAuthorizeAttribute>? _logger;

        /// <summary>
        /// Initializes a new instance of FlexibleAuthorizeAttribute.
        /// </summary>
        public FlexibleAuthorizeAttribute()
        {
            // Default constructor with no logger
        }

        /// <summary>
        /// Initializes a new instance of FlexibleAuthorizeAttribute with logging.
        /// </summary>
        /// <param name="logger">Logger for diagnostic information</param>
        public FlexibleAuthorizeAttribute(ILogger<FlexibleAuthorizeAttribute> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Called early in the filter pipeline to confirm request authorization.
        /// </summary>
        /// <param name="context">The authorization filter context.</param>
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Ensure we have a valid context
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // Get logger if not provided in constructor
            var logger = _logger ?? context.HttpContext.RequestServices.GetService<ILogger<FlexibleAuthorizeAttribute>>();
            
            // Check Firebase authentication first
            if (context.HttpContext.User?.Identity?.IsAuthenticated == true)
            {
                logger?.LogDebug("User authenticated via Firebase");
                return; // Allow the request to proceed
            }

            // If not authenticated via Firebase, check for anonymous user
            if (context.HttpContext.Items.TryGetValue("Account", out var accountObj) && 
                accountObj is Account account)
            {
                logger?.LogDebug("User authenticated via anonymous account: {AccountId}", account.Id);
                return; // Allow the request to proceed
            }

            // If we get here, the user is neither Firebase-authenticated nor anonymous
            logger?.LogWarning("Unauthorized access attempt: Neither Firebase auth nor anonymous account found");
            context.Result = new UnauthorizedResult();
        }
    }
} 