using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Attributes;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.DataContracts.Profile;
using NutritionAmbition.Backend.API.Extensions;
using NutritionAmbition.Backend.API.Services;
using System.Linq;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [FlexibleAuthorize]
    public class AssistantToolController : ControllerBase
    {
        private readonly IAssistantToolService _assistantToolService;
        private readonly IAccountsService _accountsService;
        private readonly ILogger<AssistantToolController> _logger;

        public AssistantToolController(
            IAssistantToolService assistantToolService,
            IAccountsService accountsService,
            ILogger<AssistantToolController> logger)
        {
            _assistantToolService = assistantToolService;
            _accountsService = accountsService;
            _logger = logger;
        }

        [HttpPost(nameof(AssistantToolTypes.LogMealTool))]
        public async Task<ActionResult<LogMealToolResponse>> LogMealTool([FromBody] LogMealToolRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get account from context (supports both Firebase and anonymous auth)
            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
            if (account == null)
            {
                _logger.LogWarning("Unauthorized access attempt to LogMealTool endpoint");
                return Unauthorized("User account not found");
            }

            _logger.LogInformation("Assistant requested meal logging for account {AccountId}: {Meal}", 
                account.Id, request.Meal);

            var response = await _assistantToolService.LogMealToolAsync(account.Id, request.Meal);

            if (!response.IsSuccess)
            {
                _logger.LogWarning("Failed to log meal for account {AccountId}: {Errors}", 
                    account.Id, string.Join(", ", response.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost(nameof(AssistantToolTypes.SaveUserProfileTool))]
        public async Task<ActionResult<SaveUserProfileResponse>> SaveUserProfileTool([FromBody] SaveUserProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get account from context (supports both Firebase and anonymous auth)
            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
            if (account == null)
            {
                _logger.LogWarning("Unauthorized access attempt to SaveUserProfileTool endpoint");
                return Unauthorized("User account not found");
            }

            // Override accountId in the request with the authenticated user's account ID
            request.AccountId = account.Id;

            _logger.LogInformation("Assistant requested profile creation for account {AccountId}", account.Id);

            var response = await _assistantToolService.SaveUserProfileToolAsync(request);

            if (!response.IsSuccess)
            {
                _logger.LogWarning("Failed to create profile for account {AccountId}: {Errors}", 
                    account.Id, string.Join(", ", response.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost(nameof(AssistantToolTypes.GetProfileAndGoalsTool))]
        public async Task<ActionResult<GetProfileAndGoalsResponse>> GetProfileAndGoalsTool([FromBody] GetProfileAndGoalsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get account from context (supports both Firebase and anonymous auth)
            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
            if (account == null)
            {
                _logger.LogWarning("Unauthorized access attempt to GetProfileAndGoalsTool endpoint");
                return Unauthorized("User account not found");
            }

            _logger.LogInformation("Assistant requested profile and goals retrieval for account {AccountId}", account.Id);

            // Use the authenticated account ID
            request.AccountId = account.Id;
            
            var response = await _assistantToolService.GetProfileAndGoalsToolAsync(request.AccountId);
            
            if (!response.IsSuccess)
            {
                _logger.LogWarning("Failed to retrieve profile and goals for account {AccountId}: {Errors}", 
                    account.Id, string.Join(", ", response.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(response);
            }
            
            return Ok(response);
        }

        [HttpPost(nameof(AssistantToolTypes.SetDefaultGoalProfileTool))]
        public async Task<ActionResult<SetDefaultGoalProfileResponse>> SetDefaultGoalProfileTool([FromBody] SetDefaultGoalProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get account from context (supports both Firebase and anonymous auth)
            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
            if (account == null)
            {
                _logger.LogWarning("Unauthorized access attempt to SetDefaultGoalProfileTool endpoint");
                return Unauthorized("User account not found");
            }

            // Override accountId in the request with the authenticated user's account ID
            request.AccountId = account.Id;

            _logger.LogInformation("Assistant requested default goal profile update for account {AccountId}", account.Id);

            var response = await _assistantToolService.SetDefaultGoalProfileToolAsync(request);

            if (!response.IsSuccess)
            {
                _logger.LogWarning("Failed to set default goal profile for account {AccountId}: {Errors}", 
                    account.Id, string.Join(", ", response.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost(nameof(AssistantToolTypes.OverrideDailyGoalsTool))]
        public async Task<ActionResult<OverrideDailyGoalsResponse>> OverrideDailyGoalsTool([FromBody] OverrideDailyGoalsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get account from context (supports both Firebase and anonymous auth)
            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
            if (account == null)
            {
                _logger.LogWarning("Unauthorized access attempt to OverrideDailyGoalsTool endpoint");
                return Unauthorized("User account not found");
            }

            // Override accountId in the request with the authenticated user's account ID
            request.AccountId = account.Id;

            _logger.LogInformation("Assistant requested daily goals override for account {AccountId} to {Calories} calories", 
                account.Id, request.NewBaseCalories);

            var response = await _assistantToolService.OverrideDailyGoalsToolAsync(request);

            if (!response.IsSuccess)
            {
                _logger.LogWarning("Failed to override daily goals for account {AccountId}: {Errors}", 
                    account.Id, string.Join(", ", response.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(response);
            }

            return Ok(response);
        }
    }
} 