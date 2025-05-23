using Microsoft.AspNetCore.Mvc;
using NutritionAmbition.Backend.API.Attributes;
using NutritionAmbition.Backend.API.Services;
using NutritionAmbition.Backend.API.DataContracts;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Extensions;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [FlexibleAuthorize]
    public class FoodEntryController : ControllerBase
    {
        private readonly IFoodEntryService _foodEntryService;
        private readonly IOpenAiService _openAiService;
        private readonly IAccountsService _accountsService;
        private readonly ILogger<FoodEntryController> _logger;

        public FoodEntryController(
            IFoodEntryService foodEntryService,
            IOpenAiService openAiService,
            IAccountsService accountsService,
            ILogger<FoodEntryController> logger)
        {
            _foodEntryService = foodEntryService;
            _openAiService = openAiService;
            _accountsService = accountsService;
            _logger = logger;
        }

        [HttpPost("CreateFoodEntry")]
        public async Task<ActionResult<CreateFoodEntryResponse>> CreateFoodEntry([FromBody] CreateFoodEntryRequest request)
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

            var response = await _foodEntryService.AddFoodEntryAsync(account.Id, request);
            return CreatedAtAction(nameof(GetFoodEntries), new { accountId = account.Id }, response);
        }

        [HttpPost("GetFoodEntries")]
        public async Task<ActionResult<GetFoodEntriesResponse>> GetFoodEntries([FromBody] GetFoodEntriesRequest request)
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

            var response = await _foodEntryService.GetFoodEntriesAsync(account.Id, request);
            return Ok(response);
        }

        [HttpPost("UpdateFoodEntry")]
        public async Task<ActionResult<UpdateFoodEntryResponse>> UpdateFoodEntry([FromBody] UpdateFoodEntryRequest request)
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

            var response = await _foodEntryService.UpdateFoodEntryAsync(account.Id, request);
            if (response == null)
            {
                return NotFound();
            }

            return Ok(response);
        }

        [HttpPost("DeleteFoodEntry")]
        public async Task<ActionResult<DeleteFoodEntryResponse>> DeleteFoodEntry([FromBody] DeleteFoodEntryRequest request)
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

            var response = await _foodEntryService.RemoveFoodItemsFromEntryAsync(account.Id, request);
            if (!response.IsSuccess)
            {
                return NotFound();
            }

            return Ok(response);
        }
    }
}