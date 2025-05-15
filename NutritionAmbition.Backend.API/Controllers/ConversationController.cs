using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Attributes;
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
        private readonly AccountsService _accountsService;
        private readonly ILogger<ConversationController> _logger;

        public ConversationController(
            IConversationService conversationService,
            AccountsService accountsService,
            ILogger<ConversationController> logger)
        {
            _conversationService = conversationService;
            _accountsService = accountsService;
            _logger = logger;
        }

        [HttpPost("MergeAnonymousAccount")]
        [FlexibleAuthorize]
        public async Task<ActionResult<MergeAnonymousAccountResponse>> MergeAnonymousAccount([FromBody] MergeAnonymousAccountRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = HttpContext.Items["Account"] as Account;
            if (user == null)
            {
                return Unauthorized();
            }

            var response = await _conversationService.MergeAnonymousAccountAsync(request.AnonymousAccountId, user.Id);
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }
            
            return Ok(response);
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

            var response = await _conversationService.GetInitialMessageAsync(
                account.Id,
                request.LastLoggedDate,
                request.HasLoggedFirstMeal);

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

            var response = await _conversationService.RunAssistantConversationAsync(account.Id, request.Message);

            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
    }
} 