using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Services;
using NutritionAmbition.Backend.API.DataContracts;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/dailygoal")]
    [Authorize]
    public class DailyGoalController : BaseController
    {
        private readonly IDailyGoalService _dailyGoalService;

        public DailyGoalController(IDailyGoalService dailyGoalService, IAccountsService accountsService, ILogger<DailyGoalController> logger)
            : base(accountsService, logger)
        {
            _dailyGoalService = dailyGoalService;
        }

        [HttpPost("GetDailyGoal")]
        public async Task<ActionResult<GetDailyGoalResponse>> GetDailyGoal([FromBody] GetDailyGoalRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Account initialization is now handled automatically by the BaseController

            // Create response object
            var response = new GetDailyGoalResponse();
            
            try
            {
                // Use date from request or default to today
                DateTime requestDate = request.Date ?? DateTime.UtcNow.Date;
                
                // Use the new read-only method that doesn't create a goal if one doesn't exist
                var dailyGoal = await _dailyGoalService.GetGoalByDateAsync(_account.Id, requestDate);
                
                // Set the response - goal can be null if not found
                response.DailyGoal = dailyGoal;
                response.IsSuccess = true;
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily goal for account {AccountId}", _account.Id);
                response.AddError($"Failed to retrieve daily goal: {ex.Message}");
                return BadRequest(response);
            }
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

            // Account initialization is now handled automatically by the BaseController

            var response = await _dailyGoalService.SetGoalsAsync(_account.Id, request);
            
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