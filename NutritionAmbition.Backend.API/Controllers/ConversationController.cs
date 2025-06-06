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
using System.Diagnostics; // add this at the top

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
            

            var stopwatch = Stopwatch.StartNew();

            // code you want to time
            var response = await _conversationService.RunResponsesConversationAsync(account.Id, request.Message);

            stopwatch.Stop();
            _logger.LogWarning("RunResponsesConversationAsync took {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

            response.LoggedMeal = response.ToolCalls.Any(t =>
                string.Equals(t.Function?.Name, AssistantToolTypes.LogMealTool, StringComparison.OrdinalIgnoreCase));

            return Ok(response);
        }

        [HttpPost("focus-in-chat")]
        [FlexibleAuthorize]
        public async Task<ActionResult<BotMessageResponse>> FocusInChat([FromBody] FocusInChatRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request == null || string.IsNullOrWhiteSpace(request.FocusText))
            {
                var errorResponse = new BotMessageResponse();
                errorResponse.AddError("Focus text is required.");
                return BadRequest(errorResponse);
            }

            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
            if (account == null)
            {
                return Unauthorized();
            }

            var stopwatch = Stopwatch.StartNew();
            
            var response = await _conversationService.RunFocusInChatAsync(account.Id, request);
            
            stopwatch.Stop();
            _logger.LogWarning("RunFocusInChatAsync took {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("learn-more-about")]
        [FlexibleAuthorize]
        public async Task<ActionResult<BotMessageResponse>> LearnMoreAbout([FromBody] LearnMoreAboutRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Topic))
            {
                var errorResponse = new BotMessageResponse();
                errorResponse.AddError("Topic is required.");
                return BadRequest(errorResponse);
            }

            var account = await HttpContext.GetAccountFromContextAsync(_accountsService);
            if (account == null)
            {
                return Unauthorized();
            }

            var stopwatch = Stopwatch.StartNew();
            
            var response = await _conversationService.RunLearnMoreAboutAsync(account.Id, request);
            
            stopwatch.Stop();
            _logger.LogWarning("RunLearnMoreAboutAsync took {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
    }
} 