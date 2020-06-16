using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.ObjectPool;

namespace Loupe.Extensions.Logging
{
    /// <summary>
    /// Extracts properties from logging state and scope into JSON details.
    /// </summary>
    /// <remarks>
    /// This class supports Loupe internally and is not intended to be used from your code.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class LoupeLogEnricher
    {
        // Pool HashSets for performance
        private static readonly ObjectPool<HashSet<string>> HashSetPool =
            new DefaultObjectPool<HashSet<string>>(new HashSetPooledObjectPolicy(), 64);
        
        /// <summary>
        /// Gets a JSON string for properties from the log message's state and any active scopes.
        /// </summary>
        /// <param name="state">The state from the log message.</param>
        /// <param name="provider">The <see cref="LoupeLoggerProvider"/>.</param>
        /// <typeparam name="T">The type of the state object</typeparam>
        /// <returns>A JSON string.</returns>
        public static string GetJson<T>(T state, LoupeLoggerProvider provider)
        {
            var propertySet = HashSetPool.Get();
            ByteBufferWriter buffer = null;
            Utf8JsonWriter jsonWriter = null;

            try
            {
                // Write state first
                WriteState(state, propertySet, ref buffer, ref jsonWriter);

                // Write any values from active scopes
                WriteScope(provider, propertySet, ref jsonWriter, ref buffer);

                if (!(jsonWriter is null))
                {
                    jsonWriter.WriteEndObject();
                    jsonWriter.Flush();
                    return buffer.OutputAsString;
                }
            }
            finally
            {
                HashSetPool.Return(propertySet);
                if (!(jsonWriter is null))
                {
                    jsonWriter.Dispose();
                    buffer.Dispose();
                }
            }

            return null;
        }

        private static void WriteState<T>(T state, HashSet<string> propertySet, ref ByteBufferWriter buffer, ref Utf8JsonWriter jsonWriter)
        {
            if (!(state is IEnumerable<KeyValuePair<string, object>> pairs)) return;
            
            buffer = new ByteBufferWriter();
            jsonWriter = new Utf8JsonWriter(buffer);
            jsonWriter.WriteStartObject();
            
            Write(jsonWriter, propertySet, pairs);
        }

        private static void WriteScope(LoupeLoggerProvider provider, HashSet<string> propertySet, ref Utf8JsonWriter jsonWriter, ref ByteBufferWriter buffer)
        {
            if (provider.CurrentScope is null) return;
            
            if (jsonWriter is null)
            {
                buffer = new ByteBufferWriter();
                jsonWriter = new Utf8JsonWriter(buffer);
                jsonWriter.WriteStartObject();
            }
            provider.CurrentScope.Enrich(jsonWriter, propertySet);
        }

        internal static void Write(Utf8JsonWriter jsonWriter, HashSet<string> propertySet, IEnumerable<KeyValuePair<string, object>> pairs)
        {
            foreach (var pair in pairs)
            {
                // {OriginalFormat} is included in Logging's properties, we don't want it
                // Also ignore any properties that are already set
                if (pair.Key == "{OriginalFormat}" || !propertySet.Add(pair.Key))
                {
                    continue;
                }
                Write(jsonWriter, pair.Key, pair.Value);
            }
        }

        private static void Write(Utf8JsonWriter writer, string key, object value)
        {
            // Only write known types to the JSON
            switch (value)
            {
                case null:
                    return;
                case byte byteValue:
                    writer.WriteNumber(key, byteValue);
                    break;
                case bool boolValue:
                    writer.WriteBoolean(key, boolValue);
                    break;
                case char charValue:
                    writer.WriteString(key, charValue.ToString());
                    break;
                case decimal decimalValue:
                    writer.WriteNumber(key, decimalValue);
                    break;
                case double doubleValue:
                    writer.WriteNumber(key, doubleValue);
                    break;
                case float floatValue:
                    writer.WriteNumber(key, floatValue);
                    break;
                case int intValue:
                    writer.WriteNumber(key, intValue);
                    break;
                case long longValue:
                    writer.WriteNumber(key, longValue);
                    break;
                case short shortValue:
                    writer.WriteNumber(key, shortValue);
                    break;
                case sbyte sbyteValue:
                    writer.WriteNumber(key, sbyteValue);
                    break;
                case uint uintValue:
                    writer.WriteNumber(key, uintValue);
                    break;
                case ulong ulongValue:
                    writer.WriteNumber(key, ulongValue);
                    break;
                case ushort ushortValue:
                    writer.WriteNumber(key, ushortValue);
                    break;
                case Guid guidValue:
                    writer.WriteString(key, guidValue);
                    break;
                case DateTime dateTimeValue:
                    writer.WriteString(key, dateTimeValue);
                    break;
                case DateTimeOffset dateTimeOffsetValue:
                    writer.WriteString(key, dateTimeOffsetValue);
                    break;
                case string stringValue:
                    writer.WriteString(key, stringValue);
                    break;
            }
        }

        /// <summary>
        /// Create case-insensitive <see cref="HashSet{T}"/> and clear on return
        /// </summary>
        private class HashSetPooledObjectPolicy : PooledObjectPolicy<HashSet<string>>
        {
            public override HashSet<string> Create() => new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public override bool Return(HashSet<string> obj)
            {
                obj.Clear();
                return true;
            }
        }
    }
}