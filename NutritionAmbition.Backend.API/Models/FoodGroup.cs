using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.Models
{
    /// <summary>
    /// Represents a logical grouping of food items derived from a user's input.
    /// </summary>
    public class FoodGroup
    {
        /// <summary>
        /// The name assigned to this group by the AI (e.g., "Coffee", "Protein Shake").
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// The list of individual food items belonging to this group.
        /// </summary>
        public List<FoodItem> Items { get; set; } = new List<FoodItem>();
    }
}

