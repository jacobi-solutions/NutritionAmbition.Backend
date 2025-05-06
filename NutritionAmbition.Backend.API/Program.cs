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

// Configure OpenAiSettings
builder.Services.Configure<OpenAiSettings>(builder.Configuration.GetSection("OpenAiSettings"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<OpenAiSettings>>().Value);

// Configure NutritionixSettings
builder.Services.Configure<NutritionixSettings>(builder.Configuration.GetSection("NutritionixSettings"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<NutritionixSettings>>().Value);

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
builder.Services.AddSingleton<AccountsService>();
builder.Services.AddSingleton<AiService>(); // Assuming this is the AI Conversation Handler
builder.Services.AddSingleton<IFoodEntryService, FoodEntryService>();
builder.Services.AddSingleton<IFoodParsingService, FoodParsingService>();

// Register the DailyGoal service and repository
builder.Services.AddSingleton<DailyGoalRepository>();
builder.Services.AddSingleton<IDailyGoalService, DailyGoalService>();

// Register the ChatMessage service and repository
builder.Services.AddSingleton<ChatMessageRepository>();
builder.Services.AddSingleton<IChatMessageService, ChatMessageService>();

// Register the DailySummary service 
builder.Services.AddSingleton<IDailySummaryService, DailySummaryService>();

// Register the NutritionCalculation service
builder.Services.AddSingleton<INutritionCalculationService, NutritionCalculationService>();

// Register Nutritionix Service with HttpClient
builder.Services.AddHttpClient<NutritionixClient>();
builder.Services.AddSingleton<NutritionixClient>();
builder.Services.AddSingleton<INutritionixService, NutritionixService>();
builder.Services.AddSingleton<INutritionService, NutritionService>();

// Register OpenAI Service with HttpClient
builder.Services.AddHttpClient<IOpenAiService, OpenAiService>();
builder.Services.AddSingleton<IOpenAiService, OpenAiService>();

// Register the main Nutrition Service (now using Nutritionix)


// Commenting out potentially conflicting extension methods until verified
// NutritionAmbition.Backend.API.AiServiceExtensions.AddAiServices(builder.Services);
// NutritionAmbition.Backend.API.NutritionServiceExtensions.AddNutritionServices(builder.Services);

// Repos
builder.Services.AddSingleton<AccountsRepository>();
builder.Services.AddSingleton<FoodEntryRepository>(); // Make sure this is registered before IFoodEntryRepository
builder.Services.AddSingleton<IFoodEntryRepository, FoodEntryRepository>();

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

// Use anonymous auth middleware before authentication
app.UseMiddleware<AnonymousAuthMiddleware>();

// ✅ Use Authentication & Authorization Middleware
app.UseAuthentication();
app.UseAuthorization();

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
