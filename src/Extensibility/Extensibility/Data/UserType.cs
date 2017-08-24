using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// The various levels of users in the system
    /// </summary>
    [Flags]
    public enum UserType
    {
        /// <summary>
        /// No user type has been specified
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The internal system user
        /// </summary>
        System = 1,

        /// <summary>
        /// A virtual user (only has an email address, can't access the system)
        /// </summary>
        Virtual = 2,

        /// <summary>
        /// A lightweight reviewer user
        /// </summary>
        Reviewer = 4,

        /// <summary>
        /// A normal user
        /// </summary>
        Full = 8,

        /// <summary>
        /// An administrator
        /// </summary>
        Administrator = 16,

        /// <summary>
        /// Users that have accounts
        /// </summary>
        CanLogIn = Reviewer | Full | Administrator,

        /// <summary>
        /// Users that can access all application-specific features
        /// </summary>
        FullApplicationAccess = Full | Administrator
    }
}
