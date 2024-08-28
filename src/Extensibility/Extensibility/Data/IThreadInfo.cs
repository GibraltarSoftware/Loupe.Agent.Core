using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// An interface for accessing information about a thread.
    /// </summary>
    public interface IThreadInfo
    {
        /// <summary>
        /// The session this thread was recorded with
        /// </summary>
        ISession Session { get; }

        /// <summary>
        /// The unique key for this thread
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// The managed thread ID of the thread which originated this log message.
        /// </summary>
        int ThreadId { get; }

        /// <summary>
        /// The name of the thread which originated this log message.
        /// </summary>
        string ThreadName { get; }

        /// <summary>
        /// The application domain identifier of the app domain which originated this log message.
        /// </summary>
        int DomainId { get; }

        /// <summary>
        /// The friendly name of the app domain which originated this log message.
        /// </summary>
        string DomainName { get; }

        /// <summary>
        /// Indicates whether the thread which originated this log message is a background thread.
        /// </summary>
        bool IsBackground { get; }

        /// <summary>
        /// Indicates whether the thread which originated this log message is a threadpool thread.
        /// </summary>
        bool IsThreadPoolThread { get; }
    }
}
