using System.Threading.Tasks;
using NutritionAmbition.Backend.API.DataContracts;
using Microsoft.Extensions.Logging;

namespace NutritionAmbition.Backend.API.Services
{
    // Implementation class for IFoodParsingService
    public class FoodParsingService : IFoodParsingService
    {
        private readonly IOpenAiService _openAiService;
        private readonly ILogger<FoodParsingService> _logger;

        public FoodParsingService(
            IOpenAiService openAiService, 
            ILogger<FoodParsingService> logger)
        {
            _openAiService = openAiService;
            _logger = logger;
        }

        public async Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription)
        {
            _logger.LogInformation("Parsing food text: {FoodDescription}", foodDescription);
            return await _openAiService.ParseFoodTextAsync(foodDescription);
        }

        // Removed GetNutritionDataAsync method as it's no longer needed here.
        // The NutritionService.ProcessFoodTextAndGetNutritionAsync handles the end-to-end flow.
    }
}

