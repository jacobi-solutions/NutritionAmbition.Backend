namespace NutritionAmbition.Backend.API.DataContracts
{
    public class MergeAnonymousAccountResponse : Response
    {
        public long ChatMessagesMigrated { get; set; }
        public long FoodEntriesMigrated { get; set; }
    }
} 