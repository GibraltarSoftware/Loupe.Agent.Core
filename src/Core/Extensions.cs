using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Gibraltar
{
    public static class Extensions
    {
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
