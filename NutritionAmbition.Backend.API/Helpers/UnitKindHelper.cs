using System;
using System.Collections.Generic;
using System.Linq;
using NutritionAmbition.Backend.API.Constants;

namespace NutritionAmbition.Backend.API.Helpers
{
    /// <summary>
    /// Helper class for classifying measurement units into categories (Weight, Volume, Count)
    /// </summary>
    public static class UnitKindHelper
    {
        // Weight-based units
        private static readonly HashSet<string> WeightUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "g", "gram", "grams", "oz", "mg", "lb", "kilogram", "kg"
        };

        // Volume-based units
        private static readonly HashSet<string> VolumeUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ml", "l", "cup", "cups", "tbsp", "tablespoon", "tablespoons",
            "tsp", "teaspoon", "teaspoons", "fl oz", "fluid ounce", "floz",
            "liter", "liters"
        };

        // Count-based units
        private static readonly HashSet<string> CountUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "slice", "slices", "piece", "pieces", "medium", "large", "small",
            "serving", "servings", "item", "items", "unit", "units", "each"
        };

        
        
        /// <summary>
        /// Infers the UnitKind from a unit string, defaulting to Count if the unit cannot be classified.
        /// </summary>
        /// <param name="unit">The unit string to classify</param>
        /// <returns>The UnitKind category for the given unit, or Count if it cannot be determined</returns>
        public static UnitKind InferUnitKindOrDefault(string? unit)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(unit))
                return UnitKind.Count;                          // default → Count

            string baseUnit = unit.Split('(')[0]               // strip "(…)"
                                .Trim()
                                .ToLowerInvariant();

            if (WeightUnits.Any(wu  => baseUnit == wu)) return UnitKind.Weight;
            if (VolumeUnits.Any(vu  => baseUnit == vu)) return UnitKind.Volume;
            if (CountUnits .Any(cu  => baseUnit == cu)) return UnitKind.Count;

            return UnitKind.Count;
            }
            catch
            {
                return UnitKind.Count;
            }
        }
    }
} 