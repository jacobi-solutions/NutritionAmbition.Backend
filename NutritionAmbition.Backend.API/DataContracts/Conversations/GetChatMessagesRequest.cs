using System;
using System.ComponentModel.DataAnnotations;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetChatMessagesRequest : Request
    {
        [Required]
        public DateTime LoggedDateUtc { get; set; }
    }
} 