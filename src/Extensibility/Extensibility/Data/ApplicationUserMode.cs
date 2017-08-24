namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// The user tracking mode for an application
    /// </summary>
    public enum ApplicationUserMode
    {
        /// <summary>
        /// User tracking is disabled for this application
        /// </summary>
        None = 0,

        /// <summary>
        /// The application runs as a single user which is the process user
        /// </summary>
        SingleUser = 1,

        /// <summary>
        /// The application can impersonate many users
        /// </summary>
        MultiUser = 2
    }
}
