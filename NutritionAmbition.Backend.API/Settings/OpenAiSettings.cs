namespace NutritionAmbition.Backend.API.Settings
{
    public class OpenAiSettings
    {
        public string ApiKey { get; set; }
        public string ApiBaseUrl { get; set; }
        public string Model { get; set;}
        public string AssistantId { get; set; }

        public string ChatCompletionsEndpoint { get; set; } 
        public string ThreadsEndpoint { get; set; }
        public string ThreadMessagesEndpoint { get; set; }
        public string ThreadRunsEndpoint { get; set; }
        public string SubmitToolOutputsEndpoint { get; set; }
    }
}
