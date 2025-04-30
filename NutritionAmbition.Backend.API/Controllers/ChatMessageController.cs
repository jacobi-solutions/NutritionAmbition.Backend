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
    public class ChatMessageController : ControllerBase
    {
        private readonly IChatMessageService _chatMessageService;
        private readonly AccountsService _accountsService;
        private readonly ILogger<ChatMessageController> _logger;

        public ChatMessageController(
            IChatMessageService chatMessageService,
            AccountsService accountsService,
            ILogger<ChatMessageController> logger)
        {
            _chatMessageService = chatMessageService;
            _accountsService = accountsService;
            _logger = logger;
        }

        /// <summary>
        /// Logs a chat message for the user
        /// </summary>
        /// <param name="request">The chat message details</param>
        /// <returns>Response with the logged message</returns>
        [HttpPost("log")]
        [FlexibleAuthorize]
        public async Task<IActionResult> LogChatMessage([FromBody] LogChatMessageRequest request)
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

            var response = await _chatMessageService.LogMessageAsync(account.Id, request);
            
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        
        /// <summary>
        /// Gets all chat messages for a specific date
        /// </summary>
        /// <param name="request">The request with date information</param>
        /// <returns>Response with the list of chat messages</returns>
        [HttpPost("GetChatMessages")]
        [FlexibleAuthorize]
        public async Task<IActionResult> GetChatMessages([FromBody] GetChatMessagesRequest request)
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

            var response = await _chatMessageService.GetChatMessagesAsync(account.Id, request);
            
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        
        /// <summary>
        /// Clears chat messages for a specific date or all if date is null
        /// </summary>
        /// <param name="request">The request with optional date information</param>
        /// <returns>Response indicating success and count of messages deleted</returns>
        [HttpPost("ClearChatMessages")]
        [FlexibleAuthorize]
        public async Task<IActionResult> ClearChatMessages([FromBody] ClearChatMessagesRequest request)
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

            var response = await _chatMessageService.ClearChatMessagesAsync(account.Id, request);
            
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
    }
} 