using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Constants;

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
        private readonly NutritionixClient _nutritionixClient;
        private readonly INutritionCalculationService _nutritionCalculationService;
        private readonly IDailySummaryService _dailySummaryService;

        public NutritionService(
            INutritionixService nutritionixService,
            IOpenAiService openAiService,
            IFoodEntryService foodEntryService, 
            ILogger<NutritionService> logger,
            NutritionixClient nutritionixClient,
            INutritionCalculationService nutritionCalculationService,
            IDailySummaryService dailySummaryService)
        {
            _nutritionixService = nutritionixService;
            _openAiService = openAiService;
            _foodEntryService = foodEntryService;
            _logger = logger;
            _nutritionixClient = nutritionixClient;
            _nutritionCalculationService = nutritionCalculationService;
            _dailySummaryService = dailySummaryService;
        }

        private async Task<NutritionixFood> GetBrandedNutritionDataAsync(string nixItemId)
        {
            // Call Nutritionix API to fetch nutrition data by NixItemId
            var response = await _nutritionixClient.GetNutritionByItemIdAsync(nixItemId);
            if (response == null)
            {
                throw new InvalidOperationException($"Nutritionix returned no data for NixItemId {nixItemId}");
            }
            return response;
        }

        private async Task<NutritionixFood> GetGenericNutritionDataAsync(string tagName)
        {
            // Call Nutritionix API to fetch generic food nutrition data by TagName
            var response = await _nutritionixClient.GetNutritionByTagNameAsync(tagName);
            if (response == null)
            {
                throw new InvalidOperationException($"Nutritionix returned no data for TagName {tagName}");
            }
            return response;
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

                var foodItems = MapNutritionixResponseToFoodItem(nutritionixResponse);
                response.Foods = ConvertFoodItemsToFoodNutrition(foodItems);
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
                    return response;
                }

                // 2. Map Nutritionix response to our internal FoodItem structure
                var foodItems = MapNutritionixResponseToFoodItem(nutritionixResponse);
                response.Foods = ConvertFoodItemsToFoodNutrition(foodItems);
                response.IsSuccess = true;

                // 3. 🟢 Save the FoodEntry to the database (with grouping)
                if (response.IsSuccess && response.Foods.Any())
                {
                    try
                    {
                        // Use the already mapped foodItems directly
                        var parsedItems = foodItems;

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

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing food text and getting nutrition data for Account {AccountId}: {FoodDescription}", accountId, foodDescription);
                response.AddError($"Error processing food text: {ex.Message}");
                return response;
            }
        }

        public async Task<NutritionApiResponse> GetSmartNutritionDataAsync(string accountId, string foodDescription)
        {
            var response = new NutritionApiResponse();
            try
            {
                _logger.LogInformation("Smart nutrition lookup for account {AccountId}: {FoodDescription}", accountId, foodDescription);

                // Track items that couldn't be matched
                var missingItems = new List<string>();

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
                var allFoodItems = new List<FoodItem>();
                var brandedProcessed = 0;
                var genericProcessed = 0;
                
                // 3. Process branded items - look them up individually for more precise results
                foreach (var brandedItem in brandedItems)
                {
                    try
                    {
                        _logger.LogInformation("Looking up branded item: {Name} ({Quantity} {Unit})",
                            brandedItem.Name, brandedItem.Quantity, brandedItem.Unit);
                        
                        // Build a more natural search query combining all information
                        string searchQuery = BuildSearchQuery(brandedItem);
                        _logger.LogInformation("Using search query: {SearchQuery}", searchQuery);
                        
                        // Search for the branded item
                        var searchResults = await _nutritionixService.SearchInstantAsync(searchQuery);
                        
                        if (searchResults.Branded.Count > 0)
                        {
                            _logger.LogInformation("Found {Count} branded food options for: {Name}", 
                                searchResults.Branded.Count, brandedItem.Name);
                            
                            // Use OpenAI to select the best branded food match
                            int selectedIndex = await _openAiService.SelectBestBrandedFoodAsync(brandedItem.Name, brandedItem.Quantity, brandedItem.Unit, searchResults.Branded);
                            
                            if (selectedIndex >= 0 && selectedIndex < searchResults.Branded.Count)
                            {
                                var selectedBrandedItem = searchResults.Branded[selectedIndex];
                                _logger.LogInformation("Selected branded food: {BrandName} {FoodName}", 
                                    selectedBrandedItem.BrandName, selectedBrandedItem.FoodName);
                                
                                try
                                {
                                    // Get detailed nutrition data using the Nix Item ID
                                    var brandedNutritionData = await GetBrandedNutritionDataAsync(selectedBrandedItem.NixItemId);
                                    
                                    if (brandedNutritionData != null)
                                    {
                                        // Create a response to match the expected format
                                        var nutritionixResponse = new NutritionixResponse 
                                        { 
                                            Foods = new List<NutritionixFood> { brandedNutritionData } 
                                        };
                                        
                                        var foodItems = MapNutritionixResponseToFoodItem(nutritionixResponse);
                                        
                                        foreach (var foodItem in foodItems)
                                        {
                                            foodItem.Quantity = brandedItem.Quantity > 0 ? brandedItem.Quantity : foodItem.Quantity;
                                            foodItem.Unit = string.IsNullOrWhiteSpace(foodItem.Unit) && !string.IsNullOrWhiteSpace(brandedItem.Unit)
                                                ? brandedItem.Unit
                                                : foodItem.Unit;
                                        }
                                        
                                        allFoodItems.AddRange(foodItems);
                                        brandedProcessed++;
                                        
                                        _logger.LogInformation("Successfully retrieved nutrition data for branded food with NixItemId: {NixItemId}", 
                                            selectedBrandedItem.NixItemId);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Failed to get nutrition data for selected branded food with NixItemId: {NixItemId}", 
                                            selectedBrandedItem.NixItemId);
                                        genericItems.Add(brandedItem); // Process as generic if nutrition data retrieval failed
                                        missingItems.Add(brandedItem.Name);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error fetching nutrition data for branded food with NixItemId: {NixItemId}", 
                                        selectedBrandedItem.NixItemId);
                                    genericItems.Add(brandedItem); // Process as generic if nutrition data retrieval failed
                                    missingItems.Add(brandedItem.Name);
                                }
                            }
                            else
                            {
                                _logger.LogInformation("No confident branded match found by AI for: {Name}", brandedItem.Name);
                                genericItems.Add(brandedItem); // Process as generic if no confident match
                                missingItems.Add(brandedItem.Name);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("No branded items found for: {Name}", brandedItem.Name);
                            genericItems.Add(brandedItem); // Process as generic if no search results
                            missingItems.Add(brandedItem.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing branded item: {Name}", brandedItem.Name);
                        // Continue with other items
                    }
                }
                
                // 4. Process generic items - process each item individually with AI selection
                if (genericItems.Any())
                {
                    _logger.LogInformation("Processing {Count} generic items individually", genericItems.Count);
                    
                    foreach (var genericItem in genericItems)
                    {
                        try
                        {
                            _logger.LogInformation("Looking up generic item: {Name} ({Quantity} {Unit})",
                                genericItem.Name, genericItem.Quantity, genericItem.Unit);
                            
                            // Build a more natural search query combining all information
                            string searchQuery = BuildSearchQuery(genericItem);
                            _logger.LogInformation("Using search query: {SearchQuery}", searchQuery);
                            
                            // Search for the generic item
                            var searchResults = await _nutritionixService.SearchInstantAsync(searchQuery);
                            
                            if (searchResults.Common.Count > 0)
                            {
                                _logger.LogInformation("Found {Count} generic food options for: {Name}", 
                                    searchResults.Common.Count, genericItem.Name);
                                
                                // Use OpenAI to select the best generic food match
                                int selectedIndex = await _openAiService.SelectBestGenericFoodAsync(genericItem.Name, searchResults.Common);
                                
                                if (selectedIndex >= 0 && selectedIndex < searchResults.Common.Count)
                                {
                                    var selectedCommonItem = searchResults.Common[selectedIndex];
                                    _logger.LogInformation("Selected generic food: {FoodName}", selectedCommonItem.FoodName);
                                    
                                    try
                                    {
                                        // Get detailed nutrition data using the TagName
                                        if (!string.IsNullOrEmpty(selectedCommonItem.TagName))
                                        {
                                            var genericNutritionData = await GetGenericNutritionDataAsync(selectedCommonItem.TagName);
                                            
                                            if (genericNutritionData != null)
                                            {
                                                // Create a response to match the expected format
                                                var nutritionixResponse = new NutritionixResponse 
                                                { 
                                                    Foods = new List<NutritionixFood> { genericNutritionData } 
                                                };
                                                
                                                var foodItems = MapNutritionixResponseToFoodItem(nutritionixResponse);
                                                
                                                foreach (var foodItem in foodItems)
                                                {
                                                    foodItem.Quantity = genericItem.Quantity > 0 ? genericItem.Quantity : foodItem.Quantity;
                                                    foodItem.Unit = string.IsNullOrWhiteSpace(foodItem.Unit) && !string.IsNullOrWhiteSpace(genericItem.Unit)
                                                        ? genericItem.Unit
                                                        : foodItem.Unit;
                                                }
                                                
                                                allFoodItems.AddRange(foodItems);
                                                genericProcessed++;
                                                
                                                _logger.LogInformation("Successfully retrieved nutrition data for generic food with TagName: {TagName}", 
                                                    selectedCommonItem.TagName);
                                            }
                                            else
                                            {
                                                _logger.LogWarning("Failed to get nutrition data for selected generic food with TagName: {TagName}", 
                                                    selectedCommonItem.TagName);
                                                missingItems.Add(genericItem.Name);
                                            }
                                        }
                                        else
                                        {
                                            // Fallback to using food name if TagName is not available
                                            _logger.LogWarning("No TagName available for selected generic food, falling back to food name: {FoodName}", 
                                                selectedCommonItem.FoodName);
                                                
                                            // Create a basic ParsedFoodItem to use with our query builder
                                            var queryItem = new ParsedFoodItem 
                                            { 
                                                Name = selectedCommonItem.FoodName,
                                                Quantity = genericItem.Quantity,
                                                Unit = genericItem.Unit
                                            };
                                            
                                            string fallbackSearchQuery = BuildSearchQuery(queryItem);
                                            _logger.LogInformation("Using fallback search query: {SearchQuery}", fallbackSearchQuery);
                                                
                                            var nutritionixResponse = await _nutritionixService.GetNutritionDataAsync(fallbackSearchQuery);
                                            
                                            if (nutritionixResponse != null && nutritionixResponse.Foods.Any())
                                            {
                                                var foodItems = MapNutritionixResponseToFoodItem(nutritionixResponse);
                                                
                                                foreach (var foodItem in foodItems)
                                                {
                                                    foodItem.Quantity = genericItem.Quantity > 0 ? genericItem.Quantity : foodItem.Quantity;
                                                    foodItem.Unit = string.IsNullOrWhiteSpace(foodItem.Unit) && !string.IsNullOrWhiteSpace(genericItem.Unit)
                                                        ? genericItem.Unit
                                                        : foodItem.Unit;
                                                }
                                                
                                                allFoodItems.AddRange(foodItems);
                                                genericProcessed++;
                                            }
                                            else
                                            {
                                                _logger.LogWarning("Failed to get nutrition data for selected generic food: {Query}", 
                                                    fallbackSearchQuery);
                                                missingItems.Add(genericItem.Name);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error fetching nutrition data for generic food: {Name}", genericItem.Name);
                                        missingItems.Add(genericItem.Name);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("No confident generic match found by AI for: {Name}", genericItem.Name);
                                    missingItems.Add(genericItem.Name);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("No generic options found for: {Name}", genericItem.Name);
                                missingItems.Add(genericItem.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing generic item: {Name}", genericItem.Name);
                            // Continue with other generic items
                        }
                    }
                    
                    if (genericProcessed == 0)
                    {
                        _logger.LogWarning("None of the {Count} generic items were successfully processed", genericItems.Count);
                    }
                    else
                    {
                        _logger.LogInformation("Successfully processed {ProcessedCount} out of {TotalCount} generic items", 
                            genericProcessed, genericItems.Count);
                    }
                }
                
                // 5. Check if we found any nutrition data
                if (allFoodItems.Any())
                {
                    _logger.LogInformation("Converting {Count} food items to FoodNutrition objects", allFoodItems.Count);
                    
                    response.Foods = ConvertFoodItemsToFoodNutrition(allFoodItems);
                    response.IsSuccess = true;
                    
                    // 5. Save the entries in the database
                    try
                    {
                        // 🟢 Group the food items using AI
                        var groupedItems = await _openAiService.GroupFoodItemsAsync(foodDescription, allFoodItems);
                        
                        // If grouping failed, fallback to individual groups
                        if (groupedItems == null || !groupedItems.Any())
                        {
                            _logger.LogWarning("AI grouping failed or returned empty list, falling back to individual groups");
                            groupedItems = allFoodItems.Select(x => new FoodGroup { GroupName = x.Name, Items = new List<FoodItem> { x } }).ToList();
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

        private List<FoodItem> MapNutritionixResponseToFoodItem(NutritionixResponse nutritionixResponse)
        {
            var mappedFoods = new List<FoodItem>();

            foreach (var food in nutritionixResponse.Foods)
            {
                var foodItem = new FoodItem
                {
                    Name = food.FoodName,
                    BrandName = food.BrandName,
                    Quantity = food.ServingQty,
                    Unit = food.ServingUnit ?? string.Empty,
                    Calories = (int)(food.Calories ?? 0),
                    Protein = food.Protein ?? 0,
                    Carbohydrates = food.TotalCarbohydrate ?? 0,
                    Fat = food.TotalFat ?? 0,
                    Fiber = food.DietaryFiber ?? 0,
                    Sugar = food.Sugars ?? 0,
                    SaturatedFat = food.SaturatedFat ?? 0,
                    UnsaturatedFat = 0, // Placeholder for now, Nutritionix doesn't provide this separately
                    TransFat = 0, // Nutritionix doesn't provide trans fat directly
                    Micronutrients = new Dictionary<string, double>()
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
                            // Store just the amount in the micronutrients dictionary
                            foodItem.Micronutrients[nutrientName] = nutrient.Value;
                        }
                    }
                }
                mappedFoods.Add(foodItem);
            }
            return mappedFoods;
        }

        private List<FoodNutrition> ConvertFoodItemsToFoodNutrition(List<FoodItem> foodItems)
        {
            var foodNutritions = new List<FoodNutrition>();
            
            foreach (var item in foodItems)
            {
                var foodNutrition = new FoodNutrition
                {
                    Name = item.Name,
                    BrandName = item.BrandName,
                    Quantity = item.Quantity.ToString(),
                    Unit = item.Unit,
                    Calories = item.Calories,
                    Macronutrients = new Macronutrients
                    {
                        Protein = new NutrientInfo { Amount = item.Protein, Unit = "g" },
                        Carbohydrates = new NutrientInfo { Amount = item.Carbohydrates, Unit = "g" },
                        Fat = new NutrientInfo { Amount = item.Fat, Unit = "g" },
                        Fiber = new NutrientInfo { Amount = item.Fiber, Unit = "g" },
                        Sugar = new NutrientInfo { Amount = item.Sugar, Unit = "g" },
                        SaturatedFat = new NutrientInfo { Amount = item.SaturatedFat, Unit = "g" },
                        UnsaturatedFat = new NutrientInfo { Amount = item.UnsaturatedFat, Unit = "g" },
                        TransFat = new NutrientInfo { Amount = item.TransFat, Unit = "g" }
                    },
                    Micronutrients = new Dictionary<string, Micronutrient>()
                };
                
                // Convert micronutrients
                foreach (var nutrient in item.Micronutrients)
                {
                    string unit = nutrient.Key switch
                    {
                        "Vitamin D" => "IU",
                        "Folate" => "mcg",
                        "Vitamin B12" => "mcg",
                        "Vitamin K" => "mcg",
                        "Vitamin A" => "mcg",
                        _ => "mg"
                    };
                    
                    foodNutrition.Micronutrients[nutrient.Key] = new Micronutrient 
                    { 
                        Amount = nutrient.Value, 
                        Unit = unit 
                    };
                }
                
                foodNutritions.Add(foodNutrition);
            }
            
            return foodNutritions;
        }

        private string BuildSearchQuery(ParsedFoodItem item)
        {
            var queryComponents = new List<string>();
            
            // Add quantity and unit if available
            if (item.Quantity > 0 && !string.IsNullOrWhiteSpace(item.Unit))
            {
                queryComponents.Add($"{item.Quantity} {item.Unit}");
            }
            
            // Add brand if available
            if (!string.IsNullOrWhiteSpace(item.Brand))
            {
                queryComponents.Add(item.Brand);
            }
            
            // Always add the name
            queryComponents.Add(item.Name);
            
            // Join everything with spaces
            return string.Join(" ", queryComponents);
        }

    
    }
}

