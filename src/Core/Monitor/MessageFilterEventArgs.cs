
using System;
using Loupe.Extensibility.Data;



namespace Gibraltar.Monitor
{
    /// <summary>
    /// EventArgs for Message Filter events.
    /// </summary>
    public class MessageFilterEventArgs : EventArgs
    {
        /// <summary>
        /// A new log message received for possible display by the (LiveLogViewer) sender of this event.
        /// </summary>
        public readonly ILogMessage Message;

        /// <summary>
        /// Cancel (block) this message from being displayed to users by the (LiveLogViewer) sender of this event.
        /// </summary>
        public bool Cancel;

        internal MessageFilterEventArgs(ILogMessage message)
        {
            Message = message;
            Cancel = false;
        }
    }
}
