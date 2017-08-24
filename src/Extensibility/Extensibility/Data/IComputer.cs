using System;
using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A single unique computer that has sent information to Loupe
    /// </summary>
    public interface IComputer
    {
        /// <summary>
        /// The unique Id assigned to the computer
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// The link to this item on the server
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// Optional.  A configured display cation that overrides the full host name.
        /// </summary>
        /// <remarks>When specified, the DisplayCaption will reflect this value instead of the host name.</remarks>
        string Caption { get; }

        /// <summary>
        /// The display label for this computer, taking the host name and configuration into account
        /// </summary>
        /// <remarks>Typically this is the host name however users can specify an alternate value to use instead.
        /// This property takes into account the host name and configuration.</remarks>
        string DisplayCaption { get; }

        /// <summary>
        /// The most recent full host name (computer plus DNS Domain) seen for this computer
        /// </summary>
        string FullHostName { get; }

        /// <summary>
        /// The timestamp of the last time this computer has contacted the server
        /// </summary>
        /// <remarks>This is the time as seen by the server and not necessarily the ending 
        /// timestamp of the last log received</remarks>
        DateTimeOffset LastContactTimestamp { get; }

        /// <summary>
        /// Optional.  An override environment to use instead of the value specified in the session (if any)
        /// </summary>
        /// <remarks>If specified, this environment is used instead of the environment in any session that is
        /// received for any application.</remarks>
        IEnvironment Environment { get; }

        /// <summary>
        /// Optional.  An override promotion level to use instead of the value specified in the session (if any)
        /// </summary>
        /// <remarks>If specified, this promotion level is used instead of the level in any session that is
        /// received for any application, or the promotion level specified on an application version.</remarks>
        IPromotionLevel PromotionLevel { get; }

        /// <summary>
        /// Get an enumerable for all of the sessions related to this application event
        /// </summary>
        /// <returns>An enumerable that will iterate over all of the sessions</returns>
        /// <remarks>Since there may be a very large number of sessions and they may each contain significant
        /// data callers are advised to process these in a streaming fashion - evaluating each as you go.</remarks>
        IEnumerable<ISessionSummary> GetSessions();
    }
}
