using System;
using System.Collections.Generic;
using System.Globalization;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.Helpers
{
    /// <summary>
    /// Helpers for turning “16 oz coffee” into a scaling factor relative to the
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
        /// Returns null if the unit isn’t recognised.
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
                    var u = string.Join(' ', parts[1..]);   // keep “fl oz”
                    var g = TryConvertToGrams(num, u);
                    if (g != null) return g;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns true if unit strings look equivalent (“cup” vs “cup (8 fl oz)”).
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
        /// by the user’s <paramref name="userQty"/> <paramref name="userUnit"/>.
        /// Returns null if we can’t figure it out.
        /// </summary>
        public static double? ScaleFromUserInput(
            double userQty, string userUnit,
            double servingQty, string servingUnit,
            double? servingWeightG // null = unknown
        )
        {
            // 3a. easiest path – units are identical
            if (UnitsMatch(userUnit, servingUnit))
            {
                return userQty / servingQty;   // 2 slices out of 1 slice serving = ×2
            }

            // 3b. convert both sides to grams
            var userG     = TryConvertToGrams(userQty, userUnit);
            var servingG  = servingWeightG ?? TryConvertToGrams(servingQty, servingUnit);

            if (userG != null && servingG != null && servingG.Value > 0)
                return userG.Value / servingG.Value;

            // 3c. cannot compute
            return null;
        }

        /* ---------- 4. Apply multiplier to nutrient fields ---------- */

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
        }
    }
}
