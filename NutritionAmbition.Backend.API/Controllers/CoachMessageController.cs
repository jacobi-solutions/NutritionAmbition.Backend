using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Attributes;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Extensions;
using NutritionAmbition.Backend.API.Services;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoachMessageController : ControllerBase
    {
        private readonly ICoachMessageService _coachMessageService;
        private readonly AccountsService _accountsService;
        private readonly ILogger<CoachMessageController> _logger;

        public CoachMessageController(
            ICoachMessageService coachMessageService,
            AccountsService accountsService,
            ILogger<CoachMessageController> logger)
        {
            _coachMessageService = coachMessageService;
            _accountsService = accountsService;
            _logger = logger;
        }

        /// <summary>
        /// Logs a coach message for the user
        /// </summary>
        /// <param name="request">The coach message details</param>
        /// <returns>Response with the logged message</returns>
        [HttpPost("log")]
        [FlexibleAuthorize]
        public async Task<IActionResult> LogCoachMessage([FromBody] LogCoachMessageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var account = await HttpContext.GetAccountFromContextAsync(_accountsService, _logger);
            if (account == null)
            {
                return Unauthorized("User account not found");
            }

            var response = await _coachMessageService.LogMessageAsync(account.Id, request);
            
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        
        /// <summary>
        /// Gets all coach messages for a specific date
        /// </summary>
        /// <param name="request">The request with date information</param>
        /// <returns>Response with the list of coach messages</returns>
        [HttpPost("GetCoachMessages")]
        [FlexibleAuthorize]
        public async Task<IActionResult> GetCoachMessages([FromBody] GetCoachMessagesRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var account = await HttpContext.GetAccountFromContextAsync(_accountsService, _logger);
            if (account == null)
            {
                return Unauthorized("User account not found");
            }

            var response = await _coachMessageService.GetCoachMessagesAsync(account.Id, request);
            
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        
        /// <summary>
        /// Clears coach messages for a specific date or all if date is null
        /// </summary>
        /// <param name="request">The request with optional date information</param>
        /// <returns>Response indicating success and count of messages deleted</returns>
        [HttpPost("ClearCoachMessages")]
        [FlexibleAuthorize]
        public async Task<IActionResult> ClearCoachMessages([FromBody] ClearCoachMessagesRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var account = await HttpContext.GetAccountFromContextAsync(_accountsService, _logger);
            if (account == null)
            {
                return Unauthorized("User account not found");
            }

            var response = await _coachMessageService.ClearCoachMessagesAsync(account.Id, request);
            
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
    }
} 