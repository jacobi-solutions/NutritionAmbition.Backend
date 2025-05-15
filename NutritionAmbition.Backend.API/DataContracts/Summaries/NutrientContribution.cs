namespace NutritionAmbition.Backend.API.DataContracts
{
    public class NutrientContribution
    {
        public string Name { get; set; }
        public string BrandName { get; set; }
        public double Amount { get; set; }
        public string Unit { get; set; }
        public string OriginalUnit { get; set; }
    }
} 