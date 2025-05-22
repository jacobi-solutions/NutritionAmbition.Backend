using System.Collections.Generic;
using NutritionAmbition.Backend.API.Constants;

namespace NutritionAmbition.Backend.API.Models
{
    /// <summary>
    /// Represents a food item with its nutritional information.
    /// </summary>
    public class FoodItem
    {
        public string Name { get; set; } // Required
        public string? BrandName { get; set; }
        
        
        
        /// <summary>
        /// The original scaled quantity in user-specified units.
        /// This value is used for display purposes to show the actual amount the user consumed.
        /// </summary>
        public double Quantity { get; set; } = 0.0;
        public string Unit { get; set; } = string.Empty;
        public double? WeightGramsPerUnit { get; set; }
        public double Calories { get; set; } = 0.0;
        public double Protein { get; set; } = 0.0;
        public double Carbohydrates { get; set; } = 0.0;
        public double Fat { get; set; } = 0.0;
        public Dictionary<string, double> Micronutrients { get; set; } = new Dictionary<string, double>();
        
        /// <summary>
        /// The kind of unit used for the API serving (Weight, Volume, or Count)
        /// </summary>
        public UnitKind ApiServingKind { get; set; }
    }

}