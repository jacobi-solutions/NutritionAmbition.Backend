using System;
using System.ComponentModel.DataAnnotations;

namespace NutritionAmbition.Backend.API.DataContracts.FoodEntries
{
    public class GetFoodEntriesRequest : Request
    {

        public DateTime? LoggedDateUtc { get; set; }
    }
} 