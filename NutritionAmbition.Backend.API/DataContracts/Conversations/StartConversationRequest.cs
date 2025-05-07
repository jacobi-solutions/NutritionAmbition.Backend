using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.Controllers
{
    public class StartConversationRequest : Request
    {
        public string MessageContent { get; set; }
    }
} 