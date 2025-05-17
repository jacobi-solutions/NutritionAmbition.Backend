using Microsoft.AspNetCore.Mvc;
using NutritionAmbition.Backend.API;
using NutritionAmbition.Backend.API.Attributes;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Services;
using System.Linq;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/accounts")]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountsService _accountsService;
        private readonly ILogger<AccountsController> _logger;

        public AccountsController(IAccountsService accountsService, ILogger<AccountsController> logger)
        {
            _accountsService = accountsService;
            _logger = logger;
        }

        [HttpPost("RegisterUser")]
        public async Task<ActionResult<Response>> RegisterUser([FromBody] AccountRequest request)
        {
            _logger.LogInformation("RegisterUser endpoint called.");

            var googleAuthUserId = User?.Claims.FirstOrDefault(x => x.Type == "user_id")?.Value;
            if (string.IsNullOrEmpty(googleAuthUserId))
            {
                _logger.LogWarning("Missing Google Auth User ID.");
                return BadRequest(new Response { IsSuccess = false, Errors = { new Error { ErrorMessage = "Invalid Google Auth User ID" } } });
            }

            var response = await _accountsService.CreateAccountAsync(request, googleAuthUserId);
            if (!response.IsSuccess)
            {
                _logger.LogError("Failed to register user: {Errors}", string.Join(", ", response.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(response);
            }

            _logger.LogInformation("User registered successfully.");
            return Ok(response);
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

            var response = await _accountsService.MergeAnonymousAccountAsync(request.AnonymousAccountId, user.Id);
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }
            
            return Ok(response);
        }
    }
} 