using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.Controllers
{
    public class MergeAnonymousAccountRequest : Request
    {
        public string AnonymousAccountId { get; set; }
    }
} 