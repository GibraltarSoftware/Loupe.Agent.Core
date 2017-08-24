namespace Gibraltar.Agent
{
    /// <summary>
    /// The current known disposition of the session
    /// </summary>
    public enum SessionStatus
    {
        /// <summary>
        /// The final status of the session isn't known
        /// </summary>
        Unknown = Loupe.Extensibility.Data.SessionStatus.Unknown,

        /// <summary>
        /// The application is still running
        /// </summary>
        Running = Loupe.Extensibility.Data.SessionStatus.Running,

        /// <summary>
        /// The application closed normally
        /// </summary>
        Normal = Loupe.Extensibility.Data.SessionStatus.Normal,
        
        /// <summary>
        /// The application closed unexpectedly
        /// </summary>
        Crashed = Loupe.Extensibility.Data.SessionStatus.Crashed,
    }
}
