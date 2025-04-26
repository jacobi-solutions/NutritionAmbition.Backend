using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Services;

namespace NutritionAmbition.Backend.API.Services
{
    public interface INutritionService
    {
        Task<NutritionApiResponse> GetNutritionDataForParsedFoodAsync(ParseFoodTextResponse parsedFood);
        Task<NutritionApiResponse> GetNutritionDataForFoodItemAsync(string foodDescription, string quantity = "1", string unit = "serving");
    }

    public class NutritionService : INutritionService
    {
        private readonly IUsdaFoodDataService _usdaFoodDataService;
        private readonly ILogger<NutritionService> _logger;

        public NutritionService(IUsdaFoodDataService usdaFoodDataService, ILogger<NutritionService> logger)
        {
            _usdaFoodDataService = usdaFoodDataService;
            _logger = logger;
        }

        public async Task<NutritionApiResponse> GetNutritionDataForParsedFoodAsync(ParseFoodTextResponse parsedFood)
        {
            var response = new NutritionApiResponse();
            try
            {
                _logger.LogInformation("Getting nutrition data for parsed food with {Count} items", parsedFood.MealItems.Count);
                
                response = new NutritionApiResponse
                {
                    Foods = new List<FoodNutrition>(),
                    IsSuccess = true
                };

                foreach (var item in parsedFood.MealItems)
                {
                    var foodNutrition = await GetFoodNutritionAsync(item.Name, item.Quantity);
                    if (foodNutrition != null)
                    {
                        response.Foods.Add(foodNutrition);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nutrition data for parsed food");
                response.AddError($"Error getting nutrition data: {ex.Message}");
                return response;
            }
        }

        public async Task<NutritionApiResponse> GetNutritionDataForFoodItemAsync(string foodDescription, string quantity = "1", string unit = "serving")
        {
            var response = new NutritionApiResponse();
            try
            {
                _logger.LogInformation("Getting nutrition data for food item: {FoodDescription}", foodDescription);
                
                var foodNutrition = await GetFoodNutritionAsync(foodDescription, quantity, unit);
                
                response = new NutritionApiResponse
                {
                    Foods = new List<FoodNutrition>()
                };

                if (foodNutrition != null)
                {
                    response.Foods.Add(foodNutrition);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nutrition data for food item: {FoodDescription}", foodDescription);
                response.AddError($"Error getting nutrition data: {ex.Message}");
                return response;
            }
        }

        private async Task<FoodNutrition> GetFoodNutritionAsync(string foodDescription, string quantityStr, string unit = "")
        {
            // Parse quantity from string
            double quantity = 1.0;
            if (!string.IsNullOrEmpty(quantityStr))
            {
                // Extract numeric part if quantity contains both number and unit
                var parts = quantityStr.Trim().Split(' ');
                if (parts.Length > 0 && double.TryParse(parts[0], out double parsedQuantity))
                {
                    quantity = parsedQuantity;
                    
                    // If unit wasn't provided but is in the quantity string, extract it
                    if (string.IsNullOrEmpty(unit) && parts.Length > 1)
                    {
                        unit = string.Join(" ", parts.Skip(1));
                    }
                }
            }

            // Search for the food in USDA database
            var searchResults = await _usdaFoodDataService.SearchFoodsAsync(foodDescription, 5);
            if (searchResults == null || !searchResults.Any())
            {
                _logger.LogWarning("No search results found for food: {FoodDescription}", foodDescription);
                return CreateDefaultFoodNutrition(foodDescription, quantityStr, unit);
            }

            // Get details for the first (best match) result
            var bestMatch = searchResults.First();
            var foodDetails = await _usdaFoodDataService.GetFoodDetailsAsync(bestMatch.FdcId);
            
            if (foodDetails == null || foodDetails.Nutrients == null || !foodDetails.Nutrients.Any())
            {
                _logger.LogWarning("No nutrient data found for food: {FoodDescription}", foodDescription);
                return CreateDefaultFoodNutrition(foodDescription, quantityStr, unit);
            }

            // Create FoodNutrition object from USDA data
            var foodNutrition = new FoodNutrition
            {
                Name = foodDetails.Description,
                Quantity = quantityStr,
                Unit = unit
            };

            // Map nutrients to our structure
            MapNutrients(foodDetails, foodNutrition, quantity);

            return foodNutrition;
        }

        private void MapNutrients(FoodDetails foodDetails, FoodNutrition foodNutrition, double quantity)
        {
            // Map calories (Energy)
            var calories = foodDetails.Nutrients.FirstOrDefault(n => n.Name.Contains("Energy") && n.UnitName.Contains("kcal"));
            if (calories != null)
            {
                foodNutrition.Calories = calories.Amount * quantity;
            }

            // Map macronutrients
            MapMacronutrient(foodDetails, "Protein", quantity, value => foodNutrition.Macronutrients.Protein.Amount = value);
            MapMacronutrient(foodDetails, "Carbohydrate", quantity, value => foodNutrition.Macronutrients.Carbohydrates.Amount = value);
            MapMacronutrient(foodDetails, "Total lipid (fat)", quantity, value => foodNutrition.Macronutrients.Fat.Amount = value);
            MapMacronutrient(foodDetails, "Fiber, total dietary", quantity, value => foodNutrition.Macronutrients.Fiber.Amount = value);
            MapMacronutrient(foodDetails, "Sugars, total", quantity, value => foodNutrition.Macronutrients.Sugar.Amount = value);
            MapMacronutrient(foodDetails, "Fatty acids, total saturated", quantity, value => foodNutrition.Macronutrients.SaturatedFat.Amount = value);
            
            // Map micronutrients
            foreach (var nutrient in foodDetails.Nutrients)
            {
                // Skip macronutrients already mapped
                if (IsMacronutrient(nutrient.Name))
                    continue;

                foodNutrition.Micronutrients[nutrient.Name] = new Micronutrient
                {
                    Amount = nutrient.Amount * quantity,
                    Unit = nutrient.UnitName
                };
            }
        }

        private void MapMacronutrient(FoodDetails foodDetails, string nutrientName, double quantity, Action<double> setter)
        {
            var nutrient = foodDetails.Nutrients.FirstOrDefault(n => n.Name.Contains(nutrientName));
            if (nutrient != null)
            {
                setter(nutrient.Amount * quantity);
            }
        }

        private bool IsMacronutrient(string nutrientName)
        {
            var macronutrients = new[]
            {
                "Protein", "Carbohydrate", "Total lipid (fat)", 
                "Fiber, total dietary", "Sugars, total", 
                "Fatty acids, total saturated", "Energy"
            };

            return macronutrients.Any(m => nutrientName.Contains(m));
        }

        private FoodNutrition CreateDefaultFoodNutrition(string foodDescription, string quantity, string unit)
        {
            // Create a default food nutrition object when no data is found
            return new FoodNutrition
            {
                Name = foodDescription,
                Quantity = quantity,
                Unit = unit,
                Calories = 0,
                Macronutrients = new Macronutrients(),
                Micronutrients = new Dictionary<string, Micronutrient>()
            };
        }
    }
}
