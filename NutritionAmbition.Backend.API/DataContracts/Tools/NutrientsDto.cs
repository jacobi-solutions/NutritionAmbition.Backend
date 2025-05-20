namespace NutritionAmbition.Backend.API.DataContracts.Tools
{
    /// <summary>
    /// Simple DTO representing macronutrient amounts for a food item.
    /// </summary>
    public class NutrientsDto
    {
        /// <summary>
        /// Protein amount in grams.
        /// </summary>
        public float Protein { get; set; }

        /// <summary>
        /// Fat amount in grams.
        /// </summary>
        public float Fat { get; set; }

        /// <summary>
        /// Carbohydrate amount in grams.
        /// </summary>
        public float Carbs { get; set; }
    }
}
