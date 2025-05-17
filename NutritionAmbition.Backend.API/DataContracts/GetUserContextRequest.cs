using System;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetUserContextRequest
    {
        public int? TimezoneOffsetMinutes { get; set; }
    }
} 