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
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 🟢 Bind Settings
var mongoDbSettings = builder.Configuration.GetSection(AppConstants.MongoDbSettings).Get<MongoDBSettings>();
builder.Services.AddSingleton(mongoDbSettings);

var firebaseSettings = builder.Configuration.GetSection(AppConstants.FirebaseSettings).Get<FirebaseSettings>();
builder.Services.AddSingleton(firebaseSettings);

var openAiSettings = builder.Configuration.GetSection(AppConstants.OpenAiSettings).Get<OpenAiSettings>();
builder.Services.AddSingleton(openAiSettings);

var nutritionApiSettings = builder.Configuration.GetSection(AppConstants.NutritionApiSettings).Get<NutritionApiSettings>();
builder.Services.AddSingleton(nutritionApiSettings);

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
builder.Services.AddSingleton<AiService>();
builder.Services.AddSingleton<FoodEntryService>();
// // builder.Services.AddSingleton<FoodParsingService>();
builder.Services.AddSingleton<NutritionApiService>();
// builder.Services.AddSingleton<NutritionService>();
builder.Services.AddSingleton<OpenAiService>();
// builder.Services.AddSingleton<UsdaFoodDataService>();
NutritionAmbition.Backend.API.AiServiceExtensions.AddAiServices(builder.Services);
NutritionAmbition.Backend.API.NutritionServiceExtensions.AddNutritionServices(builder.Services);

// Repos
builder.Services.AddSingleton<AccountsRepository>();
builder.Services.AddSingleton<FoodEntryRepository>();

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
