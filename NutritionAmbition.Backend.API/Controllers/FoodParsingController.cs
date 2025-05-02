using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Attributes;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Extensions;
using NutritionAmbition.Backend.API.Services;
using System.Threading.Tasks;
using System.Linq;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [FlexibleAuthorize]
    public class FoodParsingController : ControllerBase
    {
        private readonly IFoodParsingService _foodParsingService;
        private readonly AccountsService _accountsService;
        private readonly ILogger<FoodParsingController> _logger;

        public FoodParsingController(
            IFoodParsingService foodParsingService, 
            AccountsService accountsService,
            ILogger<FoodParsingController> logger)
        {
            _foodParsingService = foodParsingService;
            _accountsService = accountsService;
            _logger = logger;
        }

        [HttpPost("ParseFoodText")]
        public async Task<ActionResult<ParseFoodTextResponse>> ParseFoodText([FromBody] ParseFoodTextRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get account using the extension method which handles both auth types
            var account = await HttpContext.GetAccountFromContextAsync(_accountsService, _logger);
            if (account == null)
            {
                return Unauthorized();
            }

            // Optionally log whether this is an anonymous or authenticated user
            bool isAnonymous = string.IsNullOrEmpty(account.GoogleAuthUserId);
            _logger.LogInformation(
                "Processing food text parsing request for {User} (AccountId: {AccountId})",
                isAnonymous ? "anonymous user" : "authenticated user",
                account.Id
            );

            var response = await _foodParsingService.ParseFoodTextAsync(request.FoodDescription);
            
            if (!response.IsSuccess)
            {
                return BadRequest(new 
                { 
                    error = "Failed to parse food text",
                    details = response.Errors.Select(e => e.ErrorMessage)
                });
            }
            
            return Ok(response);
        }
    }
}
