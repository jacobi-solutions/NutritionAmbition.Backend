using System.Text.Json;
using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API {


    public class SafeStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString() ?? string.Empty;
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                using (JsonDocument document = JsonDocument.ParseValue(ref reader))
                {
                    // It's an object — return empty string
                    return string.Empty;
                }
            }
            else if (reader.TokenType == JsonTokenType.Null)
            {
                return string.Empty;
            }

            throw new JsonException($"Unexpected token type {reader.TokenType} for string property.");
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
