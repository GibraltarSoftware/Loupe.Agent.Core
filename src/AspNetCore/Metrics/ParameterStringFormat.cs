using System.Buffers;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Abstractions;

namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// Provides methods to format parameter strings.
    /// </summary>
    public static class ParameterStringFormat
    {
        /// <summary>
        /// Formats 0, 1 or n parameters as a readable string.
        /// </summary>
        /// <param name="actionDescriptorParameters">The action descriptor parameters.</param>
        /// <returns>A formatted string.</returns>
        public static string FromList(IList<ParameterDescriptor> actionDescriptorParameters)
        {
            switch (actionDescriptorParameters.Count)
            {
                case 0:
                    return string.Empty;
                case 1:
                    return actionDescriptorParameters[0].Name;
                default:
                    return FromListImpl(actionDescriptorParameters);
            }
        }

        private static string FromListImpl(IList<ParameterDescriptor> actionDescriptorParameters)
        {
            int count = actionDescriptorParameters.Count;
            int length = 0;
            for (int i = 0; i < count; i++)
            {
                length += actionDescriptorParameters[i].Name.Length + 2;
            }

            var buffer = ArrayPool<char>.Shared.Rent(length);
            try
            {
                string name = actionDescriptorParameters[0].Name;
                name.CopyTo(0, buffer, 0, name.Length);
                int index = name.Length;

                for (int i = 1; i < count; i++)
                {
                    buffer[index++] = ',';
                    buffer[index++] = ' ';
                    name = actionDescriptorParameters[i].Name;
                    name.CopyTo(0, buffer, index, name.Length);
                    index += name.Length;
                }

                return new string(buffer, 0, index);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }
    }
}