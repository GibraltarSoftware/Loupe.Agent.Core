namespace Loupe.Messaging
{
    /// <summary>
    /// Different types of commands.
    /// </summary>
    internal enum MessagingCommand
    {
        /// <summary>
        /// Not a command.
        /// </summary>
        None = 0,

        /// <summary>
        /// Flush the queue
        /// </summary>
        Flush = 1,

        /// <summary>
        /// Close the current file (and open a new one because the session isn't ending)
        /// </summary>
        CloseFile = 2,

        /// <summary>
        /// Alert the messaging system to make preparations for the application exiting.
        /// </summary>
        ExitMode = 3,

        /// <summary>
        /// Close the messenger (and don't restart it)
        /// </summary>
        CloseMessenger = 4,

        /// <summary>
        /// Cause the Loupe Live View form to be (generated if necessary and) shown.
        /// </summary>
        ShowLiveView = 5,

        /// <summary>
        /// Causes the network messenger to connect out to a remote viewer
        /// </summary>
        OpenRemoteViewer = 6
    }
}
