namespace NutritionAmbition.Backend.API.DataContracts
{
    // Request for deleting a food entry
    public class DeleteFoodEntryRequest : Request
    {
        public string FoodEntryId { get; set; } = string.Empty;
    }
}

