using System;

namespace NutritionAmbition.Backend.API.Models
{
    public class AssistantRun : Model
    {
        public string AccountId { get; set; }
        public string ThreadId { get; set; }
        public string RunId { get; set; }
        public string Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
} 