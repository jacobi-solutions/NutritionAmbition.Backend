namespace NutritionAmbition.Backend.API.DataContracts
{
    public class Request
    {
        public string? AccountId { get; set; }
        public bool IsAnonymousUser { get; set; } = false;
    }
}
