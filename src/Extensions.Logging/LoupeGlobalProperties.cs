using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Loupe.Extensions.Logging
{
    /// <summary>
    /// This type supports Loupe internally and should not be used in your code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class LoupeGlobalProperties
    {
        public LoupeGlobalProperties(IEnumerable<KeyValuePair<string, string>> properties)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in properties)
            {
                dictionary[property.Key] = property.Value;
            }
            Properties = new ReadOnlyDictionary<string, string>(dictionary);
        }

        internal IReadOnlyDictionary<string, string> Properties { get; }
    }
}