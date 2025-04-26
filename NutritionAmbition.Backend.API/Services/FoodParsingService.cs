using System.Threading.Tasks;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Services;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IFoodParsingService
    {
        Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription);
        Task<NutritionApiResponse> GetNutritionDataAsync(ParseFoodTextResponse parsedFood);
    }

    public class FoodParsingService : IFoodParsingService
    {
        private readonly IOpenAiService _openAiService;
        private readonly INutritionService _nutritionService;
        private readonly ILogger<FoodParsingService> _logger;

        public FoodParsingService(
            IOpenAiService openAiService, 
            INutritionService nutritionService,
            ILogger<FoodParsingService> logger)
        {
            _openAiService = openAiService;
            _nutritionService = nutritionService;
            _logger = logger;
        }

        public async Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription)
        {
            _logger.LogInformation("Parsing food text: {FoodDescription}", foodDescription);
            return await _openAiService.ParseFoodTextAsync(foodDescription);
        }

        public async Task<NutritionApiResponse> GetNutritionDataAsync(ParseFoodTextResponse parsedFood)
        {
            _logger.LogInformation("Getting nutrition data for {Count} parsed food items", parsedFood.MealItems.Count);
            return await _nutritionService.GetNutritionDataForParsedFoodAsync(parsedFood);
        }
    }
}
