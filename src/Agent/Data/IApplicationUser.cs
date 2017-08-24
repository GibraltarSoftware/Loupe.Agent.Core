using System.Collections.Generic;

namespace Gibraltar.Agent.Data
{
    /// <summary>
    /// Extended information about a single user the application
    /// </summary>
    public interface IApplicationUser
    {
        /// <summary>
        /// Optional. An absolute, unique key for the user to use as a primary match
        /// </summary>
        string Key { get; set; }

        /// <summary>
        /// The fully qualified user name
        /// </summary>
        /// <remarks>If Key isn't specified this value is used as the alternate key</remarks>
        string FullyQualifiedUserName { get; }

        /// <summary>
        /// A display label for the user (such as their full name)
        /// </summary>
        string Caption { get; set; }

        /// <summary>
        /// Optional.  A primary email address for the user
        /// </summary>
        string EmailAddress { get; set; }

        /// <summary>
        /// Optional.  A phone number or other telecommunication alias
        /// </summary>
        string Phone { get; set; }

        /// <summary>
        /// Optional.  A label for the organization this user is a part of
        /// </summary>
        string Organization { get; set; }

        /// <summary>
        /// Optional.  A primary role for this user with respect to this application
        /// </summary>
        string Role { get; set; }

        /// <summary>
        /// Optional.  The primary tenant this user is a part of.
        /// </summary>
        string Tenant { get; set; }

        /// <summary>
        /// Application provided properties 
        /// </summary>
        Dictionary<string, string> Properties { get; }
    }
}
