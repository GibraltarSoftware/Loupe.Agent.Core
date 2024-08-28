using System;
using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// Contains the log information for a single execution cycle
    /// </summary>
    /// <remarks>A session contains all of the thread, event, and metric information captured when it originally was executing
    /// and can be extended with analysis information including comments and markers.</remarks>
    public interface ISession : IEquatable<ISession>, IComparable<ISession>
    {
        /// <summary>
        /// A constant, unique identifier for this session.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// A short end-user display caption 
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// An extended description without formatting.
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Indicates whether a session had errors during re-hydration and has lost some packets.
        /// </summary>
        bool HasCorruptData { get; }

        /// <summary>
        /// The final status of the session.
        /// </summary>
        SessionStatus Status { get; }

        /// <summary>
        /// The worst severity of the log messages in the session
        /// </summary>
        LogMessageSeverity TopSeverity { get; }

        /// <summary>
        /// The number of messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        long MessageCount { get; }

        /// <summary>
        /// The number of critical messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        long CriticalCount { get; }

        /// <summary>
        /// The number of error messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        long ErrorCount { get; }

        /// <summary>
        /// The number of warning messages in the messages collection.
        /// </summary>
        /// <remarks>This value is cached for high performance and reflects all of the known messages.  If only part
        /// of the files for a session are loaded, the totals as of the latest file loaded are used.  This means the
        /// count of items may exceed the actual number of matching messages in the messages collection if earlier
        /// files are missing.</remarks>
        long WarningCount { get; }

        /// <summary>
        /// Summary and common information about this session
        /// </summary>
        ISessionSummary Summary { get; }

        /// <summary>
        /// The list of threads associated with this session.  Threads are sorted by their thread Id and creation time.
        /// </summary>
        IThreadInfoCollection Threads { get; }

        /// <summary>
        /// The list of assemblies associated with this session.  Assemblies are sorted by their unique full names.
        /// </summary>
        IAssemblyInfoCollection Assemblies { get; }

        /// <summary>
        /// The list of application users associated with this session.
        /// </summary>
        IApplicationUserCollection Users { get; }

        /// <summary>
        /// The set of log messages for this session.
        /// </summary>
        /// <returns>An enumerable of the messages</returns>
        /// <remarks>This method provides an enumerable that reads the session data from the data file each time it is iterated
        /// so it won't consume excessive memory even if the file is very large or contains very large messages.</remarks>
        IEnumerable<ILogMessage> GetMessages();
            
        /// <summary>
        /// The set of all metrics tracked in this session.
        /// </summary>
        IMetricDefinitionCollection MetricDefinitions { get; }
    }
}
