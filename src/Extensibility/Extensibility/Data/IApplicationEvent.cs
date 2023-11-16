using System;
using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// Distinct warnings and errors within an application
    /// </summary>
    /// <remarks>An Application Event aggregates similar log events together within a single application.
    /// A fingerprint is calculated for each log message.  Within a single session all of the log messages with 
    /// the same signature are grouped together into a Log Event.  All of the Log Events with the same fingerprint 
    /// within the same Application are grouped together in the same Application Event.</remarks>
    public interface IApplicationEvent
    {
        /// <summary>
        /// The unique key of this application event
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// The link to this item on the server
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// The short display caption for this event
        /// </summary>
        /// <remarks>Limited to 120 characters.</remarks>
        string Caption { get; }

        /// <summary>
        /// The unique fingerprint of this application event within the application
        /// </summary>
        /// <remarks>Each application event has a fingerprint which is used to associate similar log events
        /// with the same application event.  There will only be one application event with a given fingerprint
        /// for a specific application.</remarks>
        string FingerprintHash { get; }

        /// <summary>
        /// The severity of all the log messages associated with this event.
        /// </summary>
        LogMessageSeverity Severity { get; }

        /// <summary>
        /// Display caption and description for the severity
        /// </summary>
        IDisplay SeverityDisplay { get; }

        /// <summary>
        /// The total number of sessions this event has occurred in
        /// </summary>
        int SessionCount { get; }

        /// <summary>
        /// The total number of occurrences of log messages that occurred which match this event.
        /// </summary>
        long OccurrenceCount { get; }

        /// <summary>
        /// The total number of distinct users affected by this event.
        /// </summary>
        int ApplicationUserCount { get; }

        /// <summary>
        /// True if this event has been suppressed, ensuring new occurrences will not cause it to be marked for review.
        /// </summary>
        bool IsSuppressed { get; }

        /// <summary>
        /// The number of computers that have recorded this event.
        /// </summary>
        int ComputerCount { get; }

        /// <summary>
        /// True if this event has been queued for review to determine if it should be converted to an issue, suppressed, or deferred.
        /// </summary>
        bool ForReview { get; }

        /// <summary>
        /// The timestamp of the very last log message related to this application event
        /// </summary>
        /// <remarks>This will be associated with the LastLogEvent</remarks>
        DateTimeOffset LastOccurrenceTimestamp { get; }

        /// <summary>
        /// The timestamp of the very first log message related to this application event
        /// </summary>
        DateTimeOffset FirstOccurrenceTimestamp { get; }

        /// <summary>
        /// The application this event is associated with
        /// </summary>
        IRepositoryApplication Application { get; }

        /// <summary>
        /// The earliest version that this application event has been found in.
        /// </summary>
        /// <remarks>In this case First means the first version, not the version of the first occurrence.</remarks>
        IRepositoryApplicationVersion FirstVersion { get; }

        /// <summary>
        /// The latest version that this application event has been found in.
        /// </summary>
        /// <remarks>In this case Last means the last version, not the version of the last occurrence.</remarks>
        IRepositoryApplicationVersion LastVersion { get; }

        /// <summary>
        /// The highest release type, based on sequence
        /// </summary>
        /// <remarks>Higher release types are more important, typically indicating a version that has gone farther through the promotion or publication process</remarks>
        IReleaseType MaxReleaseType { get; }

        /// <summary>
        /// Optional.  The Issue this application event is related to, if any.
        /// </summary>
        IIssue Issue { get; }

        /// <summary>
        /// Get an enumerable for all of the log events related to this application event
        /// </summary>
        /// <returns></returns>
        /// <remarks>Since there may be a very large number of log events and they may each contain significant
        /// data callers are advised to process these in a streaming fashion - evaluating each as you go.</remarks>
        IEnumerable<ILogEvent> GetLogEvents();

        /// <summary>
        /// Get an enumerable for all of the computers related to this application event
        /// </summary>
        /// <returns></returns>
        /// <remarks>Since there may be a very large number of computers and they may each contain significant
        /// data callers are advised to process these in a streaming fashion - evaluating each as you go.</remarks>
        IEnumerable<IComputer> GetComputers();

        /// <summary>
        /// Get an enumerable for all of the sessions related to this application event
        /// </summary>
        /// <returns></returns>
        /// <remarks>Since there may be a very large number of sessions and they may each contain significant
        /// data callers are advised to process these in a streaming fashion - evaluating each as you go.</remarks>
        IEnumerable<ISessionSummary> GetSessions();
        
        /// <summary>
        /// The last computer (by timestamp) to report a log message associated with this event.
        /// </summary>
        IComputer LastComputer { get; }

        /// <summary>
        /// The last application user (by timestamp) to experience a log message associated with this event.
        /// </summary>
        /// <remarks>Only available if the application tracks users and this event occurred to a specific user.</remarks>
        IApplicationUser LastApplicationUser { get; }

        /// <summary>
        /// The last log event (by timestamp) that reported this event
        /// </summary>
        ILogEvent LastLogEvent { get; }

        /// <summary>
        /// Create a new issue with this application event
        /// </summary>
        /// <param name="caption">A summary display caption for the new issue.  Limited to 120 characters.</param>
        /// <param name="description">An extended description of the issue.</param>
        /// <param name="workaround">Optional.  A workaround to the problem.</param>
        /// <param name="activate">True if the issue should be created in the Active status instead of New.</param>
        /// <param name="isPublic">True if the issue should be visible to anonymous users</param>
        /// <param name="publicCaption">Optional.  Alternate summary display caption for anonymous public users.  Limited to 120 characters.</param>
        /// <param name="publicDescription">Optional.  Alternate extended description of the issue for anonymous public users.</param>
        /// <param name="publicWorkaround">Optional.  Alternate workaround for the problem for anonymous public users.</param>
        /// <param name="note">Optional.  An issue note to record with the creation</param>
        /// <param name="assignedUserName">Optional.  The user to assign the issue to.  If null it will be left unassigned.</param>
        /// <param name="userName">Optional.  The user that is performing the action</param>
        /// <returns>The new issue</returns>
        /// <remarks>The application event must not be already related to an issue.</remarks>
        void AddIssue(string caption, string description, string workaround,
            bool activate, bool isPublic, string? publicCaption = null, string? publicDescription = null, string? publicWorkaround = null, string? note = null,
            string? assignedUserName = null, string? userName = null);
    }
}
