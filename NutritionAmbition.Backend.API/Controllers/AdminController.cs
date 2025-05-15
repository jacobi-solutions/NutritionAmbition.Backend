using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace NutritionAmbition.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")] // Ensure only admins can access these endpoints
    public class AdminController : ControllerBase
    {
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ILogger<AdminController> logger)
        {
            _logger = logger;
        }

        // Admin endpoints can be added here as needed
    }
} 