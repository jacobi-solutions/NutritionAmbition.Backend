namespace NutritionAmbition.Backend.API.DataContracts
{
    // Request for deleting a food entry
    public class DeleteFoodEntryRequest : Request
    {
        public List<string> FoodItemIds { get; set; } = new();
    }
}

