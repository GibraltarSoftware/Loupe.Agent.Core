using System;
using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A single software problem being tracked by Loupe.
    /// </summary>
    /// <remarks>
    /// An issue ties together one or more application events for an application into a single software problem that needs
    /// to be investigated and resolved.  These are typically considered software defects.
    /// </remarks>
    public interface IIssue
    {
        /// <summary>
        /// The unique key of this issue
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// The unique name of the issue
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The link to this item on the server
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// A short display label for the issue.
        /// </summary>
        /// <remarks>Limited to 120 characters.</remarks>
        string Caption { get; }
        
        /// <summary>
        /// An extended description of the issue
        /// </summary>
        /// <remarks>Not limited in length.</remarks>
        string Description { get; }

        /// <summary>
        /// Optional.  A workaround for this issue.
        /// </summary>
        /// <remarks>Not limited in length.</remarks>
        string Workaround { get; }

        /// <summary>
        /// The current status of this issue
        /// </summary>
        IssueStatus Status { get; }

        /// <summary>
        /// Display information for the status
        /// </summary>
        IDisplay StatusDisplay { get; }

        /// <summary>
        /// Indicates if the issue should be publicly viewable
        /// </summary>
        bool IsPublic { get; }

        /// <summary>
        /// Optional.  If publicly viewable this caption is used instead of the main caption.
        /// </summary>
        /// <remarks>Limited to 120 characters.</remarks>
        string PublicCaption { get; }

        /// <summary>
        /// Optional.  If publicly viewable this description is used instead of the main description.
        /// </summary>
        /// <remarks>Not limited in length.</remarks>
        string PublicDescription { get; }

        /// <summary>
        /// Optional.  If publicly viewable this workaround is used instead of the main workaround.
        /// </summary>
        /// <remarks>Not limited in length.</remarks>
        string PublicWorkaround { get; }

        /// <summary>
        /// The total number of sessions affected by this issue.
        /// </summary>
        int SessionCount { get; }

        /// <summary>
        /// The total number of computers affected by this issue.
        /// </summary>
        int ComputerCount { get; }

        /// <summary>
        /// The total number of distinct users affected by this issue.
        /// </summary>
        int ApplicationUserCount { get; }

        /// <summary>
        /// The timestamp of the last update to this issue
        /// </summary>
        /// <remarks>Does not consider updates to events or statistics.</remarks>
        DateTimeOffset UpdatedTimestamp { get; }

        /// <summary>
        /// The timestamp of when this issue was created.
        /// </summary>
        DateTimeOffset AddedTimestamp { get; }

        /// <summary>
        /// The application this issue is associated with.
        /// </summary>
        IRepositoryApplication Application { get; }

        /// <summary>
        /// Optional. If this issue was resolved this is the version that it was resolved in.
        /// </summary>
        IRepositoryApplicationVersion FixedIn { get; }

        /// <summary>
        /// The user that added this issue
        /// </summary>
        /// <remarks>The LocalSystem user is used when the issue was created by a rule or Extension.</remarks>
        IUserProfile AddedBy { get; }

        /// <summary>
        /// Optional.  The user that this issue is assigned to.
        /// </summary>
        IUserProfile AssignedTo { get; }

        /// <summary>
        /// The last user that updated this issue
        /// </summary>
        /// <remarks>The LocalSystem user is used when the issue was created by a rule or Extension.</remarks>
        IUserProfile UpdatedBy { get; }

        /// <summary>
        /// The set of Application Events associated with this issue.
        /// </summary>
        ICollection<IApplicationEvent> ApplicationEvents { get; }

        /// <summary>
        /// The set of web links associated with this issue.
        /// </summary>
        ICollection<IIssueLink> Links { get; }

        /// <summary>
        /// The set of notes associated with this issue.
        /// </summary>
        ICollection<IIssueNote> Notes { get; }

        /// <summary>
        /// Add a new note to this issue
        /// </summary>
        /// <param name="category">The note category (denotes system vs. user notes)</param>
        /// <param name="note">The text note of unlimited length.  Plain text is recommended.</param>
        /// <param name="userName">Optional.  The user name of the user to credit the message to</param>
        /// <returns>The new note that has been added</returns>
        /// <remarks>If adding a system note no user name need be provided (and any specified will be ignored).</remarks>
        IIssueNote AddNote(IssueNoteCategory category, string note, string? userName = null);

        /// <summary>
        /// Add a new link to this issue
        /// </summary>
        /// <param name="type">The link type for this issue.  This defines a template for each link</param>
        /// <param name="caption">The label to use in the UI for this link</param>
        /// <param name="value">A string value to insert into the template from the link type or a full URL if there is no template stored on the link type.</param>
        /// <returns>The new link that was added.</returns>
        IIssueLink AddLink(IIssueLinkType type, string caption, string value);

        /// <summary>
        /// Reopens an issue that has been resolved or suppressed.
        /// </summary>
        /// <param name="note">Optional.  A text note of unlimited length.  Plain text is recommended.</param>
        /// <param name="userName">Optional.  The user name of the user to attribute the action to.</param>
        void ReOpen(string note, string? userName = null);

        /// <summary>
        /// Resolve an issue that is new or active
        /// </summary>
        /// <param name="fixedInVersion">The application version where the fix for this issue was first included</param>
        /// <param name="note">Optional.  A text note of unlimited length.  Plain text is recommended.</param>
        /// <param name="userName">Optional.  The user name of the user to attribute the action to.</param>
        void Resolve(IRepositoryApplicationVersion fixedInVersion, string note, string? userName = null);

        /// <summary>
        /// Suppress an issue so it will not be automatically reopened if it recurs.
        /// </summary>
        /// <param name="note">Optional.  A text note of unlimited length.  Plain text is recommended.</param>
        /// <param name="userName">Optional.  The user name of the user to attribute the action to.</param>
        void Suppress(string note, string? userName = null);
    }
}
