using Serilog;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using NutritionAmbition.Backend.API.Services;
using NutritionAmbition.Backend.API.Settings;
using Microsoft.Extensions.Options;
using NutritionAmbition.Backend.API.Repositories;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using NutritionAmbition.Backend.API;
using NutritionAmbition.Backend.API.Middleware;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

// 🟢 Configure Serilog from appsettings.json
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// If we are in development, force Kestrel to use only HTTP on port 5165
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://localhost:5165");
}

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options => 
    {
        // Use the custom converter for potentially problematic string deserialization
        options.JsonSerializerOptions.Converters.Add(new SafeStringConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 🟢 Bind Settings
var mongoDbSettings = builder.Configuration.GetSection(AppConstants.MongoDbSettings).Get<MongoDBSettings>();
builder.Services.AddSingleton(mongoDbSettings);

var firebaseSettings = builder.Configuration.GetSection(AppConstants.FirebaseSettings).Get<FirebaseSettings>();
builder.Services.AddSingleton(firebaseSettings);

var openAiSettings = builder.Configuration.GetSection(AppConstants.OpenAiSettings).Get<OpenAiSettings>();
builder.Services.AddSingleton(openAiSettings);

var nutritionixSettings = builder.Configuration.GetSection(AppConstants.NutritionixSettings).Get<NutritionixSettings>();
builder.Services.AddSingleton(nutritionixSettings);

// 🟢 Database
var pack = new ConventionPack
{
    new CamelCaseElementNameConvention(),
    new IgnoreIfNullConvention(true),
    new IgnoreExtraElementsConvention(true)
};
ConventionRegistry.Register("My Custom Conventions", pack, t => true);

// ✅ Register MongoDB Client & Database as Singleton
var mongoClient = new MongoClient(mongoDbSettings.ConnectionString);
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton<IMongoDatabase>(x => mongoClient.GetDatabase(mongoDbSettings.DatabaseName));

// 🟢 Services
builder.Services.AddSingleton<IAccountsService, AccountsService>();
builder.Services.AddSingleton<IFoodEntryService, FoodEntryService>();

// Register the DailyGoal service and repository
builder.Services.AddSingleton<DailyGoalRepository>();
builder.Services.AddSingleton<IDailyGoalService, DailyGoalService>();

// Register the DefaultGoalProfileRepository
builder.Services.AddSingleton<DefaultGoalProfileRepository>();

// Register the Profile service
builder.Services.AddSingleton<IProfileService, ProfileService>();

// Register the GoalScaffoldingService
builder.Services.AddSingleton<IGoalScaffoldingService, GoalScaffoldingService>();

// Register the Conversation service and repository
builder.Services.AddSingleton<ChatMessageRepository>();
builder.Services.AddScoped<IConversationService, ConversationService>();

// Register the DailySummary service 
builder.Services.AddSingleton<IDailySummaryService, DailySummaryService>();

// Register the DetailedSummary service
builder.Services.AddSingleton<IDetailedSummaryService, DetailedSummaryService>();

// Register the NutritionCalculation service
builder.Services.AddSingleton<INutritionCalculationService, NutritionCalculationService>();

// Register the AssistantToolService
builder.Services.AddScoped<IAssistantToolService, AssistantToolService>();

// Register the AssistantToolHandler service
builder.Services.AddScoped<IAssistantToolHandlerService, AssistantToolHandlerService>();

// Register Nutritionix Service with HttpClient
builder.Services.AddHttpClient<NutritionixClient>();
builder.Services.AddSingleton<NutritionixClient>();
builder.Services.AddSingleton<INutritionixService, NutritionixService>();
builder.Services.AddScoped<INutritionService, NutritionService>();

// Register OpenAI SDK Client
builder.Services.AddSingleton(sp => 
{
    return new OpenAIClient(openAiSettings.ApiKey);
});

// Register OpenAI service
builder.Services.AddScoped<IOpenAiService, OpenAiService>();

// Register Thread Service
builder.Services.AddScoped<IThreadService, ThreadService>();

// Repos
builder.Services.AddSingleton<AccountsRepository>();
builder.Services.AddSingleton<FoodEntryRepository>();
builder.Services.AddSingleton<ChatMessageRepository>();
builder.Services.AddSingleton<OpenAiThreadRepository>();

// ✅ Initialize Firebase Admin SDK
var firebaseProjectId = firebaseSettings.ProjectId;
FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.GetApplicationDefault(),
    ProjectId = firebaseProjectId
});

// 🟢 Authentication & Authorization
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = $"https://securetoken.google.com/{firebaseProjectId}";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
        ValidateAudience = true,
        ValidAudience = firebaseProjectId,
        ValidateLifetime = true
    };
});

// ✅ CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Build the app
var app = builder.Build();

// ✅ Use Authentication & Authorization Middleware
app.UseAuthentication();
app.UseMiddleware<AnonymousAuthMiddleware>();
app.UseAuthorization();
app.UseMiddleware<AttachAccountIdToResponseMiddleware>();
// 🟢 Use Swagger in Dev Environment
if (builder.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowLocalhost");
app.MapControllers();

// ✅ Log Startup Information
Log.Information("🚀 Application is starting...");

// Run the app
try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "🔥 Application failed to start.");
}
finally
{
    Log.CloseAndFlush();
}
