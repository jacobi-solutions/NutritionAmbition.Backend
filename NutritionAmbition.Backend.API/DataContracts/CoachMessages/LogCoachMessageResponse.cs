using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class LogCoachMessageResponse : Response
    {
        public CoachMessage Message { get; set; }
    }
} 