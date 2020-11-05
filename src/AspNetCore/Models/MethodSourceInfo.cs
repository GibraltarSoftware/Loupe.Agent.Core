#pragma warning disable 1591

using System.ComponentModel;
using System.Text.Json;

namespace Loupe.Agent.AspNetCore.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class MethodSourceInfo
    {
        /// <summary>
        /// File that the error occurred in
        /// </summary>
        public string? File { get; set; }

        /// <summary>
        /// Class that the error occured in
        /// </summary>
        /// <value>The class.</value>
        public string? Class { get; set; }

        /// <summary>
        /// Function that was being executed when error occurred
        /// </summary>
        public string? Method { get; set; }

        /// <summary>
        /// Optional. The line number upon which the error occurred
        /// </summary>
        public int? Line { get; set; }

        /// <summary>
        /// Optional. The column number upon which the error occurred
        /// </summary>
        public int? Column { get; set; }

        public bool IsEmpty()
        {
            return string.IsNullOrWhiteSpace(File) 
                   && string.IsNullOrWhiteSpace(Class) 
                   && string.IsNullOrWhiteSpace(Method) 
                   && !Line.HasValue && !Column.HasValue;
        }

        public void WriteJson(Utf8JsonWriter writer, JsonEncodedText propertyName)
        {
            if (IsEmpty()) return;
            
            writer.WritePropertyName(propertyName);
            writer.WriteStartObject();
            writer.WriteString(FileProperty, File);
            writer.WriteString(ClassProperty, Class);
            writer.WriteString(MethodProperty, Method);
            writer.WriteNumber(LineProperty, Line ?? 0);
            writer.WriteNumber(ColumnProperty, Column ?? 0);
            writer.WriteEndObject();
        }
        
        private static readonly JsonEncodedText FileProperty = JsonEncodedText.Encode(nameof(File));
        private static readonly JsonEncodedText ClassProperty = JsonEncodedText.Encode(nameof(Class));
        private static readonly JsonEncodedText MethodProperty = JsonEncodedText.Encode(nameof(Method));
        private static readonly JsonEncodedText LineProperty = JsonEncodedText.Encode(nameof(Line));
        private static readonly JsonEncodedText ColumnProperty = JsonEncodedText.Encode(nameof(Column));
    }
}