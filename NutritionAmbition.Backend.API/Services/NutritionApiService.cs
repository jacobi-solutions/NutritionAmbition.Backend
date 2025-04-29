using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Settings;

namespace NutritionAmbition.Backend.API.Services
{
    public interface INutritionApiService
    {
        Task<NutritionApiResponse> GetNutritionDataAsync(NutritionApiRequest request);
    }

    public class NutritionApiService : INutritionApiService
    {
        private readonly ILogger<NutritionApiService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _nutritionApiKey;
        private readonly string _nutritionApiEndpoint;
        private readonly NutritionixSettings _nutritionApiSettings;

        public NutritionApiService(ILogger<NutritionApiService> logger, HttpClient httpClient, NutritionixSettings nutritionApiSettings)
        {
            _logger = logger;
            _httpClient = httpClient;
            _nutritionApiSettings = nutritionApiSettings;
        }

        public async Task<NutritionApiResponse> GetNutritionDataAsync(NutritionApiRequest request)
        {
            var response = new NutritionApiResponse();
            try
            {
                _logger.LogInformation("Getting nutrition data for {Count} ingredients", request.Ingredients.Count);

                // This is a placeholder for the actual Nutrition API call
                // In a real implementation, this would call a nutrition data API
                
                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                
                // In a real implementation, we would add the API key to the headers
                // _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _nutritionApiKey);
                
                // For now, we'll simulate the nutrition API response
                // var response = await _httpClient.PostAsync(_nutritionApiEndpoint, content);
                // response.EnsureSuccessStatusCode();
                // var responseContent = await response.Content.ReadAsStringAsync();
                // return JsonSerializer.Deserialize<NutritionApiResponse>(responseContent);
                
                // Simulate nutrition API response for development
                return SimulateNutritionApiResponse(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nutrition data");
                response.AddError($"Error getting nutrition data: {ex.Message}");
                return response;
            }
        }

        private NutritionApiResponse SimulateNutritionApiResponse(NutritionApiRequest request)
        {
            // This is a simple simulation of what the nutrition API would return
            // In a real implementation, this would be replaced with actual API calls
            
            var response = new NutritionApiResponse
            {
                Foods = new List<FoodNutrition>()
            };

            foreach (var ingredient in request.Ingredients)
            {
                var foodNutrition = new FoodNutrition
                {
                    Name = ingredient.Name,
                    Quantity = ingredient.Quantity,
                    Unit = ingredient.Unit
                };

                // Simulate different nutrition values based on the food name
                if (ingredient.Name.Contains("Egg", StringComparison.OrdinalIgnoreCase) && 
                    ingredient.Name.Contains("Burrito", StringComparison.OrdinalIgnoreCase))
                {
                    foodNutrition.Calories = 350;
                    foodNutrition.Macronutrients.Protein.Amount = 15;
                    foodNutrition.Macronutrients.Carbohydrates.Amount = 30;
                    foodNutrition.Macronutrients.Fat.Amount = 18;
                    foodNutrition.Macronutrients.Fiber.Amount = 3;
                    foodNutrition.Macronutrients.Sugar.Amount = 2;
                    foodNutrition.Macronutrients.SaturatedFat.Amount = 5;
                    
                    foodNutrition.Micronutrients["Vitamin A"] = new Micronutrient { Amount = 150, Unit = "mcg", DailyValuePercent = 15 };
                    foodNutrition.Micronutrients["Vitamin C"] = new Micronutrient { Amount = 5, Unit = "mg", DailyValuePercent = 5 };
                    foodNutrition.Micronutrients["Calcium"] = new Micronutrient { Amount = 200, Unit = "mg", DailyValuePercent = 20 };
                    foodNutrition.Micronutrients["Iron"] = new Micronutrient { Amount = 2.5, Unit = "mg", DailyValuePercent = 15 };
                }
                else if (ingredient.Name.Contains("Fruit", StringComparison.OrdinalIgnoreCase))
                {
                    foodNutrition.Calories = 80;
                    foodNutrition.Macronutrients.Protein.Amount = 1;
                    foodNutrition.Macronutrients.Carbohydrates.Amount = 20;
                    foodNutrition.Macronutrients.Fat.Amount = 0;
                    foodNutrition.Macronutrients.Fiber.Amount = 3;
                    foodNutrition.Macronutrients.Sugar.Amount = 15;
                    
                    foodNutrition.Micronutrients["Vitamin A"] = new Micronutrient { Amount = 100, Unit = "mcg", DailyValuePercent = 10 };
                    foodNutrition.Micronutrients["Vitamin C"] = new Micronutrient { Amount = 30, Unit = "mg", DailyValuePercent = 30 };
                    foodNutrition.Micronutrients["Potassium"] = new Micronutrient { Amount = 250, Unit = "mg", DailyValuePercent = 5 };
                }
                else if (ingredient.Name.Contains("Coffee", StringComparison.OrdinalIgnoreCase))
                {
                    foodNutrition.Calories = 5;
                    foodNutrition.Macronutrients.Protein.Amount = 0;
                    foodNutrition.Macronutrients.Carbohydrates.Amount = 0;
                    foodNutrition.Macronutrients.Fat.Amount = 0;
                    
                    foodNutrition.Micronutrients["Magnesium"] = new Micronutrient { Amount = 7, Unit = "mg", DailyValuePercent = 2 };
                    foodNutrition.Micronutrients["Niacin"] = new Micronutrient { Amount = 0.5, Unit = "mg", DailyValuePercent = 3 };
                }
                else
                {
                    // Generic nutrition values for unknown foods
                    foodNutrition.Calories = 100;
                    foodNutrition.Macronutrients.Protein.Amount = 5;
                    foodNutrition.Macronutrients.Carbohydrates.Amount = 15;
                    foodNutrition.Macronutrients.Fat.Amount = 3;
                }

                response.Foods.Add(foodNutrition);
            }

            return response;
        }
    }
}
