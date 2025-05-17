using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Attributes;
using NutritionAmbition.Backend.API.DataContracts.Profile;
using NutritionAmbition.Backend.API.Services;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [FlexibleAuthorize]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(IProfileService profileService, ILogger<ProfileController> logger)
        {
            _profileService = profileService;
            _logger = logger;
        }

        [HttpPost("SaveUserProfile")]
        public async Task<ActionResult<SaveUserProfileResponse>> SaveUserProfile([FromBody] SaveUserProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _profileService.SaveUserProfileAsync(request);
            
            if (response.IsSuccess)
            {
                return Ok(response);
            }
            
            return BadRequest(response);
        }

        [HttpPost("GetProfileAndGoals")]
        public async Task<ActionResult<GetProfileAndGoalsResponse>> GetProfileAndGoals([FromBody] GetProfileAndGoalsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _profileService.GetProfileAndGoalsAsync(request);
            
            if (response.IsSuccess)
            {
                return Ok(response);
            }
            
            return BadRequest(response);
        }
    }
} 