namespace NutritionAmbition.Backend.API.Constants
{
    /// <summary>
    /// Represents the kind of measurement unit being used
    /// </summary>
    public enum UnitKind
    {
        /// <summary>
        /// Weight-based units (e.g., grams, ounces, pounds)
        /// </summary>
        Weight,
        
        /// <summary>
        /// Volume-based units (e.g., ml, cups, tablespoons)
        /// </summary>
        Volume,
        
        /// <summary>
        /// Count-based units (e.g., pieces, slices, servings)
        /// </summary>
        Count
    }
} 