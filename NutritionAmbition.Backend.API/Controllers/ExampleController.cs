using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Attributes;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Extensions;
using NutritionAmbition.Backend.API.Services;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/example")]
    public class ExampleController : ControllerBase
    {
        private readonly AccountsService _accountsService;
        private readonly ILogger<ExampleController> _logger;

        public ExampleController(
            AccountsService accountsService,
            ILogger<ExampleController> logger)
        {
            _accountsService = accountsService;
            _logger = logger;
        }

        /// <summary>
        /// Example endpoint that works with both anonymous and authenticated users
        /// </summary>
        [HttpPost("info")]
        public async Task<IActionResult> GetUserInfo([FromBody] ExampleRequest request)
        {
            // Get account using our extension method
            var account = await HttpContext.GetAccountFromContextAsync(_accountsService, _logger);

            if (account == null)
            {
                // Check if this is an anonymous request
                if (request?.IsAnonymousUser == true)
                {
                    _logger.LogWarning("Anonymous request received but no account found with ID: {AccountId}", 
                        request.AccountId ?? "(not provided)");
                    
                    return BadRequest(new { 
                        message = "Anonymous user account not found. Please create a new anonymous account first." 
                    });
                }
                
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Determine if this is an anonymous user by checking if GoogleAuthUserId is null
            bool isAnonymous = string.IsNullOrEmpty(account.GoogleAuthUserId);

            return Ok(new
            {
                accountId = account.Id,
                username = account.Name,
                email = account.Email,
                isAnonymous = isAnonymous,
                message = isAnonymous 
                    ? "You are using an anonymous account. Your data will be associated with this account ID."
                    : "You are using an authenticated account."
            });
        }
        
        /// <summary>
        /// Example of a protected endpoint using FlexibleAuthorize, which allows
        /// both Firebase-authenticated users and anonymous users.
        /// </summary>
        [HttpPost("protected")]
        [FlexibleAuthorize]
        public async Task<IActionResult> ProtectedEndpoint([FromBody] ExampleRequest request)
        {
            // Get account using our extension method
            var account = await HttpContext.GetAccountFromContextAsync(_accountsService, _logger);
            
            // We should always have an account here because of FlexibleAuthorize
            // But just to be safe, check anyway
            if (account == null)
            {
                // This should not happen due to FlexibleAuthorize, but just in case
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Determine if this is an anonymous user
            bool isAnonymous = string.IsNullOrEmpty(account.GoogleAuthUserId);

            return Ok(new
            {
                accountId = account.Id,
                username = account.Name,
                accessType = isAnonymous ? "anonymous" : "authenticated",
                message = "You have accessed a protected endpoint."
            });
        }
        
        /// <summary>
        /// Custom request model for the example endpoint
        /// </summary>
        public class ExampleRequest : Request
        {
            public string? AdditionalData { get; set; }
        }
    }
} 