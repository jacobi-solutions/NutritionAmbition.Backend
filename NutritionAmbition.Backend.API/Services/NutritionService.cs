using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.Services
{
    public interface INutritionService
    {
        Task<NutritionApiResponse> GetNutritionDataForFoodItemAsync(string accountId, string foodDescription);
        Task<NutritionApiResponse> ProcessFoodTextAndGetNutritionAsync(string accountId, string foodDescription);
        Task<NutritionApiResponse> GetSmartNutritionDataAsync(string accountId, string foodDescription);
    }

    public class NutritionService : INutritionService
    {
        private readonly INutritionixService _nutritionixService;
        private readonly IOpenAiService _openAiService;
        private readonly IFoodEntryService _foodEntryService;
        private readonly ILogger<NutritionService> _logger;

        public NutritionService(INutritionixService nutritionixService, IOpenAiService openAiService, IFoodEntryService foodEntryService, ILogger<NutritionService> logger)
        {
            _nutritionixService = nutritionixService;
            _openAiService = openAiService;
            _foodEntryService = foodEntryService;
            _logger = logger;
        }

        public async Task<NutritionApiResponse> GetNutritionDataForFoodItemAsync(string accountId, string foodDescription)
        {
            // ... existing implementation ...
            var response = new NutritionApiResponse();
            try
            {
                _logger.LogInformation("Getting nutrition data for food item via Nutritionix: {FoodDescription}", foodDescription);
                
                var nutritionixResponse = await _nutritionixService.GetNutritionDataAsync(foodDescription);

                if (nutritionixResponse == null || !nutritionixResponse.Foods.Any())
                {
                    _logger.LogWarning("Nutritionix returned no data for: {FoodDescription}", foodDescription);
                    response.AddError("Could not find nutrition data for the specified food.");
                    return response;
                }

                response.Foods = MapNutritionixResponseToFoodNutrition(nutritionixResponse);
                response.IsSuccess = true;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nutrition data for food item: {FoodDescription}", foodDescription);
                response.AddError($"Error getting nutrition data: {ex.Message}");
                return response;
            }
        }

        public async Task<NutritionApiResponse> ProcessFoodTextAndGetNutritionAsync(string accountId, string foodDescription)
        {
            var response = new NutritionApiResponse();
            try
            {
                _logger.LogInformation("Processing food text and getting nutrition data via Nutritionix for Account {AccountId}: {FoodDescription}", accountId, foodDescription);

                // 1. Get nutrition data from Nutritionix
                var nutritionixResponse = await _nutritionixService.GetNutritionDataAsync(foodDescription);

                if (nutritionixResponse == null || !nutritionixResponse.Foods.Any())
                {
                    _logger.LogWarning("Nutritionix returned no data for: {FoodDescription}", foodDescription);
                    response.AddError("Could not find nutrition data for the specified food description.");
                    response.AiCoachResponse = await GenerateFallbackCoachResponseAsync(foodDescription);
                    return response;
                }

                // 2. Map Nutritionix response to our internal FoodNutrition structure
                response.Foods = MapNutritionixResponseToFoodNutrition(nutritionixResponse);
                response.IsSuccess = true;

                // 3. 🟢 Save the FoodEntry to the database (with grouping)
                if (response.IsSuccess && response.Foods.Any())
                {
                    try
                    {
                        // Map FoodNutrition to FoodItem for grouping and saving
                        var parsedItems = MapFoodNutritionToFoodItem(response.Foods);

                        // 🟢 Call AI to group the items
                        var groupedItems = await _openAiService.GroupFoodItemsAsync(foodDescription, parsedItems);

                        // If grouping failed, fallback to individual groups
                        if (groupedItems == null || !groupedItems.Any())
                        {
                            _logger.LogWarning("AI grouping failed or returned empty list, falling back to individual groups");
                            groupedItems = parsedItems.Select(x => new FoodGroup { GroupName = x.Name, Items = new List<FoodItem> { x } }).ToList();
                        }

                        var createFoodEntryRequest = new CreateFoodEntryRequest
                        {
                            Description = foodDescription,
                            Meal = MealType.Unknown, // Default for now
                            LoggedDateUtc = DateTime.UtcNow,
                            // 🟢 Use GroupedItems instead of ParsedItems
                            GroupedItems = groupedItems 
                        };
                        
                        var saveResponse = await _foodEntryService.AddFoodEntryAsync(accountId, createFoodEntryRequest);
                        if (!saveResponse.IsSuccess)
                        {
                            _logger.LogWarning("Failed to save food entry for Account {AccountId}: {Errors}", accountId, string.Join(", ", saveResponse.Errors));
                        }
                        else
                        {
                            _logger.LogInformation("Successfully saved food entry {FoodEntryId} for Account {AccountId}", saveResponse.FoodEntry?.Id, accountId);
                        }
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx, "Error grouping or saving food entry for Account {AccountId} and description {FoodDescription}", accountId, foodDescription);
                    }
                }

                // 4. Generate AI coach response
                if (response.IsSuccess && response.Foods.Any())
                {
                    try
                    {
                        // Use the first food item for the coach response context (or potentially summarize all)
                        response.AiCoachResponse = await _openAiService.GenerateCoachResponseAsync(foodDescription, response.Foods[0]);
                    }
                    catch (Exception coachEx)
                    {
                        _logger.LogWarning(coachEx, "Failed to generate AI coach response for: {FoodDescription}", foodDescription);
                        response.AiCoachResponse = "Logged!";
                    }
                }
                else
                {
                    response.AiCoachResponse = await GenerateFallbackCoachResponseAsync(foodDescription);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing food text and getting nutrition data for Account {AccountId}: {FoodDescription}", accountId, foodDescription);
                response.AddError($"Error processing food text: {ex.Message}");
                response.AiCoachResponse = "Sorry, an error occurred while processing your request.";
                return response;
            }
        }

        public async Task<NutritionApiResponse> GetSmartNutritionDataAsync(string accountId, string foodDescription)
        {
            var response = new NutritionApiResponse();
            try
            {
                _logger.LogInformation("Smart nutrition lookup for account {AccountId}: {FoodDescription}", accountId, foodDescription);

                // 1. First, parse the food description using OpenAI to identify individual items and whether they're branded
                var parsedFoodsResponse = await _openAiService.ParseFoodTextAsync(foodDescription);
                
                // Log the number of parsed foods and their details
                _logger.LogInformation("OpenAI parsed {Count} foods from description", 
                    parsedFoodsResponse.Foods?.Count ?? 0);
                    
                if (parsedFoodsResponse.Foods != null)
                {
                    foreach (var food in parsedFoodsResponse.Foods)
                    {
                        _logger.LogInformation("Parsed food: Name={Name}, Quantity={Quantity}, Unit={Unit}, IsBranded={IsBranded}", 
                            food.Name, food.Quantity, food.Unit, food.IsBranded);
                    }
                }
                
                if (!parsedFoodsResponse.IsSuccess)
                {
                    _logger.LogWarning("Failed to parse food description: {Errors}", 
                        string.Join(", ", parsedFoodsResponse.Errors.Select(e => e.ErrorMessage)));
                    response.AddError("Failed to parse food description. Please try with a clearer description.");
                    return response;
                }
                
                if (parsedFoodsResponse.Foods == null || !parsedFoodsResponse.Foods.Any())
                {
                    _logger.LogWarning("No foods identified in description: {FoodDescription}", foodDescription);
                    response.AddError("No food items could be identified from the description.");
                    return response;
                }
                
                // 2. Split the parsed foods into branded and generic items
                var brandedItems = parsedFoodsResponse.Foods.Where(f => f.IsBranded).ToList();
                var genericItems = parsedFoodsResponse.Foods.Where(f => !f.IsBranded).ToList();
                
                _logger.LogInformation("Found {BrandedCount} branded items and {GenericCount} generic items", 
                    brandedItems.Count, genericItems.Count);
                
                // Lists to collect the results
                var allFoodNutrition = new List<FoodNutrition>();
                var brandedProcessed = 0;
                var genericProcessed = 0;
                
                // 3. Process branded items - look them up individually for more precise results
                foreach (var brandedItem in brandedItems)
                {
                    try
                    {
                        _logger.LogInformation("Looking up branded item: {Name} ({Quantity} {Unit})",
                            brandedItem.Name, brandedItem.Quantity, brandedItem.Unit);
                        
                        // Search for the branded item
                        var searchResults = await _nutritionixService.SearchInstantAsync(brandedItem.Name);
                        
                        if (searchResults.Branded.Count > 0)
                        {
                            // Try to find a confident match
                            foreach (var result in searchResults.Branded)
                            {
                                bool isConfident = await _nutritionixService.IsBrandedItemConfident(brandedItem.Name, result);
                                
                                if (isConfident)
                                {
                                    _logger.LogInformation("Found confident branded match: {BrandName} {FoodName}", 
                                        result.BrandName, result.FoodName);
                                    
                                    // Get detailed nutrition data
                                    string brandedQuery = $"{result.BrandName} {result.FoodName}";
                                    var nutritionixResponse = await _nutritionixService.GetNutritionDataAsync(brandedQuery);
                                    
                                    if (nutritionixResponse != null && nutritionixResponse.Foods.Any())
                                    {
                                        var foods = MapNutritionixResponseToFoodNutrition(nutritionixResponse);
                                        allFoodNutrition.AddRange(foods);
                                        brandedProcessed++;
                                        break; // Found a match, move to next item
                                    }
                                }
                            }
                        }
                        
                        if (brandedProcessed < 1) // If no branded match was found
                        {
                            _logger.LogWarning("No confident branded match found for: {Name}", brandedItem.Name);
                            // We'll handle this item as generic
                            genericItems.Add(brandedItem);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing branded item: {Name}", brandedItem.Name);
                        // Continue with other items
                    }
                }
                
                // 4. Process generic items - combine them for a single query if there are any
                if (genericItems.Any())
                {
                    try
                    {
                        // Build a combined query for all generic items
                        var combinedQuery = string.Join(", ", genericItems.Select(item => 
                            $"{item.Quantity} {item.Unit} {item.Name}".Trim()));
                        
                        _logger.LogInformation("Looking up combined generic items: {Query}", combinedQuery);
                        
                        var nutritionixResponse = await _nutritionixService.GetNutritionDataAsync(combinedQuery);
                        
                        if (nutritionixResponse != null && nutritionixResponse.Foods.Any())
                        {
                            var foods = MapNutritionixResponseToFoodNutrition(nutritionixResponse);
                            allFoodNutrition.AddRange(foods);
                            genericProcessed = nutritionixResponse.Foods.Count;
                        }
                        else
                        {
                            _logger.LogWarning("No nutrition data found for generic items: {Query}", combinedQuery);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing generic items");
                    }
                }
                
                // 5. Check if we found any nutrition data
                if (allFoodNutrition.Any())
                {
                    response.Foods = allFoodNutrition;
                    response.IsSuccess = true;
                    response.Source = "smart";
                    
                    _logger.LogInformation("Smart nutrition lookup successful: Found data for {BrandedCount} branded and {GenericCount} generic items", 
                        brandedProcessed, genericProcessed);
                    
                    // Save the food entry to the database with grouping
                    try
                    {
                        // Map FoodNutrition to FoodItem for grouping and saving
                        var parsedItems = MapFoodNutritionToFoodItem(allFoodNutrition);
                        
                        // Group the food items
                        var groupedItems = await _openAiService.GroupFoodItemsAsync(foodDescription, parsedItems);
                        
                        // If grouping failed, fallback to individual groups
                        if (groupedItems == null || !groupedItems.Any())
                        {
                            _logger.LogWarning("AI grouping failed or returned empty list, falling back to individual groups");
                            groupedItems = parsedItems.Select(x => new FoodGroup { GroupName = x.Name, Items = new List<FoodItem> { x } }).ToList();
                        }
                        
                        // Create the food entry request
                        var createFoodEntryRequest = new CreateFoodEntryRequest
                        {
                            Description = foodDescription,
                            Meal = MealType.Unknown, // Default for now
                            LoggedDateUtc = DateTime.UtcNow,
                            GroupedItems = groupedItems
                        };
                        
                        // Save the food entry
                        var saveResponse = await _foodEntryService.AddFoodEntryAsync(accountId, createFoodEntryRequest);
                        
                        if (!saveResponse.IsSuccess)
                        {
                            _logger.LogWarning("Failed to save food entry for Account {AccountId}: {Errors}", 
                                accountId, string.Join(", ", saveResponse.Errors));
                        }
                        else
                        {
                            _logger.LogInformation("Successfully saved food entry {FoodEntryId} for Account {AccountId}", 
                                saveResponse.FoodEntry?.Id, accountId);
                        }
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx, "Error grouping or saving food entry for Account {AccountId} and description {FoodDescription}", 
                            accountId, foodDescription);
                        // Continue with the response even if saving fails
                    }
                    
                    // Generate AI coach response based on the first food item
                    try
                    {
                        response.AiCoachResponse = await _openAiService.GenerateCoachResponseAsync(foodDescription, allFoodNutrition[0]);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate AI coach response");
                        response.AiCoachResponse = "Logged!";
                    }
                    
                    return response;
                }
                else
                {
                    // If no nutrition data found, return an error
                    _logger.LogWarning("No nutrition data found for any items in: {FoodDescription}", foodDescription);
                    response.AddError("Could not find nutrition data for the specified food items.");
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in smart nutrition lookup for account {AccountId}: {FoodDescription}", 
                    accountId, foodDescription);
                
                response.AddError($"Error getting nutrition data: {ex.Message}");
                return response;
            }
        }

        // ... MapNutritionixResponseToFoodNutrition (unchanged) ...
        private List<FoodNutrition> MapNutritionixResponseToFoodNutrition(NutritionixResponse nutritionixResponse)
        {
            var mappedFoods = new List<FoodNutrition>();

            foreach (var food in nutritionixResponse.Foods)
            {
                var foodNutrition = new FoodNutrition
                {
                    Name = food.FoodName,
                    Quantity = food.ServingQty.ToString(),
                    Unit = food.ServingUnit,
                    Calories = food.Calories ?? 0,
                    Macronutrients = new Macronutrients
                    {
                        Protein = new NutrientInfo { Amount = food.Protein ?? 0, Unit = "g" },
                        Carbohydrates = new NutrientInfo { Amount = food.TotalCarbohydrate ?? 0, Unit = "g" },
                        Fat = new NutrientInfo { Amount = food.TotalFat ?? 0, Unit = "g" },
                        Fiber = new NutrientInfo { Amount = food.DietaryFiber ?? 0, Unit = "g" },
                        Sugar = new NutrientInfo { Amount = food.Sugars ?? 0, Unit = "g" },
                        SaturatedFat = new NutrientInfo { Amount = food.SaturatedFat ?? 0, Unit = "g" }
                    },
                    Micronutrients = new Dictionary<string, Micronutrient>()
                };

                // Map micronutrients from FullNutrients
                if (food.FullNutrients != null)
                {
                    foreach (var nutrient in food.FullNutrients)
                    {
                        // Basic mapping based on common attr_ids (can be expanded)
                        string? nutrientName = nutrient.AttrId switch
                        {
                            301 => "Calcium",
                            303 => "Iron",
                            304 => "Magnesium",
                            305 => "Phosphorus",
                            306 => "Potassium",
                            307 => "Sodium",
                            309 => "Zinc",
                            312 => "Copper",
                            315 => "Manganese",
                            317 => "Selenium",
                            401 => "Vitamin C",
                            404 => "Thiamin", // B1
                            405 => "Riboflavin", // B2
                            406 => "Niacin", // B3
                            410 => "Pantothenic Acid", // B5
                            415 => "Vitamin B6",
                            417 => "Folate", // B9
                            418 => "Vitamin B12",
                            320 => "Vitamin A", // RAE
                            323 => "Vitamin E",
                            328 => "Vitamin D", // D2 + D3
                            430 => "Vitamin K",
                            _ => null
                        };

                        if (nutrientName != null)
                        {
                            // Determine unit (most are mg, some mcg or IU)
                            string unit = nutrient.AttrId switch
                            {
                                328 => "IU", // Vitamin D often in IU
                                417 => "mcg", // Folate often in mcg (DFE)
                                418 => "mcg", // B12 often in mcg
                                430 => "mcg", // Vitamin K often in mcg
                                320 => "mcg", // Vitamin A often in mcg (RAE)
                                _ => "mg"
                            };
                            foodNutrition.Micronutrients[nutrientName] = new Micronutrient { Amount = nutrient.Value, Unit = unit };
                        }
                    }
                }
                mappedFoods.Add(foodNutrition);
            }
            return mappedFoods;
        }

        // ... MapFoodNutritionToFoodItem (unchanged) ...
        private List<FoodItem> MapFoodNutritionToFoodItem(List<FoodNutrition> foodNutritions)
        {
            var foodItems = new List<FoodItem>();
            foreach (var fn in foodNutritions)
            {
                var fi = new FoodItem
                {
                    Name = fn.Name,
                    Quantity = double.TryParse(fn.Quantity, out double qty) ? qty : 1.0,
                    Unit = fn.Unit ?? string.Empty,
                    Calories = fn.Calories,
                    Protein = fn.Macronutrients.Protein.Amount,
                    Carbohydrates = fn.Macronutrients.Carbohydrates.Amount,
                    Fat = fn.Macronutrients.Fat.Amount,
                    Micronutrients = fn.Micronutrients.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Amount)
                };
                foodItems.Add(fi);
            }
            return foodItems;
        }

        // ... GenerateFallbackCoachResponseAsync (unchanged) ...
        private async Task<string> GenerateFallbackCoachResponseAsync(string foodDescription)
        {
            try
            {
                return await _openAiService.GenerateCoachResponseAsync(foodDescription, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate fallback AI coach response for: {FoodDescription}", foodDescription);
                return "Sorry, I couldn\'t find nutrition data, but I\'ve noted the description.";
            }
        }
    }
}

