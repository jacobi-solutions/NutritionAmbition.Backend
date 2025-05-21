using System;
using System.Collections.Generic;
using System.Globalization;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.DataContracts;
using Microsoft.Extensions.Logging;

namespace NutritionAmbition.Backend.API.Helpers
{
    /// <summary>
    /// Helpers for turning "16 oz coffee" into a scaling factor relative to the
    /// Nutritionix reference serving (usually 1 × ServingUnit).
    /// </summary>
    public static class UnitScalingHelpers
    {
        /* ---------- 1. quick-and-dirty unit → grams lookup ---------- */

        // Strict mass units (already grams)
        private static readonly Dictionary<string,double> MassUnits = new(StringComparer.OrdinalIgnoreCase)
        {
            { "g", 1 }, { "gram", 1 }, { "grams", 1 },
            { "kg", 1000 },
            { "oz", 28.3495 },
            { "lb", 453.592 }
        };

        // Volumetric units → millilitres  (≈ grams for water-like liquids)
        private static readonly Dictionary<string,double> VolumeUnits = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ml", 1 }, { "milliliter", 1 }, { "milliliters", 1 },
            { "l", 1000 }, { "liter", 1000 }, { "liters", 1000 },
            { "fl oz", 29.5735 }, { "floz", 29.5735 },
            { "tsp", 4.92892 }, { "teaspoon", 4.92892 }, { "teaspoons", 4.92892 },
            { "tbsp", 14.7868 }, { "tablespoon", 14.7868 }, { "tablespoons", 14.7868 },
            { "cup", 236.588 }
        };

        /// <summary>
        /// Try to convert a OriginalScaledQuantity+unit (e.g. 16, "oz") into **grams**.
        /// Returns null if the unit isn't recognised.
        /// </summary>
        public static double? TryConvertToGrams(double qty, string unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) return null;

            if (MassUnits.TryGetValue(unit.Trim().ToLowerInvariant(), out var gPerUnit))
                return qty * gPerUnit;

            if (VolumeUnits.TryGetValue(unit.Trim().ToLowerInvariant(), out var mlPerUnit))
                return qty * mlPerUnit;          // ≈ grams

