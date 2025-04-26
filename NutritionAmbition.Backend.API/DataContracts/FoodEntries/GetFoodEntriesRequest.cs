using System;
using System.ComponentModel.DataAnnotations;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetFoodEntriesRequest : Request
    {

        public DateTime? LoggedDateUtc { get; set; }
    }
} 