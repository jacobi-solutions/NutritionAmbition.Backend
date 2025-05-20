using System;

namespace NutritionAmbition.Backend.API.DataContracts.Tools
{
    /// <summary>
    /// Response returned after logging a meal with the assistant tool.
    /// </summary>
    public class LogMealToolResponse : Response
    {
        /// <summary>
        /// Original meal description provided by the user.
        /// </summary>
        public string MealName { get; set; } = string.Empty;

        /// <summary>
        /// Total calories for the meal.
        /// </summary>
        public int Calories { get; set; }

        /// <summary>
        /// UTC timestamp when the meal was logged.
        /// </summary>
        public DateTime LoggedAtUtc { get; set; }

        /// <summary>
        /// Macronutrient details for the meal.
        /// </summary>
        public NutrientsDto Nutrients { get; set; } = new NutrientsDto();
    }
}
