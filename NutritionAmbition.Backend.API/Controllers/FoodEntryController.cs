using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NutritionAmbition.Backend.API.Services;
using NutritionAmbition.Backend.API.DataContracts;
using System.Threading.Tasks;
using System.Linq;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FoodEntryController : ControllerBase
    {
        private readonly IFoodEntryService _foodEntryService;
        private readonly AccountsService _accountsService;

        public FoodEntryController(IFoodEntryService foodEntryService, AccountsService accountsService)
        {
            _foodEntryService = foodEntryService;
            _accountsService = accountsService;
        }

        [HttpPost("CreateFoodEntry")]
        public async Task<ActionResult<CreateFoodEntryResponse>> CreateFoodEntry([FromBody] CreateFoodEntryRequest request)
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

            var googleAuthUserId = User?.Claims.FirstOrDefault(x => x.Type == "user_id")?.Value;
            var account = await _accountsService.GetAccountByGoogleAuthIdAsync(googleAuthUserId);
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

            var googleAuthUserId = User?.Claims.FirstOrDefault(x => x.Type == "user_id")?.Value;
            var account = await _accountsService.GetAccountByGoogleAuthIdAsync(googleAuthUserId);
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

            var googleAuthUserId = User?.Claims.FirstOrDefault(x => x.Type == "user_id")?.Value;
            var account = await _accountsService.GetAccountByGoogleAuthIdAsync(googleAuthUserId);
            if (account == null)
            {
                return Unauthorized();
            }

            var response = await _foodEntryService.DeleteFoodEntryAsync(account.Id, request);
            if (!response.IsSuccess)
            {
                return NotFound();
            }

            return Ok(response);
        }
    }

}