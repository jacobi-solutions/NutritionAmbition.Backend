namespace NutritionAmbition.Backend.API.DataContracts
{
    public class AccountRequest: Request
    {
      public string Username { get; set; }
      public string Email { get; set; }
    }

}
