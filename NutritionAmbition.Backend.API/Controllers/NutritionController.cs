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
        private readonly IFoodParsingService _foodParsingService;
        private readonly AccountsService _accountsService;

        public NutritionController(
            INutritionService nutritionService,
            IFoodParsingService foodParsingService,
            AccountsService accountsService)
        {
            _nutritionService = nutritionService;
            _foodParsingService = foodParsingService;
            _accountsService = accountsService;
        }

        [HttpPost("GetNutritionData")]
        public async Task<ActionResult<NutritionApiResponse>> GetNutritionData([FromBody] ParseFoodTextResponse parsedFood)
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

            var response = await _nutritionService.GetNutritionDataForParsedFoodAsync(parsedFood);
            return Ok(response);
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

            var response = await _nutritionService.GetNutritionDataForFoodItemAsync(
                request.FoodDescription, 
                request.Quantity, 
                request.Unit);
                
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

            // Step 1: Parse the food text
            var parsedFood = await _foodParsingService.ParseFoodTextAsync(request.FoodDescription);
            if (!parsedFood.Success)
            {
                return BadRequest(new { error = "Failed to parse food text", details = parsedFood.ErrorMessage });
            }

            // Step 2: Get nutrition data
            var nutritionData = await _nutritionService.GetNutritionDataForParsedFoodAsync(parsedFood);
            if (!nutritionData.IsSuccess)
            {
                return BadRequest(new { error = "Failed to get nutrition data", details = nutritionData.Errors });
            }

            return Ok(new
            {
                ParsedFood = parsedFood,
                NutritionData = nutritionData
            });
        }
    }

    public class FoodItemRequest
    {
        public string FoodDescription { get; set; } = string.Empty;
        public string Quantity { get; set; } = "1";
        public string Unit { get; set; } = string.Empty;
    }
}
