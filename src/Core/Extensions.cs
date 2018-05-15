using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Gibraltar
{
    /// <summary>
    /// Conversion extensions
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Convert from .NET Core Architecture type to ProcessorArchitecture type.
        /// </summary>
        /// <param name="architecture"></param>
        /// <returns></returns>
        public static ProcessorArchitecture ToProcessorArchitecture(this Architecture architecture)
        {
            switch (architecture)
            {
                case Architecture.X86:
                    return ProcessorArchitecture.X86;
                case Architecture.X64:
                    return ProcessorArchitecture.Amd64;
                case Architecture.Arm:
                    return ProcessorArchitecture.Arm;
                case Architecture.Arm64:
                    return ProcessorArchitecture.Arm; ;
                default:
                    throw new ArgumentOutOfRangeException(nameof(architecture), architecture, null);
            }
        }
    }
}
