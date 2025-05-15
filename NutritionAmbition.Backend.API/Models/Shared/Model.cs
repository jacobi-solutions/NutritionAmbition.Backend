using System;
using MongoDB.Bson.Serialization.Attributes;

namespace NutritionAmbition.Backend.API.Models
{
  public class Model
  {
    [BsonId(IdGenerator = typeof(GuidAsStringGenerator))]
    [BsonIgnoreIfDefault]
    [BsonIgnoreIfNull]
    public string Id { get; set; }

    public DateTime CreatedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedDateUtc { get; set; } = DateTime.UtcNow;
  }

}
