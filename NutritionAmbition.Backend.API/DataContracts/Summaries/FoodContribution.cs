namespace NutritionAmbition.Backend.API.DataContracts
{
    public class FoodContribution
    {
        public string Name { get; set; }
        public string BrandName { get; set; }
        public double Amount { get; set; }
        public string Unit { get; set; }
        public string FoodUnit { get; set; }
        
        /// <summary>
        /// The original scaled quantity for display purposes.
        /// Shows the actual amount the user consumed.
        /// </summary>
        public double DisplayQuantity { get; set; }
    }
} 