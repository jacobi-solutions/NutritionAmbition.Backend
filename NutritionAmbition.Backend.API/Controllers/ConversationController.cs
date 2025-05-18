using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Attributes;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Extensions;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Services;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConversationController : ControllerBase
    {
        private readonly IConversationService _conversationService;
        private readonly IAccountsService _accountsService;
        private readonly ILogger<ConversationController> _logger;

        public ConversationController(
            IConversationService conversationService,
            IAccountsService accountsService,
            ILogger<ConversationController> logger)
        {
            _conversationService = conversationService;
            _accountsService = accountsService;
            _logger = logger;
        }

        [HttpPost("GetInitialMessage")]
        public async Task<ActionResult<BotMessageResponse>> GetInitialMessage([FromBody] GetInitialMessageRequest request)
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

            // Check if timezone offset is provided
            if (request.TimezoneOffsetMinutes.HasValue)
            {
                _logger.LogInformation("Using client-provided timezone offset: {TimezoneOffsetMinutes} minutes", request.TimezoneOffsetMinutes.Value);
            }

            // Call the RunAssistantConversationAsync with daily check-in trigger
            var assistantResponse = await _conversationService.RunAssistantConversationAsync(
                account.Id, 
                ConversationConstants.DAILY_CHECKIN, 
                request.TimezoneOffsetMinutes);

            if (!assistantResponse.IsSuccess)
            {
                return BadRequest(assistantResponse);
            }

            // Map the AssistantRunMessageResponse to BotMessageResponse to maintain API compatibility
            var response = new BotMessageResponse
            {
                Message = assistantResponse.AssistantMessage,
                IsSuccess = assistantResponse.IsSuccess
            };

            if (assistantResponse.Errors != null)
            {
                foreach (var error in assistantResponse.Errors)
                {
                    response.AddError(error.ErrorMessage, error.ErrorCode);
                }
            }

            return Ok(response);
        }

        [HttpPost("GetPostLogHint")]
        public async Task<ActionResult<BotMessageResponse>> GetPostLogHint([FromBody] PostLogHintRequest request)
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

            var response = await _conversationService.GetPostLogHintAsync(
                account.Id,
                request.LastLoggedDate,
                request.HasLoggedFirstMeal);

            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("GetAnonymousWarning")]
        public async Task<ActionResult<BotMessageResponse>> GetAnonymousWarning([FromBody] AnonymousWarningRequest request)
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

            var response = await _conversationService.GetAnonymousWarningAsync(
                account.Id,
                request.LastLoggedDate,
                request.HasLoggedFirstMeal);

            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("LogChatMessage")]
        public async Task<ActionResult<LogChatMessageResponse>> LogChatMessage([FromBody] LogChatMessageRequest request)
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


            var response = await _conversationService.LogMessageAsync(account.Id, request);
            
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            response.AnonymousAccountId = account.Id;
            return Ok(response);
        }

        [HttpPost("GetChatMessages")]
        public async Task<ActionResult<GetChatMessagesResponse>> GetChatMessages([FromBody] GetChatMessagesRequest request)
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

            var response = await _conversationService.GetChatMessagesAsync(account.Id, request);
            
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("GetChatMessagesByDate")]
        public async Task<ActionResult<GetChatMessagesResponse>> GetChatMessagesByDate([FromBody] GetChatMessagesRequest request)
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

            var response = await _conversationService.GetChatMessagesAsync(account.Id, request);
            
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("ClearChatMessages")]
        public async Task<ActionResult<ClearChatMessagesResponse>> ClearChatMessages([FromBody] ClearChatMessagesRequest request)
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

            var response = await _conversationService.ClearChatMessagesAsync(account.Id, request);
            
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("AssistantRunMessage")]
        [FlexibleAuthorize]
        public async Task<ActionResult<AssistantRunMessageResponse>> AssistantRunMessage([FromBody] AssistantRunMessageRequest request)
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

            // Check if timezone offset is provided
            if (request.TimezoneOffsetMinutes.HasValue)
            {
                _logger.LogInformation("Using client-provided timezone offset: {TimezoneOffsetMinutes} minutes", request.TimezoneOffsetMinutes.Value);
            }

            var response = await _conversationService.RunAssistantConversationAsync(account.Id, request.Message, request.TimezoneOffsetMinutes);

            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            // If the run is still in progress, return a 202 Accepted with the friendly message
            if (response.RunStatus == "in_progress")
            {
                _logger.LogWarning("Assistant run for account {AccountId} is still in progress. Returning friendly waiting message.", account.Id);
                return StatusCode(202, response); // 202 Accepted
            }

            return Ok(response);
        }
    }
} 