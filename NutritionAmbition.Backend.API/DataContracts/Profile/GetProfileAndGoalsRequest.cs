
namespace NutritionAmbition.Backend.API.DataContracts.Profile
{
    public class GetProfileAndGoalsRequest : Request
    {
        public string AccountId { get; set; } = string.Empty;
    }
} 