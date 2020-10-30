using System.Text.Json;

namespace Loupe.Agent.AspNetCore.Models
{
    public class ClientDimensions
    {
        public long Height { get; set; }
        public long Width { get; set; }

        public void WriteJson(Utf8JsonWriter writer, JsonEncodedText propertyName)
        {
            writer.WritePropertyName(propertyName);
            writer.WriteStartObject();
            writer.WriteNumber(HeightProperty, Height);
            writer.WriteNumber(WidthProperty, Width);
            writer.WriteEndObject();
        }
        
        private static readonly JsonEncodedText HeightProperty = JsonEncodedText.Encode(nameof(Height));
        private static readonly JsonEncodedText WidthProperty = JsonEncodedText.Encode(nameof(Width));
    }
}