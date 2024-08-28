namespace Gibraltar.Data
{
    /// <summary>
    /// The different path types that Gibraltar uses
    /// </summary>
    public enum PathType
    {
        /// <summary>
        /// The place for the agent to record new session information
        /// </summary>
        Collection = 0,

        /// <summary>
        /// The session repository for sessions the user wants to keep
        /// </summary>
        Repository = 1,

        /// <summary>
        /// The Licensing folder for activation keys and secure date store
        /// </summary>
        Licensing = 2,

        /// <summary>
        /// The shared configuration folder for this computer.
        /// </summary>
        Configuration = 3,

        /// <summary>
        /// The shared folder for Extensions on this computer.
        /// </summary>
        Extensions = 4,

        /// <summary>
        /// The shared folder for inter-agent discovery (like for live sessions)
        /// </summary>
        Discovery = 5,

        /// <summary>
        /// A session repository that's a server cache.
        /// </summary>
        ServerRepository = 6
    }
}