using System;
using System.Reflection;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    ///  Detail about .NET assemblies loaded into the session
    /// </summary>
    public interface IAssemblyInfo: IComparable<IAssemblyInfo>, IEquatable<IAssemblyInfo>
    {
        /// <summary>
        /// The session this assembly was recorded as part of
        /// </summary>
        ISession Session { get; }

        /// <summary>
        /// A display caption for the assembly.
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// The full name of the assembly.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The unique Id of this assembly information within the session.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// The standard full name for the culture (like EN-US)
        /// </summary>
        string CultureName { get; }

        /// <summary>
        /// The full name for the assembly, generally unique within an application domain.
        /// </summary>
        string FullName { get; }

        /// <summary>
        /// The .NET Runtime version the assembly image was compiled against.
        /// </summary>
        string ImageRuntimeVersion { get; }

        /// <summary>
        /// The full location to the assembly.
        /// </summary>
        string Location { get; }

        /// <summary>
        /// The short name of the assembly (typically the same as the file name without extension).  Not unique within an application domain.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The processor architecture the assembly was compiled for.
        /// </summary>
        ProcessorArchitecture ProcessorArchitecture { get; }

        /// <summary>
        /// Indicates of the assembly was loaded out of the Global Assembly Cache.
        /// </summary>
        bool GlobalAssemblyCache { get; }

        /// <summary>
        /// The Assembly Version that was loaded.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// The file version recorded in the manifest assembly, if available.  May be null.
        /// </summary>
        string FileVersion { get; }

        /// <summary>
        /// The date &amp; time the assembly was loaded by the runtime.
        /// </summary>
        DateTimeOffset LoadedTimeStamp { get; }
    }
}
