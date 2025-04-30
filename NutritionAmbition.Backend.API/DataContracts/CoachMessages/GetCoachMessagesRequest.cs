using System;
using System.ComponentModel.DataAnnotations;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetCoachMessagesRequest : Request
    {
        [Required]
        public DateTime LoggedDateUtc { get; set; }
    }
} 