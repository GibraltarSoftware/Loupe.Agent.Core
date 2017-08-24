using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A single user of the system
    /// </summary>
    public interface IUserProfile
    {
        /// <summary>
        /// The unique key for this user
        /// </summary>
        /// <remarks>This id is globally unique and invariant for a specific user account.</remarks>
        Guid Id { get;  }

        /// <summary>
        /// The name used to authenticate to the system.
        /// </summary>
        /// <remarks>This is an alternate key for the user but is editable.</remarks>
        string UserName { get;  }

        /// <summary>
        /// A display caption for the user - often their full name.
        /// </summary>
        string UserCaption { get;  }

        /// <summary>
        /// The email address used to communicate with the user.
        /// </summary>
        string EmailAddress { get;  }

        /// <summary>
        /// Indicates if the user has requested HTML-formatted email when possible.
        /// </summary>
        bool UseHtmlEmail { get;  }

        /// <summary>
        /// Indicates if the user account has been deleted.  
        /// </summary>
        /// <remarks>Loupe does a soft-delete in many cases to preserve the history of actions.</remarks>
        bool Deleted { get;  }

        /// <summary>
        /// The number of unsuccessful authentication attempts since the last successful log in.
        /// </summary>
        int PasswordFailures { get;  }

        /// <summary>
        /// Indicates if the account has been locked out due to having too many password failures in a row.
        /// </summary>
        bool IsLockedOut { get;  }

        /// <summary>
        /// Indicates if the account has been approved and is now active.
        /// </summary>
        /// <remarks>This is used for deferred user creation</remarks>
        bool IsApproved { get;  }

        /// <summary>
        /// Optional, The name of the time zone the user has elected to view timestamps in.   Overrides the repository default.
        /// </summary>
        /// <remarks>If null the repository-wide setting will be used.</remarks>
        string TimeZoneCode { get;  }

        /// <summary>
        /// The last time the user performed an authenticated action
        /// </summary>
        /// <remarks>For performance reasons this timestamp isn't updated on every single action, but will
        /// be within a few minutes of the last time they executed an API request.</remarks>
        DateTimeOffset LastAccessTimestamp { get;  }

        /// <summary>
        /// The timestamp of when the user was created
        /// </summary>
        DateTimeOffset CreatedTimestamp { get;  }

        /// <summary>
        /// Optional.  The timestamp of the last time the account was locked out.
        /// </summary>
        DateTimeOffset? LastLockedOutTimestamp { get; }

        /// <summary>
        /// Optional.  The timestamp of the last time the password was changed.
        /// </summary>
        DateTimeOffset? LastPasswordChangeTimestamp { get; }

        /// <summary>
        /// Optional.  The timestamp of the last time the user failed to authenticate
        /// </summary>
        DateTimeOffset? LastPasswordFailureTimestamp { get; }

        /// <summary>
        /// The type of user - ranging from system to virtual.
        /// </summary>
        /// <remarks>Not all user types can log into the system.  User type incorporate the role the user has with respect to the repository.</remarks>
        UserType Type { get;  }
    }
}