            return null;
        }

        /* ---------- 2. Serving-side helpers ---------- */

        /// <summary>
        /// Nutritionix often gives ServingWeightGrams but not always.
        /// For liquid-looking units we approximate.
        /// </summary>
        public static double? TryInferMassFromVolume(string servingUnit)
        {
            if (string.IsNullOrWhiteSpace(servingUnit)) return null;

            // crude regex: pull the first number in parentheses → assume fl oz or ml
            var span = servingUnit.AsSpan();
            int parenStart = servingUnit.IndexOf('(');
            int parenEnd   = servingUnit.IndexOf(')');
            if (parenStart >= 0 && parenEnd > parenStart)
            {
                var inner = servingUnit[(parenStart+1)..parenEnd];
                // e.g. "8 fl oz"  or "240 ml"
                var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 &&
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                {
                    var u = string.Join(' ', parts[1..]);   // keep "fl oz"
                    var g = TryConvertToGrams(num, u);
                    if (g != null) return g;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns true if unit strings look equivalent ("cup" vs "cup (8 fl oz)").
        /// </summary>
        public static bool UnitsMatch(string a, string b)
        {
            if (a == null || b == null) return false;
            var na = a.Split('(')[0].Trim().ToLowerInvariant();
            var nb = b.Split('(')[0].Trim().ToLowerInvariant();
            return na == nb;
        }

        /* ---------- 3. Main scaling helper ---------- */

        /// <summary>
        /// Compute a multiplier: how many Nutritionix servings are represented
        /// by the user's <paramref name="userQty"/> <paramref name="userUnit"/>.
        /// Returns null if we can't figure it out.
        /// </summary>
        /// <param name="userQty">OriginalScaledQuantity specified by the user</param>
        /// <param name="userUnit">Unit specified by the user</param>
        /// <param name="servingQty">API serving OriginalScaledQuantity</param>
        /// <param name="servingUnit">API serving unit</param>
        /// <param name="servingWeightG">API serving weight in grams (null if unknown)</param>
        /// <param name="apiServingKind">The kind of unit used for the API serving (Weight, Volume, or Count)</param>
        /// <returns>A multiplier representing how many API servings are in the user's OriginalScaledQuantity, or null if it can't be determined</returns>
        public static double? ScaleFromUserInput(
            double userQty, string userUnit,
            double servingQty, string servingUnit,
            double? servingWeightG,
            UnitKind apiServingKind = UnitKind.Weight
        )
        {
            string normalizedUserUnit = userUnit?.Trim().ToLowerInvariant();

            if (normalizedUserUnit == "oz" && apiServingKind == UnitKind.Volume)
                normalizedUserUnit = "fl oz";

            // Case 1: direct unit match (ignores any parentheses in servingUnit)
            if (UnitsMatch(normalizedUserUnit, servingUnit))
            {
                return userQty / servingQty;
            }

            // Case 2: try to extract inner unit (e.g. "cup (8 fl oz)")
            var parenStart = servingUnit.IndexOf('(');
            var parenEnd = servingUnit.IndexOf(')');
            if (parenStart >= 0 && parenEnd > parenStart)
            {
                var inner = servingUnit.Substring(parenStart + 1, parenEnd - parenStart - 1); // e.g. "8 fl oz"
                var parts = inner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 &&
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var innerQty))
                {
                    var innerUnit = string.Join(' ', parts[1..]).Trim().ToLowerInvariant();

                    if (normalizedUserUnit == innerUnit)
                    {
                        var totalServingAmount = servingQty * innerQty; // e.g. 1 cup (8 fl oz) → 8
                        return userQty / totalServingAmount;
                    }
                }
            }

            // Case 3: fallback to grams
            var userG = TryConvertToGrams(userQty, normalizedUserUnit);
            var servingG = servingWeightG ?? TryConvertToGrams(servingQty, servingUnit);

            if (userG != null && servingG != null && servingG.Value > 0)
                return userG.Value / servingG.Value;

            return null;
        }



        /* ---------- 4. Apply multiplier to nutrient fields ---------- */

        /// <summary>
        /// Scales nutrition values in a FoodItem by the given multiplier
        /// </summary>
        public static void ScaleNutrition(FoodItem item, double factor, ILogger logger = null)
        {
            // Diagnostic logging for protein scaling
            if (logger != null)
            {
                logger.LogInformation("[PROTEIN_SCALE_DEBUG] BEFORE: {ItemName}, factor={Factor}, Protein={ProteinBefore}", 
                    item.Name, factor, item.Protein);
                
                // Add warning log if item.OriginalScaledQuantity is not 1 to catch possible double-scaling issues
                if (item.Quantity != 1)
                {
                    logger.LogWarning("[DOUBLE_SCALE_CHECK] Item {ItemName} has OriginalScaledQuantity={OriginalScaledQuantity} before scaling, suggesting it may already be scaled",
                        item.Name, item.Quantity);
                }
            }
            
            item.Calories       *= factor;
            item.Protein        *= factor;
            item.Carbohydrates  *= factor;
            item.Fat            *= factor;
            item.Fiber          *= factor;
            item.Sugar          *= factor;
            item.SaturatedFat   *= factor;
            item.UnsaturatedFat *= factor;
            item.TransFat       *= factor;

            // micronutrients
            var keys = new List<string>(item.Micronutrients.Keys);
            foreach (var k in keys)
                item.Micronutrients[k] = item.Micronutrients[k] * factor;
                
            // Scale OriginalScaledQuantity by the factor, then normalize to 1
            double scaledQuantity = item.Quantity * factor;
            
            // Set OriginalScaledQuantity to 1 to indicate this item is fully scaled
            // Store the original scaled OriginalScaledQuantity in a new property if needed for display
            item.Quantity = scaledQuantity;
            
            // More diagnostic logging after scaling
            if (logger != null)
            {
                logger.LogInformation("[PROTEIN_SCALE_DEBUG] AFTER: {ItemName}, Protein={ProteinAfter}, OriginalScaledQuantity={OriginalScaledQuantity}, OriginalScaledQuantity={OriginalScaledQuantity}, Unit={Unit}", 
                    item.Name, item.Protein, item.Quantity, item.Quantity, item.Unit);
            }
        }
        
        /// <summary>
        /// This method constructs a FoodItem and immediately scales its nutrition values based on the provided user OriginalScaledQuantity and unit.
        /// </summary>
        public static FoodItem CreateFoodItemWithScaledNutrition(
            NutritionixFood food, 
            double userQuantity, 
            string userUnit,
            UnitKind apiServingKind,
            ILogger logger = null)
        {
            // Create a FoodItem with base values from the API
            var foodItem = new FoodItem
            {
                Name = food.FoodName ?? string.Empty,
                BrandName = food.BrandName,
                Quantity = userQuantity, // Store the user's requested OriginalScaledQuantity
                Unit = food.ServingUnit ?? string.Empty,
                ApiServingKind = apiServingKind,
                // Initialize with API values (these will be scaled)
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
                        // Store the amount in the micronutrients dictionary
                        foodItem.Micronutrients[nutrientName] = nutrient.Value;
                    }
                }
            }
            
            // Calculate scaling factor based on user input
            var servingQty = food.ServingQty;
            var servingUnit = food.ServingUnit ?? string.Empty;
            var servingWeightG = food.ServingWeightGrams;
            
            // Diagnostic logging for scaling inputs
            if (logger != null)
            {
                logger.LogInformation("[PROTEIN_SCALE_DEBUG] CreateFoodItemWithScaledNutrition: {ItemName}, userQuantity={UserQuantity}, userUnit={UserUnit}, " + 
                                    "apiServingQty={ApiServingQty}, apiServingUnit={ApiServingUnit}, apiServingWeightG={ApiServingWeightG}", 
                                    foodItem.Name, userQuantity, userUnit, servingQty, servingUnit, servingWeightG);
            }
            
            var scalingFactor = ScaleFromUserInput(
                userQuantity, 
                userUnit,
                servingQty, 
                servingUnit, 
                servingWeightG,
                apiServingKind);
                
            if (scalingFactor.HasValue)
            {
                if (logger != null)
                {
                    logger.LogInformation("[PROTEIN_SCALE_DEBUG] Calculated scalingFactor={ScalingFactor}", scalingFactor.Value);
                }
                
                // Apply scaling to nutrition values
                ScaleNutrition(foodItem, scalingFactor.Value, logger);
                
                // Update unit to user's unit
                foodItem.Unit = userUnit;
            }
            else if (logger != null)
            {
                logger.LogWarning("[PROTEIN_SCALE_DEBUG] Failed to calculate scaling factor for {ItemName}", foodItem.Name);
                
                // If no scaling could be applied, still normalize OriginalScaledQuantity to 1
                foodItem.Quantity = foodItem.Quantity;
                foodItem.Quantity = 1;
                
                logger.LogInformation("[DOUBLE_SCALE_CHECK] Setting OriginalScaledQuantity=1 for {ItemName} even though no scaling factor was found",
                    foodItem.Name);
            }
            
            return foodItem;
        }
    }
}
