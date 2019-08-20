namespace Loupe.Data
{
    /// <summary>
    /// The different path types that Loupe uses
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
        /// The shared folder for inter-agent discovery (like for live sessions)
        /// </summary>
        Discovery = 5,
    }
}