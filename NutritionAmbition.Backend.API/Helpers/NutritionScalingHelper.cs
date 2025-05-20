using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.Helpers
{
    /// <summary>
    /// Helper class for scaling nutrition values and food items
    /// </summary>
    public static class NutritionScalingHelper
    {
        /// <summary>
        /// Scales a food item to the desired quantity and unit
        /// </summary>
        /// <param name="item">The food item to scale</param>
        /// <param name="desiredQty">The desired quantity</param>
        /// <param name="desiredUnit">The desired unit</param>
        public static void ScaleItem(FoodItem item, double desiredQty, string desiredUnit) 
        { 
            if (desiredQty > 0) item.Quantity = desiredQty; 
            if (!string.IsNullOrWhiteSpace(desiredUnit)) item.Unit = desiredUnit; 
        }
    }
} 