// ReSharper disable once CheckNamespace

using System.Globalization;
using System.Linq;
using System.Reflection;

namespace System
{
    public static class TypeShim
    {
        public static object InvokeMember(this Type type, string memberName, BindingFlags bindingFlags, object ignoreBinder, object target, object[] args, CultureInfo cultureInfo)
        {
            if (bindingFlags.HasFlag(BindingFlags.GetProperty))
            {
                var propertyInfo = type.GetProperty(memberName, bindingFlags);
                return propertyInfo?.GetValue(target);
            }
            if (bindingFlags.HasFlag(BindingFlags.GetField))
            {
                var fieldInfo = type.GetField(memberName, bindingFlags);
                return fieldInfo?.GetValue(target);
            }
            if (bindingFlags.HasFlag(BindingFlags.InvokeMethod))
            {
                var methodInfo = type.GetMethod(memberName, bindingFlags);
                return methodInfo?.Invoke(target, args);
            }
            throw new InvalidOperationException("InvokeMember shim failed, could not determine type of member to invoke.");
        }

        public static bool IsDefined(this Type type, Type attributeType, bool inherit)
        {
            return type.GetTypeInfo().GetCustomAttributes(inherit).Any(attributeType.IsInstanceOfType);
        }
    }
}