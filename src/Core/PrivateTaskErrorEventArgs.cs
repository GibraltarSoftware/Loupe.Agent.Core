using System;

namespace Loupe
{
    /// <summary>
    /// Event arguments for the TaskException event 
    /// </summary>
    public class PrivateTaskErrorEventArgs: EventArgs
    {
        /// <summary>
        /// Create a new event argument object for the provided task information
        /// </summary>
        /// <param name="state"></param>
        /// <param name="ex"></param>
        public PrivateTaskErrorEventArgs(object state, Exception ex)
        {
            State = state;
            Exception = ex;
        }

        /// <summary>
        /// The state object provided for the task (if any)
        /// </summary>
        public object State { get; private set; }

        /// <summary>
        /// The exception that was generated.
        /// </summary>
        public Exception Exception { get; private set; }
    }

    /// <summary>
    /// Event handler for using the Private Task Error event args.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="e"></param>
    public delegate void PrivateTaskErrorEventHandler(object state, PrivateTaskErrorEventArgs e);
}
