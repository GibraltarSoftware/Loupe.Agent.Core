
using System;



namespace Gibraltar.Messaging
{
    /// <summary>
    /// EventArgs for LogMessage notify events.
    /// </summary>
    internal class LogMessageNotifyEventArgs : EventArgs
    {
        /// <summary>
        /// The IMessengerPacket for the log message being notified about.
        /// </summary>
        internal readonly IMessengerPacket Packet;

        internal LogMessageNotifyEventArgs(IMessengerPacket packet)
        {
            Packet = packet;
        }
    }
}
