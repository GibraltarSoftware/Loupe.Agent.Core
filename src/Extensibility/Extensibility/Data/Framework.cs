using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// The primary framework of the process that recorded the session
    /// </summary>
    [Flags]
    public enum Framework
    {
        /// <summary>
        /// The framework couldn't be determined (assume .NET)
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The .NET Framework or MONO
        /// </summary>
        DotNet = 1,

        /// <summary>
        /// .NET Core
        /// </summary>
        DotNetCore = 2,

        /// <summary>
        /// Java
        /// </summary>
        Java = 4
    }
}
