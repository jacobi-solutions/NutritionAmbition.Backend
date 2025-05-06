using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Services;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DailySummaryController : ControllerBase
    {
        private readonly IDailySummaryService _dailySummaryService;

        public DailySummaryController(IDailySummaryService dailySummaryService)
        {
            _dailySummaryService = dailySummaryService;
        }

        [HttpGet("GetTotals")]
        public async Task<ActionResult<DailySummaryResponse>> GetTotals(string accountId)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return BadRequest("Account ID is required");
            }
            
            var response = await _dailySummaryService.GetDailySummaryAsync(accountId);
            
            if (!response.IsSuccess)
            {
                return BadRequest(response.Errors);
            }
            
            return Ok(response);
        }
    }
} 