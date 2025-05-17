using System;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetUserContextRequest : Request
    {
        public int? TimezoneOffsetMinutes { get; set; }
    }
} 