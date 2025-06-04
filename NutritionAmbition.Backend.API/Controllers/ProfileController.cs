using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.DataContracts.Profile;
using NutritionAmbition.Backend.API.Services;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfileController : BaseController
    {
        private readonly IProfileService _profileService;
        private readonly IAccountsService _accountsService;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(IProfileService profileService, ILogger<ProfileController> logger, IAccountsService accountsService) : base(accountsService, logger)
        {
            _profileService = profileService;
            _logger = logger;
            _accountsService = accountsService;
        }

        [HttpPost("SaveUserProfile")]
        public async Task<ActionResult<SaveUserProfileResponse>> SaveUserProfile([FromBody] SaveUserProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _profileService.SaveUserProfileAsync(request, _account);
            
            if (response.IsSuccess)
            {
                return Ok(response);
            }
            
            return BadRequest(response);
        }

        [HttpPost("GetProfileAndGoals")]
        public async Task<ActionResult<GetProfileAndGoalsResponse>> GetProfileAndGoals([FromBody] Request request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _profileService.GetProfileAndGoalsAsync(_account);
            
            if (response.IsSuccess)
            {
                return Ok(response);
            }
            
            return BadRequest(response);
        }
    }
} 