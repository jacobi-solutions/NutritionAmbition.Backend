using System;
using MongoDB.Bson.Serialization.Attributes;

namespace NutritionAmbition.Backend.API.Models
{
    public class OpenAiThreadRecord : Model
    {
        [BsonElement("accountId")]
        public string AccountId { get; set; }

        [BsonElement("date")]
        public DateTime Date { get; set; }

        [BsonElement("threadId")]
        public string ThreadId { get; set; }
    }
} 