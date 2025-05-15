// Middleware/AttachAccountIdToResponseMiddleware.cs
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.DataContracts;
using System;
using System.Linq;

namespace NutritionAmbition.Backend.API.Middleware
{
    public class AttachAccountIdToResponseMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AttachAccountIdToResponseMiddleware> _logger;

        public AttachAccountIdToResponseMiddleware(RequestDelegate next, ILogger<AttachAccountIdToResponseMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var originalBodyStream = context.Response.Body;

            using var memStream = new MemoryStream();
            context.Response.Body = memStream;

            await _next(context);

            memStream.Position = 0;

            var responseText = await new StreamReader(memStream).ReadToEndAsync();
            memStream.Position = 0;

            if (!string.IsNullOrWhiteSpace(responseText) && context.Response.ContentType?.Contains("application/json") == true)
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(responseText);
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("isSuccess", out _) &&
                        root.TryGetProperty("errors", out _) &&
                        root.ValueKind == JsonValueKind.Object &&
                        context.Items["Account"] is Account account)
                    {
                        // Inject AccountId into the response JSON
                        using var outputStream = new MemoryStream();
                        using var writer = new Utf8JsonWriter(outputStream);

                        writer.WriteStartObject();
                        foreach (var property in root.EnumerateObject())
                        {
                            property.WriteTo(writer);
                        }

                        writer.WriteString("accountId", account.Id);
                        writer.WriteEndObject();
                        await writer.FlushAsync();

                        outputStream.Position = 0;
                        context.Response.Body = originalBodyStream;
                        context.Response.ContentLength = outputStream.Length;
                        await outputStream.CopyToAsync(context.Response.Body);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to inject accountId into response");
                }
            }

            memStream.Position = 0;
            await memStream.CopyToAsync(originalBodyStream);
        }
    }
}

