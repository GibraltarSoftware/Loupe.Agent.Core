using System;
using System.Net.Http;
using System.Reflection;
using Loupe.Agent.AspNetCore.Infrastructure;
using Loupe.Agent.AspNetCore.Metrics;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore
{
    internal static class Extensions
    {
        private const string HttpContextMetricPrefix = "Loupe Request Metric";

        /// <summary>
        /// Process an arbitrary object instance into a string representation.
        /// </summary>
        /// <param name="value">An object to represent as a string, such as a return value.</param>
        /// <param name="getObjectDetails">True to get object details by evaluating ToString() even on class types.
        /// False to avoid calling ToString() on class types (still used for struct and enum types).</param>
        /// <returns>A string representing the provided object.</returns>
        internal static string? ObjectToString(object? value, bool getObjectDetails)
        {
            string? valueString = null;
            if (value == null)
            {
                valueString = "(null)";
            }
            else
            {
                Type parameterType = value.GetType();
                if (parameterType.IsClass == false)
                {
                    // Structs and enums should always have efficient ToString implementations, we assume.
                    if (parameterType.IsEnum)
                    {
                        //we want to pop the enum class name in front of the value to make it clear.
                        valueString = $"{parameterType.Name}.{value}";
                    }
                    else
                    {
                        valueString = value.ToString();
                    }
                }
                else if (parameterType == typeof(string))
                {
                    valueString = (string)value; // Use the value itself if it's already a string.
                }
                else
                {
                    if (getObjectDetails) // Before we call ToString() on a class instance...
                    {
                        valueString = value.ToString(); // Only evaluate ToString() of a class instance if logging details.

                        if (string.IsNullOrEmpty(valueString))
                        {
                            valueString = null; //and we don't need to do the next check...
                        }
                        else if (valueString.StartsWith(parameterType.Namespace + "." + parameterType.Name)) //if it's a generic type then it will have MORE than the full name.
                        {
                            valueString = null; // It's just the base object ToString implementation; we can improve on this.
                        }
                        // Otherwise, we'll keep the result of ToString to describe this object.
                    }

                    if (valueString == null) // If not logging details or if ToString was simply the type...
                    {
                        // Replace it with the type name and hash code to distinguish polymorphism and instance.
                        valueString = string.Format("{{{0}[0x{1:X8}]}}", parameterType.Namespace + "." + parameterType.Name, value.GetHashCode());
                    }
                }
            }

            return valueString;
        }

        /// <summary>
        /// Read a property from an object by name, typically because it's an anonymous type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="anonymousObject"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        internal static T? GetProperty<T>(this object anonymousObject, string propertyName) where T: class
        {
            return anonymousObject.GetType().GetTypeInfo().GetDeclaredProperty(propertyName)?.GetValue(anonymousObject) as T;
        }


        internal static string? GetSessionId(this HttpContext context)
        {
            return context.Items[Constants.SessionIdCookie] as string;
    
        }

        internal static string? GetAgentSessionId(this HttpContext context)
        {
            return context.Items[Constants.AgentSessionId] as string;
        }
    }
}