using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetDailyGoalRequest
    {
        public DateTime? Date { get; set; } = DateTime.UtcNow.Date;
    }
} 