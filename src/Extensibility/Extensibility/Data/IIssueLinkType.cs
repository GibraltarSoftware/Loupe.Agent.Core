namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A single link generator
    /// </summary>
    /// <remarks>An issue link is rendered by combining the value on the link (if any)
    /// with the UrlFormat string to create the fully qualified URL.</remarks>
    public interface IIssueLinkType
    {
        /// <summary>
        /// The unique name of this link generator (not displayed)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The display order of links using this generator
        /// </summary>
        /// <remarks>When there are multiple links on an issue this controls the order they are presented in</remarks>
        int Sequence { get; }

        /// <summary>
        /// A display label for links using this link type
        /// </summary>
        /// <remarks>Limited to 120 characters</remarks>
        string Caption { get; }

        /// <summary>
        /// A description of this link generator
        /// </summary>
        /// <remarks>Not limited in length.</remarks>
        string Description { get; }

        /// <summary>
        /// A .NET string format for a single value to be inserted to generate the fully qualified URL
        /// </summary>
        /// <remarks>If null or empty then each link is assumed to use a fully qualified URL.</remarks>
        string UrlTemplate { get; }
    }
}
