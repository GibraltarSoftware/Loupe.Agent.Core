namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// The current known disposition of the session
    /// </summary>
    public enum SessionStatus
    {
        /// <summary>
        /// The final status of the session isn't known
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The application is still running
        /// </summary>
        Running = 1,

        /// <summary>
        /// The application closed normally
        /// </summary>
        Normal = 2,

        /// <summary>
        /// The application closed unexpectedly
        /// </summary>
        Crashed =3,
    }
}
