namespace NutritionAmbition.Backend.API.Constants
{
  public static class ErrorTypes
  {
        public const string VALIDATION_ERROR = "VALIDATION_ERROR"; 

        public static bool IsValid(string type) {
          switch (type)
          {
              case VALIDATION_ERROR:
                return true;
              default:
                return false;
          }
        }

        
  }
}