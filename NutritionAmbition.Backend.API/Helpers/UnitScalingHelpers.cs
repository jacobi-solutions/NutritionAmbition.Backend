using System;
using System.Collections.Generic;
using System.Globalization;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.DataContracts;

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
        /// Try to convert a quantity+unit (e.g. 16, "oz") into **grams**.
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
        /// <param name="userQty">Quantity specified by the user</param>
        /// <param name="userUnit">Unit specified by the user</param>
        /// <param name="servingQty">API serving quantity</param>
        /// <param name="servingUnit">API serving unit</param>
        /// <param name="servingWeightG">API serving weight in grams (null if unknown)</param>
        /// <param name="apiServingKind">The kind of unit used for the API serving (Weight, Volume, or Count)</param>
        /// <returns>A multiplier representing how many API servings are in the user's quantity, or null if it can't be determined</returns>
        public static double? ScaleFromUserInput(
            double userQty, string userUnit,
            double servingQty, string servingUnit,
            double? servingWeightG, // null = unknown
            UnitKind apiServingKind = UnitKind.Weight // default to Weight if not specified
        )
        {
            // Normalize user unit based on API serving kind
            string normalizedUserUnit = userUnit;
            
            // If user entered "oz" and API serving is a Volume, interpret as "fl oz"
            if (string.Equals(userUnit?.Trim(), "oz", StringComparison.OrdinalIgnoreCase) && 
                apiServingKind == UnitKind.Volume)
            {
                normalizedUserUnit = "fl oz";
            }

            // 3a. easiest path – units are identical
            if (UnitsMatch(normalizedUserUnit, servingUnit))
            {
                return userQty / servingQty;   // 2 slices out of 1 slice serving = ×2
            }

            // 3b. convert both sides to grams
            var userG     = TryConvertToGrams(userQty, normalizedUserUnit);
            var servingG  = servingWeightG ?? TryConvertToGrams(servingQty, servingUnit);

            if (userG != null && servingG != null && servingG.Value > 0)
                return userG.Value / servingG.Value;

            // 3c. cannot compute
            return null;
        }

        /* ---------- 4. Apply multiplier to nutrient fields ---------- */

        /// <summary>
        /// Scales nutrition values in a FoodItem by the given multiplier
        /// </summary>
        public static void ScaleNutrition(FoodItem item, double factor)
        {
            item.Calories       = (int)Math.Round(item.Calories       * factor);
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
                
            // Scale quantity by the factor
            item.Quantity *= factor;
        }
        
        /// <summary>
        /// This method constructs a FoodItem and immediately scales its nutrition values based on the provided user quantity and unit.
        /// </summary>
        public static FoodItem CreateFoodItemWithScaledNutrition(
            NutritionixFood food, 
            double userQuantity, 
            string userUnit,
            UnitKind apiServingKind)
        {
            // Create a FoodItem with base values from the API
            var foodItem = new FoodItem
            {
                Name = food.FoodName ?? string.Empty,
                BrandName = food.BrandName,
                Quantity = food.ServingQty, // Use API serving quantity as initial value
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
            
            var scalingFactor = ScaleFromUserInput(
                userQuantity, 
                userUnit,
                servingQty, 
                servingUnit, 
                servingWeightG,
                apiServingKind);
                
            if (scalingFactor.HasValue)
            {
                // Apply scaling to nutrition values
                ScaleNutrition(foodItem, scalingFactor.Value);
                
                // Update unit to user's unit
                foodItem.Unit = userUnit;
            }
            
            return foodItem;
        }
    }
}
