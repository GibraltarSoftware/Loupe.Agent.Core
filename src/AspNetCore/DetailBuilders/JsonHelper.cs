using System;
using System.Text.Json;

namespace Loupe.Agent.AspNetCore.DetailBuilders
{
    internal static class JsonHelper
    {
        public static void Copy(string? jsonSource, Utf8JsonWriter target, JsonEncodedText propertyName)
        {
            if (string.IsNullOrWhiteSpace(jsonSource) || jsonSource!.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var doc = JsonDocument.Parse(jsonSource);
            
            target.WritePropertyName(propertyName);
            doc.WriteTo(target);
        }
    }
}