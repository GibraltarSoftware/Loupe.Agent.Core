namespace Gibraltar.Monitor.Internal
{
    /// <summary>
    /// Provides lookup services for packet factories to find other session-related packets
    /// </summary>
    /// <remarks>Implemented by the session object and the network viewer client</remarks>
    internal interface ISessionPacketCache
    {
        /// <summary>
        /// The set of threads in the current session
        /// </summary>
        ThreadInfoCollection Threads { get; }

        ApplicationUserCollection Users { get; }
    }
}
