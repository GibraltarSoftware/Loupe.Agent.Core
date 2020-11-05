#pragma warning disable 1591

using System.ComponentModel;
using System.Text.Json;

namespace Loupe.Agent.AspNetCore.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ClientOS
    {
        public int Architecture { get; set; }

        public string? Family { get; set; }

        public string? Version { get; set; }

        public void WriteJson(Utf8JsonWriter writer, JsonEncodedText propertyName)
        {
            writer.WritePropertyName(propertyName);
            writer.WriteStartObject();
            writer.WriteNumber(ArchitectureProperty, Architecture);
            writer.WriteString(FamilyProperty, Family);
            writer.WriteString(VersionProperty, Version);
            writer.WriteEndObject();
        }
        
        private static readonly JsonEncodedText ArchitectureProperty = JsonEncodedText.Encode(nameof(Architecture));
        private static readonly JsonEncodedText FamilyProperty = JsonEncodedText.Encode(nameof(Family));
        private static readonly JsonEncodedText VersionProperty = JsonEncodedText.Encode(nameof(Version));
    }
}