using System;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// The event arguments for the connection state changed event
    /// </summary>
    public class ChannelConnectionStateChangedEventArgs : EventArgs
    {
        internal ChannelConnectionStateChangedEventArgs(ChannelConnectionState state)
        {
            State = state;
        }

        /// <summary>
        /// The current connection state
        /// </summary>
        public ChannelConnectionState State { get; private set; }
    }


    /// <summary>
    /// Event handler for the connection state changed event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void ChannelConnectionStateChangedEventHandler(object sender, ChannelConnectionStateChangedEventArgs e);
}
