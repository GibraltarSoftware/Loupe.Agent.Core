﻿using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A single user of one of the applications in this repository
    /// </summary>
    public interface IApplicationUser
    {
        /// <summary>
        /// Optional.  The authoritative key provided by the Agent for this user.
        /// </summary>
        string Key { get; set; }

        /// <summary>
        /// The fully qualified user name as originally provided (E.g. DOMAIN\User or user@domain)
        /// </summary>
        string FullyQualifiedUserName { get; }

        /// <summary>
        /// A display caption for the user - often their full name.
        /// </summary>
        string Caption { get; set; }

        /// <summary>
        /// The email address used to communicate with the user.
        /// </summary>
        string EmailAddress { get; set; }

        /// <summary>
        /// The phone number or other telecommunication alias for the user.
        /// </summary>
        string Phone { get; set; }

        /// <summary>
        /// Optional. The organization this user belongs to - such as a customer.
        /// </summary>
        string Organization { get; set; }

        /// <summary>
        /// Optional.  A title for the user (e.g. job title)
        /// </summary>
        string Title { get; set; }

        /// <summary>
        /// Optional.  The time zone the user is associated with
        /// </summary>
        string TimeZoneCode { get; set; }

        /// <summary>
        /// Application provided properties 
        /// </summary>
        IDictionary<string, string> Properties { get; }
    }
}
