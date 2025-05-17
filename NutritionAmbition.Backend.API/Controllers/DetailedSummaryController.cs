using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Services;
using NutritionAmbition.Backend.API.Extensions;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DetailedSummaryController : ControllerBase
    {
        private readonly IDetailedSummaryService _detailedSummaryService;
        private readonly IAccountsService _accountsService;
        private readonly ILogger<DetailedSummaryController> _logger;

        public DetailedSummaryController(
            IDetailedSummaryService detailedSummaryService,
            IAccountsService accountsService,
            ILogger<DetailedSummaryController> logger)
        {
            _detailedSummaryService = detailedSummaryService;
            _accountsService = accountsService;
            _logger = logger;
        }

        [HttpPost("GetDetailedSummary")]
        public async Task<ActionResult<GetDetailedSummaryResponse>> GetDetailedSummary([FromBody] GetDetailedSummaryRequest request)
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

            _logger.LogInformation("Getting detailed summary for account {AccountId} on date {LoggedDate}", 
                account.Id, request.LoggedDateUtc.ToString("yyyy-MM-dd"));

            var response = await _detailedSummaryService.GetDetailedSummaryAsync(account.Id, request.LoggedDateUtc);
            
            if (!response.IsSuccess)
            {
                _logger.LogWarning("Failed to get detailed summary for account {AccountId}", account.Id);
                return BadRequest(response);
            }

            return Ok(response);
        }
    }
} 