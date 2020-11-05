#pragma warning disable 1591

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loupe.Agent.AspNetCore.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ClientDetails
    {
        public string? Description { get; set; }

        public string? Layout { get; set; }

        public string? Manufacturer { get; set; }

        public string? Name { get; set; }

        public string? Prerelease { get; set; }

        public string? Product { get; set; }

        [JsonPropertyName("ua")]
        public string? UserAgentString { get; set; }

        public string? Version { get; set; }

        public ClientOS? OS { get; set; }

        public ClientDimensions? Size { get; set; }

        public void WriteJson(Utf8JsonWriter writer, JsonEncodedText propertyName)
        {
            writer.WritePropertyName(propertyName);
            writer.WriteStartObject();
            writer.WriteString(DescriptionProperty, Description);
            writer.WriteString(LayoutProperty, Layout);
            writer.WriteString(ManufacturerProperty, Manufacturer);
            writer.WriteString(NameProperty, Name);
            writer.WriteString(PrereleaseProperty, Prerelease);
            writer.WriteString(ProductProperty, Product);
            writer.WriteString(UserAgentStringProperty, UserAgentString);
            writer.WriteString(VersionProperty, Version);
            OS?.WriteJson(writer, OSProperty);
            Size?.WriteJson(writer, SizeProperty);
            writer.WriteEndObject();
        }
        
        private static readonly JsonEncodedText DescriptionProperty = JsonEncodedText.Encode(nameof(Description));
        private static readonly JsonEncodedText LayoutProperty = JsonEncodedText.Encode(nameof(Layout));
        private static readonly JsonEncodedText ManufacturerProperty = JsonEncodedText.Encode(nameof(Manufacturer));
        private static readonly JsonEncodedText NameProperty = JsonEncodedText.Encode(nameof(Name));
        private static readonly JsonEncodedText PrereleaseProperty = JsonEncodedText.Encode(nameof(Prerelease));
        private static readonly JsonEncodedText ProductProperty = JsonEncodedText.Encode(nameof(Product));
        private static readonly JsonEncodedText UserAgentStringProperty = JsonEncodedText.Encode(nameof(UserAgentString));
        private static readonly JsonEncodedText VersionProperty = JsonEncodedText.Encode(nameof(Version));
        private static readonly JsonEncodedText OSProperty = JsonEncodedText.Encode(nameof(OS));
        private static readonly JsonEncodedText SizeProperty = JsonEncodedText.Encode(nameof(Size));
    }
}