namespace NutritionAmbition.Backend.API.DataContracts
{
    public class Error
    {
        public string ErrorMessage { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
    }

}
