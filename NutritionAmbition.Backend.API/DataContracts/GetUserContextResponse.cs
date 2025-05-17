using System;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetUserContextResponse : Response
    {
        public bool HasUserProfile { get; set; }
        public bool HasGoals { get; set; }
        public string LocalDate { get; set; }
        public int TimezoneOffsetMinutes { get; set; }
    }
} 