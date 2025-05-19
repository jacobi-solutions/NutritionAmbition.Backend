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
using System.Collections.Generic;

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

            // Use the Responses API with daily check-in trigger
            var response = await _conversationService.RunResponsesConversationAsync(
                account.Id, 
                ConversationConstants.DAILY_CHECKIN);

            if (!response.IsSuccess)
            {
                return BadRequest(response);
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

        [HttpPost("RunResponsesConversation")]
        [FlexibleAuthorize]
        public async Task<ActionResult<BotMessageResponse>> RunResponsesConversation([FromBody] RunChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                var errorResponse = new BotMessageResponse();
                errorResponse.AddError("Message is required.");
                return BadRequest(errorResponse);
            }

            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
            if (account == null)
            {
                return Unauthorized();
            }

            var result = await _conversationService.RunResponsesConversationAsync(account.Id, request.Message);
            return Ok(result);
        }
    }
} 