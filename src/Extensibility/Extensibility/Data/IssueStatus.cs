using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// Available status of an issue.
    /// </summary>
    [Flags]
    public enum IssueStatus
    {
        /// <summary>
        /// No value filter
        /// </summary>
        None = 0,

        /// <summary>
        /// Issue is active.
        /// </summary>
        Active = 1,

        /// <summary>
        /// Issue is new.
        /// </summary>
        New = 2,

        /// <summary>
        /// Issue is resolved.
        /// </summary>
        Resolved = 4,

        /// <summary>
        /// Issue is suppressed.
        /// </summary>
        Suppressed = 8,

        /// <summary>
        /// Issue is open (active or new)
        /// </summary>
        Open = Active | New,

        /// <summary>
        /// Issue is closed (not active or new)
        /// </summary>
        Closed = Resolved | Suppressed
    }
}
