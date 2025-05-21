using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Repositories;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IDetailedSummaryService
    {
        Task<GetDetailedSummaryResponse> GetDetailedSummaryAsync(string accountId, DateTime loggedDateUtc);
    }

    public class DetailedSummaryService : IDetailedSummaryService
    {
        private readonly FoodEntryRepository _foodEntryRepository;
        private readonly INutritionCalculationService _nutritionCalculationService;
        private readonly ILogger<DetailedSummaryService> _logger;

        private static readonly Dictionary<string, string> DefaultUnits = new Dictionary<string, string>
        {
            { "Protein", "g" },
            { "Carbohydrates", "g" },
            { "Fat", "g" },
            { "Fiber", "g" },
            { "Sugar", "g" },
            { "SaturatedFat", "g" },
            { "UnsaturatedFat", "g" },
            { "TransFat", "g" },
            { "Sodium", "mg" },
            { "Potassium", "mg" },
            { "Calcium", "mg" },
            { "Iron", "mg" },
            { "Zinc", "mg" },
            { "VitaminA", "IU" },
            { "VitaminC", "mg" },
            { "VitaminD", "IU" },
            { "VitaminE", "mg" },
            { "Cholesterol", "mg" }
        };

        public DetailedSummaryService(
            FoodEntryRepository foodEntryRepository,
            INutritionCalculationService nutritionCalculationService,
            ILogger<DetailedSummaryService> logger)
        {
            _foodEntryRepository = foodEntryRepository;
            _nutritionCalculationService = nutritionCalculationService;
            _logger = logger;
        }

        public async Task<GetDetailedSummaryResponse> GetDetailedSummaryAsync(string accountId, DateTime loggedDateUtc)
        {
            var response = new GetDetailedSummaryResponse { AccountId = accountId };

            try
            {
                _logger.LogInformation("Getting detailed summary for account {AccountId} on date {LoggedDate}", 
                    accountId, loggedDateUtc.ToString("yyyy-MM-dd"));

                // Fetch all food entries for the given date
                var entries = await _foodEntryRepository.GetFoodEntriesByAccountAndDateAsync(accountId, loggedDateUtc);
                
                if (entries == null || !entries.Any())
                {
                    response.AddError("No food entries found for the specified date.");
                    return response;
                }

                // Flatten the entries to get all food items
                var foodItems = _nutritionCalculationService.FlattenEntries(entries).ToList();
                
                if (!foodItems.Any())
                {
                    response.AddError("No food items found in entries for the specified date.");
                    return response;
                }

                // Diagnostic logging for protein values in food items
                foreach (var item in foodItems)
                {
                    _logger.LogInformation("[PROTEIN_SCALE_DEBUG] GetDetailedSummaryAsync: Raw food item {ItemName}, Protein={Protein}, OriginalScaledQuantity={OriginalQuantity}, OriginalScaledQuantity={OriginalScaledQuantity}, Unit={Unit}",
                        item.Name, item.Protein, item.Quantity, item.Quantity, item.Unit);
                }

                // Generate nutrient breakdowns
                response.Nutrients = GenerateNutrientBreakdowns(foodItems);
                
                // Generate food breakdowns
                response.Foods = GenerateFoodBreakdowns(foodItems);

                // Calculate totals for summary
                var nutritionTotals = _nutritionCalculationService.CalculateTotals(foodItems);
                
                // Log the calculated protein total
                _logger.LogInformation("[PROTEIN_SCALE_DEBUG] GetDetailedSummaryAsync: Calculated TotalProtein={TotalProtein}",
                    nutritionTotals.TotalProtein);
                
                // Compute total fiber, sugar, and saturated fat - values are already scaled
                double totalFiber = foodItems.Sum(i => i.Fiber); // Previously: i.Fiber * i.OriginalScaledQuantity
                double totalSugar = foodItems.Sum(i => i.Sugar); // Previously: i.Sugar * i.OriginalScaledQuantity
                double totalSaturatedFat = nutritionTotals.TotalSaturatedFat; // Assuming this is already fixed
                
                _logger.LogInformation("[DOUBLE_SCALE_CHECK] Computed summary totals: TotalFiber={TotalFiber}, TotalSugar={TotalSugar}, TotalSaturatedFat={TotalSaturatedFat}",
                    totalFiber, totalSugar, totalSaturatedFat);
                
                // Set the summary totals
                var totals = new SummaryTotals {
                    TotalCalories = (int)nutritionTotals.TotalCalories,
                    Macronutrients = new MacronutrientsSummary {
                        Protein = nutritionTotals.TotalProtein,
                        Carbohydrates = nutritionTotals.TotalCarbohydrates,
                        Fat = nutritionTotals.TotalFat,
                        Fiber = totalFiber,
                        Sugar = totalSugar,
                        SaturatedFat = totalSaturatedFat
                    }
                };
                response.SummaryTotals = totals;
                
                // Log the summary totals for protein
                _logger.LogInformation("[PROTEIN_SCALE_DEBUG] GetDetailedSummaryAsync: Response SummaryTotals Protein={ProteinTotal}",
                    response.SummaryTotals.Macronutrients.Protein);
                
                // Log protein values in the nutrients breakdown
                var proteinNutrient = response.Nutrients.FirstOrDefault(n => n.Name == "Protein");
                if (proteinNutrient != null)
                {
                    _logger.LogInformation("[PROTEIN_SCALE_DEBUG] GetDetailedSummaryAsync: Response Nutrient Protein TotalAmount={ProteinTotal}",
                        proteinNutrient.TotalAmount);
                        
                    foreach (var foodContribution in proteinNutrient.Foods)
                    {
                        _logger.LogInformation("[PROTEIN_SCALE_DEBUG] GetDetailedSummaryAsync: Protein contribution from {FoodName}: {Amount} {Unit}",
                            foodContribution.Name, foodContribution.Amount, foodContribution.Unit);
                    }
                }

                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed summary for account {AccountId}", accountId);
                response.AddError($"An error occurred while generating detailed summary: {ex.Message}");
            }

            return response;
        }

        private List<NutrientBreakdown> GenerateNutrientBreakdowns(List<FoodItem> foodItems)
        {
            var nutrientBreakdowns = new List<NutrientBreakdown>();

            // Process macronutrients first (only protein, carbohydrates, and fat)
            AddMacroNutrientBreakdown(nutrientBreakdowns, foodItems, "Protein", item => item.Protein);
            AddMacroNutrientBreakdown(nutrientBreakdowns, foodItems, "Carbohydrates", item => item.Carbohydrates);
            AddMacroNutrientBreakdown(nutrientBreakdowns, foodItems, "Fat", item => item.Fat);
            
            // Process micronutrients
            var allMicronutrients = new Dictionary<string, double>();
            
            // Add other nutrients that were previously treated as macronutrients
            // but are now classified as micronutrients
            AddMicroNutrientBreakdown(nutrientBreakdowns, foodItems, "Fiber", item => item.Fiber);
            AddMicroNutrientBreakdown(nutrientBreakdowns, foodItems, "Sugar", item => item.Sugar);
            AddMicroNutrientBreakdown(nutrientBreakdowns, foodItems, "Saturated Fat", item => item.SaturatedFat);
            
            // Collect all micronutrients from all food items
            foreach (var item in foodItems)
            {
                foreach (var nutrient in item.Micronutrients)
                {
                    if (!allMicronutrients.ContainsKey(nutrient.Key))
                    {
                        allMicronutrients[nutrient.Key] = 0;
                    }
                }
            }
            
            // Create breakdowns for each micronutrient
            foreach (var nutrientName in allMicronutrients.Keys)
            {
                var breakdown = new NutrientBreakdown
                {
                    Name = nutrientName,
                    Unit = GetNutrientUnit(nutrientName),
                    TotalAmount = 0
                };
                
                foreach (var item in foodItems)
                {
                    if (item.Micronutrients.TryGetValue(nutrientName, out double amount) && amount > 0)
                    {
                        // NOTE: Values are already scaled - do not multiply by item.OriginalScaledQuantity
                        double totalAmount = amount; // Previously was: amount * item.OriginalScaledQuantity
                        breakdown.TotalAmount += totalAmount;
                        
                        // Log the food item's original unit
                        _logger.LogInformation("[DOUBLE_SCALE_CHECK] Food: {Name}, OriginalScaledQuantity: {OriginalQuantity}, OriginalScaledQuantity: {OriginalScaledQuantity}, Unit: {Unit}, Contributing nutrient {Nutrient}: {Amount}", 
                            item.Name, item.Quantity, item.Quantity, item.Unit, nutrientName, amount);
                        
                        // Ensure the food unit exists - use a default if not provided
                        var foodUnit = !string.IsNullOrEmpty(item.Unit) ? item.Unit : "serving";
                        
                        breakdown.Foods.Add(new FoodContribution
                        {
                            Name = item.Name,
                            BrandName = item.BrandName,
                            Amount = totalAmount,
                            Unit = GetNutrientUnit(nutrientName), // Keep nutrient unit for consistent measurements
                            FoodUnit = foodUnit, // Preserve the original food unit for display
                            // Optionally add a property for display OriginalScaledQuantity if needed
                            DisplayQuantity = item.Quantity
                        });
                    }
                }
                
                // Only add nutrients that have contributions
                if (breakdown.Foods.Any())
                {
                    // Sort contributions by amount in descending order
                    breakdown.Foods = breakdown.Foods.OrderByDescending(f => f.Amount).ToList();
                    nutrientBreakdowns.Add(breakdown);
                }
            }
            
            return nutrientBreakdowns.OrderByDescending(n => n.TotalAmount).ToList();
        }

        private void AddMacroNutrientBreakdown(
            List<NutrientBreakdown> breakdowns, 
            List<FoodItem> foodItems, 
            string nutrientName, 
            Func<FoodItem, double> valueSelector)
        {
            var breakdown = new NutrientBreakdown
            {
                Name = nutrientName,
                Unit = GetNutrientUnit(nutrientName),
                TotalAmount = 0
            };
            
            foreach (var item in foodItems)
            {
                double nutrientValue = valueSelector(item);
                if (nutrientValue > 0)
                {
                    // NOTE: Values are already scaled - do not multiply by item.OriginalScaledQuantity
                    double totalAmount = nutrientValue; // Previously was: nutrientValue * item.OriginalScaledQuantity
                    breakdown.TotalAmount += totalAmount;
                    
                    // Log the food item's original unit
                    _logger.LogInformation("[DOUBLE_SCALE_CHECK] Food: {Name}, OriginalScaledQuantity: {OriginalQuantity}, OriginalScaledQuantity: {OriginalScaledQuantity}, Unit: {Unit}, Contributing {Nutrient}: {Amount}", 
                        item.Name, item.Quantity, item.Quantity, item.Unit, nutrientName, nutrientValue);
                    
                    // Ensure the food unit exists - use a default if not provided
                    var foodUnit = !string.IsNullOrEmpty(item.Unit) ? item.Unit : "serving";
                    
                    breakdown.Foods.Add(new FoodContribution
                    {
                        Name = item.Name,
                        BrandName = item.BrandName,
                        Amount = totalAmount,
                        Unit = GetNutrientUnit(nutrientName), // Keep nutrient unit for consistent measurements
                        FoodUnit = foodUnit, // Preserve the original food unit for display
                        // Optionally add a property for display OriginalScaledQuantity if needed
                        DisplayQuantity = item.Quantity
                    });
                }
            }
            
            // Only add nutrients that have contributions
            if (breakdown.Foods.Any())
            {
                // Sort contributions by amount in descending order
                breakdown.Foods = breakdown.Foods.OrderByDescending(f => f.Amount).ToList();
                breakdowns.Add(breakdown);
            }
        }

        private void AddMicroNutrientBreakdown(
            List<NutrientBreakdown> breakdowns, 
            List<FoodItem> foodItems, 
            string nutrientName, 
            Func<FoodItem, double> valueSelector)
        {
            var breakdown = new NutrientBreakdown
            {
                Name = nutrientName,
                Unit = GetNutrientUnit(nutrientName),
                TotalAmount = 0
            };
            
            foreach (var item in foodItems)
            {
                double nutrientValue = valueSelector(item);
                if (nutrientValue > 0)
                {
                    // NOTE: Values are already scaled - do not multiply by item.OriginalScaledQuantity
                    double totalAmount = nutrientValue; // Previously was: nutrientValue * item.OriginalScaledQuantity
                    breakdown.TotalAmount += totalAmount;
                    
                    // Log the food item's original unit
                    _logger.LogInformation("[DOUBLE_SCALE_CHECK] Food: {Name}, OriginalScaledQuantity: {OriginalQuantity}, OriginalScaledQuantity: {OriginalScaledQuantity}, Unit: {Unit}, Contributing micronutrient {Nutrient}: {Amount}", 
                        item.Name, item.Quantity, item.Quantity, item.Unit, nutrientName, nutrientValue);
                    
                    // Ensure the food unit exists - use a default if not provided
                    var foodUnit = !string.IsNullOrEmpty(item.Unit) ? item.Unit : "serving";
                    
                    breakdown.Foods.Add(new FoodContribution
                    {
                        Name = item.Name,
                        BrandName = item.BrandName,
                        Amount = totalAmount,
                        Unit = GetNutrientUnit(nutrientName), // Keep nutrient unit for consistent measurements
                        FoodUnit = foodUnit, // Preserve the original food unit for display
                        // Optionally add a property for display OriginalScaledQuantity if needed
                        DisplayQuantity = item.Quantity
                    });
                }
            }
            
            // Only add nutrients that have contributions
            if (breakdown.Foods.Any())
            {
                // Sort contributions by amount in descending order
                breakdown.Foods = breakdown.Foods.OrderByDescending(f => f.Amount).ToList();
                breakdowns.Add(breakdown);
            }
        }

        private List<FoodBreakdown> GenerateFoodBreakdowns(List<FoodItem> foodItems)
        {
            var foodBreakdowns = new List<FoodBreakdown>();
            
            // Group by food name
            var groupedFoods = foodItems
                .GroupBy(f => f.Name)
                .OrderByDescending(g => g.Sum(f => f.Quantity)); // Use OriginalScaledQuantity for sorting
            
            foreach (var group in groupedFoods)
            {
                // Log units for all items in this group
                foreach (var item in group)
                {
                    _logger.LogInformation("[DOUBLE_SCALE_CHECK] Food group item: {Name}, OriginalScaledQuantity: {OriginalQuantity}, OriginalScaledQuantity: {OriginalScaledQuantity}, Unit: {Unit}", 
                        item.Name, item.Quantity, item.Quantity, item.Unit);
                }
                
                // Always use the first item's unit for display consistency
                var firstItem = group.First();
                string unitToUse = !string.IsNullOrEmpty(firstItem.Unit) ? firstItem.Unit : "serving";
                
                // Log if there are multiple different units within the same food group
                var distinctUnits = group.Select(i => i.Unit).Distinct().ToList();
                if (distinctUnits.Count > 1)
                {
                    _logger.LogInformation("Multiple units found for food group {Name}: {Units}, using first item's unit: {Unit}", 
                        group.Key, string.Join(", ", distinctUnits), unitToUse);
                }
                
                var breakdown = new FoodBreakdown
                {
                    Name = group.Key,
                    BrandName = firstItem.BrandName,
                    // Use sum of OriginalScaledQuantity for total display amount
                    TotalAmount = group.Sum(f => f.Quantity),
                    Unit = unitToUse
                };
                
                // Log the group total
                _logger.LogInformation("[DOUBLE_SCALE_CHECK] Food group: {Name}, TotalAmount: {TotalAmount} (based on OriginalScaledQuantity), Unit: {Unit}",
                    group.Key, breakdown.TotalAmount, breakdown.Unit);
                
                // Add macronutrient contributions (only protein, carbohydrates, and fat)
                AddNutrientContribution(breakdown, group, "Calories", item => item.Calories, "kcal");
                AddNutrientContribution(breakdown, group, "Protein", item => item.Protein, "g");
                AddNutrientContribution(breakdown, group, "Carbohydrates", item => item.Carbohydrates, "g");
                AddNutrientContribution(breakdown, group, "Fat", item => item.Fat, "g");
                
                // Add former macronutrients that are now micronutrients
                AddNutrientContribution(breakdown, group, "Fiber", item => item.Fiber, "g");
                AddNutrientContribution(breakdown, group, "Sugar", item => item.Sugar, "g");
                AddNutrientContribution(breakdown, group, "Saturated Fat", item => item.SaturatedFat, "g");
                
                // Add micronutrient contributions
                var allMicronutrients = new HashSet<string>();
                foreach (var food in group)
                {
                    foreach (var nutrient in food.Micronutrients.Keys)
                    {
                        allMicronutrients.Add(nutrient);
                    }
                }
                
                foreach (var nutrient in allMicronutrients)
                {
                    // Values are already scaled - do not multiply by item.OriginalScaledQuantity
                    double totalAmount = group.Sum(f => 
                        f.Micronutrients.TryGetValue(nutrient, out double value) ? value : 0);
                    
                    if (totalAmount > 0)
                    {
                        breakdown.Nutrients.Add(new NutrientContribution
                        {
                            Name = nutrient,
                            BrandName = group.FirstOrDefault()?.BrandName,
                            Amount = totalAmount,
                            Unit = GetNutrientUnit(nutrient)
                        });
                    }
                }
                
                // Sort nutrient contributions by amount
                breakdown.Nutrients = breakdown.Nutrients.OrderByDescending(n => n.Amount).ToList();
                foodBreakdowns.Add(breakdown);
            }
            
            return foodBreakdowns;
        }

        private void AddNutrientContribution(
            FoodBreakdown breakdown, 
            IGrouping<string, FoodItem> group, 
            string nutrientName, 
            Func<FoodItem, double> valueSelector,
            string unit)
        {
            // Values are already scaled - do not multiply by OriginalScaledQuantity
            double totalAmount = group.Sum(f => valueSelector(f));
            
            _logger.LogInformation("[DOUBLE_SCALE_CHECK] Adding nutrient {Nutrient} to food group {FoodName}, TotalAmount={Amount}",
                nutrientName, group.Key, totalAmount);
            
            if (totalAmount > 0)
            {
                // Use the first item's unit as the original unit
                var firstItem = group.First();
                var originalUnit = !string.IsNullOrEmpty(firstItem.Unit) ? firstItem.Unit : "serving";
                
                breakdown.Nutrients.Add(new NutrientContribution
                {
                    Name = nutrientName,
                    BrandName = firstItem.BrandName,
                    Amount = totalAmount,
                    Unit = unit,
                    OriginalUnit = originalUnit
                });
            }
        }

        private string GetNutrientUnit(string nutrientName)
        {
            // Standardize nutrient name by removing spaces and lowercasing
            string standardizedName = nutrientName.Replace(" ", "");
            
            // Try to find the unit in the default units dictionary
            foreach (var pair in DefaultUnits)
            {
                if (string.Equals(standardizedName, pair.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }
            
            // Default unit for unknown nutrients
            return "g";
        }
    }
} 