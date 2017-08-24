using System;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Event arguments for tracking the state of a web request
    /// </summary>
    public class WebRequestEventArgs : EventArgs
    {
        internal WebRequestEventArgs(WebRequestState state)
        {
            State = state;
        }

        /// <summary>
        /// The state of the web request when the event was raised.
        /// </summary>
        public WebRequestState State { get; private set; }
    }

    /// <summary>
    /// The state of a web request
    /// </summary>
    public enum WebRequestState
    {
        /// <summary>
        /// Not yet processed
        /// </summary>
        New = 0,

        /// <summary>
        /// Completed successfully.
        /// </summary>
        Completed = 1,

        /// <summary>
        /// Canceled before it could be completed
        /// </summary>
        Canceled = 2,

        /// <summary>
        /// Attempted but generated an error.
        /// </summary>
        Error = 3
    }

    /// <summary>
    /// Delegate definition for a web request event.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void WebRequestEventHandler(object sender, EventArgs e);
}
