using System.Collections.Generic;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetCoachMessagesResponse : Response
    {
        public List<CoachMessage> Messages { get; set; } = new List<CoachMessage>();
    }
} 