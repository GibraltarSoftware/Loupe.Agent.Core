using System;
using Gibraltar.Data;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Event arguments for session header changes
    /// </summary>
    public class SessionHeaderEventArgs: EventArgs
    {
        /// <summary>
        /// Create a new session header event arguments object
        /// </summary>
        /// <param name="header"></param>
        public SessionHeaderEventArgs(SessionHeader header)
        {
            SessionHeader = header;
        }

        /// <summary>
        /// The session header that was affected
        /// </summary>
        public SessionHeader SessionHeader { get; private set; }
    }

    /// <summary>
    /// Delegate for handling session header events
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void SessionHeaderEventHandler(object sender, SessionHeaderEventArgs e);
}
