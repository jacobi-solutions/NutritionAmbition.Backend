using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Services;
using NutritionAmbition.Backend.API.DataContracts;
using System.Threading.Tasks;
using System.Linq;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/dailygoal")]
    [Authorize]
    public class DailyGoalController : ControllerBase
    {
        private readonly IDailyGoalService _dailyGoalService;
        private readonly AccountsService _accountsService;
        private readonly ILogger<DailyGoalController> _logger;

        public DailyGoalController(
            IDailyGoalService dailyGoalService, 
            AccountsService accountsService,
            ILogger<DailyGoalController> logger)
        {
            _dailyGoalService = dailyGoalService;
            _accountsService = accountsService;
            _logger = logger;
        }

        [HttpPost("GetDailyGoal")]
        public async Task<ActionResult<GetDailyGoalResponse>> GetDailyGoal([FromBody] GetDailyGoalRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var googleAuthUserId = User?.Claims.FirstOrDefault(x => x.Type == "user_id")?.Value;
            var account = await _accountsService.GetAccountByGoogleAuthIdAsync(googleAuthUserId);
            if (account == null)
            {
                _logger.LogWarning("Unauthorized access attempt for getting daily goal. User not found.");
                return Unauthorized();
            }

            var response = await _dailyGoalService.GetForDateAsync(account.Id, request);
            return Ok(response);
        }

        [HttpPost("SetDailyGoal")]
        public async Task<ActionResult<SetDailyGoalResponse>> SetDailyGoal([FromBody] SetDailyGoalRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.DailyGoal == null)
            {
                _logger.LogWarning("Bad request: DailyGoal data is missing");
                return BadRequest(new { error = "DailyGoal data is required" });
            }

            var googleAuthUserId = User?.Claims.FirstOrDefault(x => x.Type == "user_id")?.Value;
            var account = await _accountsService.GetAccountByGoogleAuthIdAsync(googleAuthUserId);
            if (account == null)
            {
                _logger.LogWarning("Unauthorized access attempt for setting daily goal. User not found.");
                return Unauthorized();
            }

            var response = await _dailyGoalService.SetGoalsAsync(account.Id, request);
            
            if (response.IsSuccess)
            {
                return Ok(response);
            }
            else
            {
                return BadRequest(response);
            }
        }
    }
} 