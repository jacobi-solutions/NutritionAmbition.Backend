using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Extensions;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Services;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ThreadController : ControllerBase
    {
        private readonly IThreadService _threadService;
        private readonly AccountsService _accountsService;
        private readonly ILogger<ThreadController> _logger;

        public ThreadController(
            IThreadService threadService,
            AccountsService accountsService,
            ILogger<ThreadController> logger)
        {
            _threadService = threadService;
            _accountsService = accountsService;
            _logger = logger;
        }

        [HttpPost("GetTodayThread")]
        public async Task<ActionResult<GetTodayThreadResponse>> GetTodayThread([FromBody] GetTodayThreadRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
            if (account == null)
            {
                return Unauthorized("User account not found");
            }

            _logger.LogInformation("Getting today's thread for account {AccountId}", account.Id);
            var response = await _threadService.GetTodayThreadAsync(account.Id);

            if (!response.IsSuccess)
            {
                _logger.LogWarning("Failed to get today's thread for account {AccountId}", account.Id);
                return BadRequest(response);
            }

            return Ok(response);
        }
    }
} 