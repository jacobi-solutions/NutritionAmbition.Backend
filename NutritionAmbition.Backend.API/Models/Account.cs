using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API.Models
{
  public class Account : Model
  {
    public string Name { get; set; }
    public string Email { get; set; }
    
    [JsonIgnore]
    public string GoogleAuthUserId { get; set; }

    [JsonIgnore]
    public bool IsAnonymousUser { get; set; }
    
    public UserProfile UserProfile { get; set; }
  }
  
  public class UserProfile
  {
    public int? Age { get; set; }
    public string Sex { get; set; }
    public int? HeightFeet { get; set; }
    public int? HeightInches { get; set; }
    public double? WeightLbs { get; set; }
    public string ActivityLevel { get; set; }
  }
}
