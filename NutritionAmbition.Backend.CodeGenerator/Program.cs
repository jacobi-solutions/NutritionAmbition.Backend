using System;
using System.IO;
using System.Threading.Tasks;
using NSwag;
using NSwag.CodeGeneration.TypeScript;

class Program
{
    private const string _frontendAppName = "nutrition-ambition.frontend";  // Name of your Angular project
    private const string _backendAppName = "NutritionAmbition.Backend";
    private const string _generatedApiFileName = "nutrition-ambition-api.service.ts";
    private const string _generatedApiClassName = "NutritionAmbitionApiService";
    private const int _localhostPort = 5165; // Change if API runs on a different port

    static async Task Main(string[] args)
    {
        await GenerateTypescriptAPI();
    }

    private static async Task GenerateTypescriptAPI()
    {
        try
        {
            Console.WriteLine("🔄 Fetching Swagger JSON...");
            var document = await OpenApiDocument.FromUrlAsync($"http://localhost:{_localhostPort}/swagger/v1/swagger.json");

            var clientSettings = new TypeScriptClientGeneratorSettings
            {
                ClassName = _generatedApiClassName,
                HttpClass = HttpClass.HttpClient,
                Template = TypeScriptTemplate.Angular,
                InjectionTokenType = InjectionTokenType.InjectionToken,
                GenerateClientInterfaces = true,
                TypeScriptGeneratorSettings = 
                {
                    TypeScriptVersion = 4.0m, // Ensure compatibility with modern TypeScript features
                    // Add any additional settings here if needed
                }
            };

            var clientGenerator = new TypeScriptClientGenerator(document, clientSettings);
            var code = clientGenerator.GenerateFile();

            // Fix any TypeScript type issues
            // code = code.Replace("FileParameter[]", "any[]");

            string currentDirectoryPath = Directory.GetCurrentDirectory();
            Console.WriteLine($"📂 Current directory: {currentDirectoryPath}");

            var relativeToPath = currentDirectoryPath.Split(_backendAppName)[0];
            Console.WriteLine($"The relativeTo path is {relativeToPath}");

            var relativePath = Path.GetRelativePath(currentDirectoryPath, relativeToPath);
            Console.WriteLine($"The relative path is {relativePath}");

            var pathToWriteClient = $"{relativePath}/{_frontendAppName}/src/app/services/{_generatedApiFileName}";

            var fullPath = Path.GetFullPath(pathToWriteClient);
            File.WriteAllText(fullPath, code);
            Console.WriteLine("Code Generated :)");
            Console.WriteLine($"You can see for yourself here:");
            Console.WriteLine($"{ fullPath }");


            // // Get the parent directory of the backend repo
            // var parentDirectory = Directory.GetParent(currentDirectoryPath).FullName;
            
            // // Construct the path to the frontend repo
            // var frontendPath = Path.Combine(parentDirectory, _frontendAppName, "src", "app", "services");

            // // Ensure the directory exists before writing the file
            // Directory.CreateDirectory(frontendPath);

            // var fullPath = Path.Combine(frontendPath, _generatedApiFileName);
            // File.WriteAllText(fullPath, code);

            // Console.WriteLine("✅ TypeScript API client generated successfully!");
            // Console.WriteLine($"📂 File location: {fullPath}");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}
