// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using NutritionAmbition.Backend.API;
// using NutritionAmbition.Backend.API.DataContracts;
// using NutritionAmbition.Backend.API.Models;
// using NutritionAmbition.Backend.API.Services;
// using System.Linq;
// using System.Threading.Tasks;

// namespace NutritionAmbition.Backend.API.Controllers
// {
//     [ApiController]
//     [Route("api/accounts")]
//     [Authorize]
//     public class AccountsController : BaseController
//     {
//         public AccountsController(IAccountsService accountsService, ILogger<AccountsController> logger)
//             : base(accountsService, logger)
//         {
//         }

//         [HttpPost("RegisterUser")]
//         public async Task<ActionResult<Response>> RegisterUser([FromBody] AccountRequest request)
//         {
//             _logger.LogInformation("RegisterUser endpoint called.");

//             var googleAuthUserId = User?.Claims.FirstOrDefault(x => x.Type == "user_id")?.Value;
//             if (string.IsNullOrEmpty(googleAuthUserId))
//             {
//                 _logger.LogWarning("Missing Google Auth User ID.");
//                 return BadRequest(new Response { IsSuccess = false, Errors = { new Error { ErrorMessage = "Invalid Google Auth User ID" } } });
//             }

//             var response = await _accountsService.CreateAccountAsync(request, googleAuthUserId);
//             if (!response.IsSuccess)
//             {
//                 _logger.LogError("Failed to register user: {Errors}", string.Join(", ", response.Errors.Select(e => e.ErrorMessage)));
//                 return BadRequest(response);
//             }

//             _logger.LogInformation("User registered successfully.");
//             return Ok(response);
//         }

       
//     }
// } 