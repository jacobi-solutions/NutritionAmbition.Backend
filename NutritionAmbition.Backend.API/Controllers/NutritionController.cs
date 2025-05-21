using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Services;
using System.Threading.Tasks;
using System.Linq;
using NutritionAmbition.Backend.API.Attributes;
using NutritionAmbition.Backend.API.Extensions;
using System;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [FlexibleAuthorize]
    public class NutritionController : ControllerBase
    {
        private readonly INutritionService _nutritionService;
        private readonly IDailySummaryService _dailySummaryService;
        private readonly IOpenAiService _openAiService;
        private readonly IAccountsService _accountsService;
        private readonly ILogger<NutritionController> _logger;

        public NutritionController(
            INutritionService nutritionService,
            IOpenAiService openAiService,
            IAccountsService accountsService,
            ILogger<NutritionController> logger,
            IDailySummaryService dailySummaryService)
        {
            _nutritionService = nutritionService;
            _openAiService = openAiService;
            _accountsService = accountsService;
            _logger = logger;
            _dailySummaryService = dailySummaryService;
        }

        [HttpPost("GetNutritionDataForFoodItem")]
        public async Task<ActionResult<NutritionApiResponse>> GetNutritionDataForFoodItem([FromBody] FoodItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get account using the extension method which handles both auth types
            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
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

            // Get account using the extension method which handles both auth types
            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
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

        [HttpPost("GetSmartNutritionData")]
        public async Task<ActionResult<NutritionApiResponse>> GetSmartNutritionData([FromBody] ParseFoodTextRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get account using the extension method which handles both auth types
            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
            if (account == null)
            {
                return Unauthorized();
            }

            _logger.LogInformation("Getting smart nutrition data: {FoodDescription}", request.FoodDescription);

            var nutritionData = await _nutritionService.GetSmartNutritionDataAsync(account.Id, request.FoodDescription);
            if (!nutritionData.IsSuccess)
            {
                return BadRequest(new { error = "Failed to get smart nutrition data", details = nutritionData.Errors });
            }

            return Ok(nutritionData);
        }

        

        [HttpPost("GetWeeklySummary")]
        public async Task<ActionResult<NutritionSummaryResponse>> GetWeeklySummary([FromBody] DateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
            if (account == null)
            {
                return Unauthorized();
            }

            var response = await _dailySummaryService.GetWeeklySummaryAsync(account.Id, request.Date);
            return Ok(response);
        }

        [HttpPost("GetMonthlySummary")]
        public async Task<ActionResult<NutritionSummaryResponse>> GetMonthlySummary([FromBody] DateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
            if (account == null)
            {
                return Unauthorized();
            }

            var response = await _dailySummaryService.GetMonthlySummaryAsync(account.Id, request.Date);
            return Ok(response);
        }
    }

    public class FoodItemRequest
    {
        public string FoodDescription { get; set; } = string.Empty;
    }

    public class DateRequest
    {
        public DateTime Date { get; set; }
    }
}
