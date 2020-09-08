
using System;
using Gibraltar.Monitor.Serialization;
using Loupe.Extensibility.Data;


namespace Gibraltar.Messaging
{
    /// <summary>
    /// EventArgs for LogMessage notify events.
    /// </summary>
    internal class LogMessageNotifyEventArgs : EventArgs
    {
        /// <summary>
        /// The ILogMessage being notified.
        /// </summary>
        internal readonly LogMessagePacket Message;

        internal LogMessageNotifyEventArgs(LogMessagePacket message)
        {
            Message = message;
        }
    }
}
