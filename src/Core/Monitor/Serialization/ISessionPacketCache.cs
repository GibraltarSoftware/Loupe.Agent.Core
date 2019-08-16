namespace Gibraltar.Monitor.Serialization
{
    /// <summary>
    /// Provides lookup services for packet factories to find other session-related packets
    /// </summary>
    /// <remarks>Implemented by the session object and the network viewer client</remarks>
    public interface ISessionPacketCache
    {
        /// <summary>
        /// The set of threads in the current session
        /// </summary>
        ThreadInfoCollection Threads { get; }

        /// <summary>
        /// The set of application users for the current session.
        /// </summary>
        ApplicationUserCollection Users { get; }
    }
}
