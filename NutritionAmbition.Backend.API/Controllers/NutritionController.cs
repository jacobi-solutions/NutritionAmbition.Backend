using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Services;
using System.Threading.Tasks;
using System.Linq;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NutritionController : ControllerBase
    {
        private readonly INutritionService _nutritionService;
        private readonly IOpenAiService _openAiService;
        private readonly AccountsService _accountsService;
        private readonly ILogger<NutritionController> _logger;

        public NutritionController(
            INutritionService nutritionService,
            IOpenAiService openAiService,
            AccountsService accountsService,
            ILogger<NutritionController> logger)
        {
            _nutritionService = nutritionService;
            _openAiService = openAiService;
            _accountsService = accountsService;
            _logger = logger;
        }

        [HttpPost("GetNutritionDataForFoodItem")]
        public async Task<ActionResult<NutritionApiResponse>> GetNutritionDataForFoodItem([FromBody] FoodItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var googleAuthUserId = User?.Claims.FirstOrDefault(x => x.Type == "user_id")?.Value;
            var account = await _accountsService.GetAccountByGoogleAuthIdAsync(googleAuthUserId);
            if (account == null)
            {
                return Unauthorized();
            }

            _logger.LogInformation("Getting nutrition data for food item: {FoodDescription}", request.FoodDescription);
            
            // Use the updated method that directly queries Nutritionix
            var response = await _nutritionService.GetNutritionDataForFoodItemAsync(account.Id, request.FoodDescription);
                
            return Ok(response);
        }

        [HttpPost("ProcessFoodTextAndGetNutrition")]
        public async Task<ActionResult<NutritionApiResponse>> ProcessFoodTextAndGetNutrition([FromBody] ParseFoodTextRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var googleAuthUserId = User?.Claims.FirstOrDefault(x => x.Type == "user_id")?.Value;
            var account = await _accountsService.GetAccountByGoogleAuthIdAsync(googleAuthUserId);
            if (account == null)
            {
                return Unauthorized();
            }

            _logger.LogInformation("Processing food text and getting nutrition data: {FoodDescription}", request.FoodDescription);

            // Use the streamlined method that handles everything automatically
            var nutritionData = await _nutritionService.ProcessFoodTextAndGetNutritionAsync(account.Id, request.FoodDescription);
            if (!nutritionData.IsSuccess)
            {
                return BadRequest(new { error = "Failed to process food text and get nutrition data", details = nutritionData.Errors });
            }

            return Ok(nutritionData);
        }
    }

    public class FoodItemRequest
    {
        public string FoodDescription { get; set; } = string.Empty;
    }
}
