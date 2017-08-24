namespace Gibraltar.Agent
{
    /// <summary>
    /// The type of process the application was run as.
    /// </summary>
    public enum ApplicationType
    {
        /// <summary>
        /// The application type couldn't be determined.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// A windows console application.  Can also include windows services running in console mode.
        /// </summary>
        Console = 1,

        /// <summary>
        /// A Windows Smart Client application (a traditional windows application)
        /// </summary>
        Windows = 2,

        /// <summary>
        /// A Windows Service application.
        /// </summary>
        Service = 3,

        /// <summary>
        /// A Web Application running in the ASP.NET framework.
        /// </summary>
        AspNet = 4,
    }
}
