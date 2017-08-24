namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// The current analysis of the session.
    /// </summary>
    public interface ISessionAnalysis
    {
        /// <summary>
        /// The session this analysis applies to
        /// </summary>
        ISession Session { get; }

        /// <summary>
        /// The set of log events present in the data being analyzed
        /// </summary>
        /// <remarks>For sessions with multiple fragments this will just be the log event
        /// information from the current data being analyzed which may be only part of the 
        /// total data for the session.</remarks>
        ILogEventCollection LogEvents { get; }

        /// <summary>
        /// The set of users for this session referenced in the current analysis
        /// </summary>
        IApplicationUserCollection Users { get; } 

        /// <summary>
        /// Indicates if this is the first set of data being analyzed for this session.
        /// </summary>
        bool IsNewSession { get; }
    }
}
