using System;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetUserContextResponse : Response
    {
        public bool IsAnonymousUser { get; set; }
        public bool HasProfile { get; set; }
        public bool HasGoals { get; set; }
        public DateTime LocalDate { get; set; }
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }
} 