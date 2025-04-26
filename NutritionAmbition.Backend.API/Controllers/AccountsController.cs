// using Microsoft.AspNetCore.Mvc;
// using System.Threading.Tasks;
// using NutritionAmbition.Backend.API.Models;
// using NutritionAmbition.Backend.API.Services;
// using NutritionAmbition.Backend.API.Auth;
// using System.Collections.Generic;

// namespace NutritionAmbition.Backend.API.Controllers
// {
//     [ApiController]
//     [Route("api/accounts")]
//     public class AccountsController : ControllerBase
//     {
//         private readonly AccountsService _accountsService;

//         public AccountsController(AccountsService accountsService)
//         {
//             _accountsService = accountsService;
//         }

//         [HttpGet]
//         [Auth] // Requires authentication
//         public async Task<ActionResult<List<Account>>> GetAccounts()
//         {
//             var accounts = await _accountsService.GetAllAccountsAsync();
//             return Ok(accounts);
//         }

//         [HttpGet("{id}")]
//         [Auth] // Requires authentication
//         public async Task<ActionResult<Account>> GetAccount(string id)
//         {
//             var account = await _accountsService.GetAccountByIdAsync(id);
//             if (account == null)
//             {
//                 return NotFound();
//             }
//             return Ok(account);
//         }

//         [HttpPost]
//         public async Task<ActionResult<Account>> CreateAccount([FromBody] Account newAccount)
//         {
//             if (newAccount == null)
//             {
//                 return BadRequest("Invalid account data.");
//             }

//             var createdAccount = await _accountsService.CreateAccountAsync(newAccount);
//             return CreatedAtAction(nameof(GetAccount), new { id = createdAccount.Id }, createdAccount);
//         }

//         [HttpPut("{id}")]
//         [Auth] // Requires authentication
//         public async Task<IActionResult> UpdateAccount(string id, [FromBody] Account updatedAccount)
//         {
//             if (updatedAccount == null)
//             {
//                 return BadRequest("Invalid account data.");
//             }

//             var result = await _accountsService.UpdateAccountAsync(id, updatedAccount);
//             if (!result)
//             {
//                 return NotFound();
//             }

//             return NoContent();
//         }

//         [HttpDelete("{id}")]
//         [Auth] // Requires authentication
//         public async Task<IActionResult> DeleteAccount(string id)
//         {
//             var result = await _accountsService.DeleteAccountAsync(id);
//             if (!result)
//             {
//                 return NotFound();
//             }

//             return NoContent();
//         }
//     }
// }
