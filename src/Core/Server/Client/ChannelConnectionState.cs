namespace Gibraltar.Server.Client
{
    /// <summary>
    /// The status of the subscription connection
    /// </summary>
    public enum ChannelConnectionState
    {
        /// <summary>
        /// The subscription is disconnected
        /// </summary>
        Disconnected = 0,

        /// <summary>
        /// The subscription is attempting to connect
        /// </summary>
        Connecting = 1,

        /// <summary>
        /// The subscription is connected.
        /// </summary>
        Connected = 2,

        /// <summary>
        /// The subscription is actively transferring data
        /// </summary>
        TransferingData = 3
    }
}
