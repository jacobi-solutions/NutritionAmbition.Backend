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
    public class FoodParsingController : ControllerBase
    {
        private readonly IFoodParsingService _foodParsingService;
        private readonly AccountsService _accountsService;

        public FoodParsingController(
            IFoodParsingService foodParsingService, 
            AccountsService accountsService)
        {
            _foodParsingService = foodParsingService;
            _accountsService = accountsService;
        }

        [HttpPost("ParseFoodText")]
        public async Task<ActionResult<ParseFoodTextResponse>> ParseFoodText([FromBody] ParseFoodTextRequest request)
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

            var response = await _foodParsingService.ParseFoodTextAsync(request.FoodDescription);
            return Ok(response);
        }
    }
}
