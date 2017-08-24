using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A link from this issue to an external web address.
    /// </summary>
    public interface IIssueLink
    {
        /// <summary>
        /// The unique key for this link
        /// </summary>
        Guid Id { get;  }

        /// <summary>
        /// A display label for this link
        /// </summary>
        string Caption { get;  }

        /// <summary>
        /// The value to be provided to the issue link type to generate the full URL for this link.
        /// </summary>
        /// <remarks>This value is merged with the UrlTemplate on the Link Type to create the final URL.  
        /// if there is no URL Template specified on the link type then this should be a full URL, otherwise
        /// it will be inserted into the template as the first insertion value for a .NET format string.</remarks>
        string Value { get;  }

        /// <summary>
        /// The issue this link is associated with.
        /// </summary>
        IIssue Issue { get;  }

        /// <summary>
        /// The generator for this link
        /// </summary>
        /// <remarks>Link Types are individual generators for links to 
        /// help centralize URL management to external systems.</remarks>
        IIssueLinkType Type { get;  }

        /// <summary>
        /// The fully qualified effective URL for this link.
        /// </summary>
        string Url { get; }

        /// <summary>
        /// Remove this link from the issue
        /// </summary>
        void Remove();
    }
}
