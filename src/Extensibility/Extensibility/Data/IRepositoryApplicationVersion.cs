using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A single application version
    /// </summary>
    public interface IRepositoryApplicationVersion
    {
        /// <summary>
        /// The version, unique within a particular application
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// The version to display
        /// </summary>
        /// <remarks>In some cases it's advisable to fudge a version number slightly - like when multiple sub versions
        /// are really the same software packaged differently or when a minor mistake was made and another version
        /// was shipped immediately.</remarks>
        Version DisplayVersion { get; }

        /// <summary>
        /// The display name of the version
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// Optional.  An extended description of this version.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Optional.  The date the version was published
        /// </summary>
        DateTime? ReleaseDate { get; }

        /// <summary>
        /// Optional.  A full URL to release notes for this version
        /// </summary>
        string ReleaseNotesUrl { get;  }

        /// <summary>
        /// The application this version relates to
        /// </summary>
        IRepositoryApplication Application { get; }

        /// <summary>
        /// Optional.  The promotion level specified for this version
        /// </summary>
        /// <remarks>If specified, all sessions will have this promotion level regardless of where they were run.</remarks>
        IPromotionLevel PromotionLevel { get; }
    }
}
