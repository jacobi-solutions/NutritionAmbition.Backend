namespace NutritionAmbition.Backend.API.Settings
{
    public class MongoDBSettings
    {
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }
        public string AccountsCollectionName { get; set; }
        public string ChatMessagesCollectionName { get; set; }
        public string FoodEntriesCollectionName { get; set; }
        public string OpenAiThreadsCollectionName { get; set; }
    }
}
