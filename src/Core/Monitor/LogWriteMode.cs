namespace Gibraltar.Monitor
{
    /// <summary>
    /// Selects the performance trade-off for writing a given message to the log.
    /// </summary>
    /// <remarks><para>This enum selects the preferred trade-off between run-time performance and diagnostic data
    /// persistence for an individual log message being issued.  By default (in API calls which do not take an argument
    /// of this type) Gibraltar uses LogWriteMode.Queued under normal conditions but will automatically enforce
    /// a wait-for-commit mode under certain abnormal conditions or during normal application exit.  The
    /// LogWriteMode.WaitForCommit setting allows the issuer of a log message to specifically request that the
    /// call not return until the log message has been committed to disk, instead of the normal queue-and-return
    /// behavior.</para>
    /// <para>Queued<br/>
    /// This setting indicates that the caller prefers to continue execution as soon as possible
    /// and only cares that the message be placed on the queue to be eventually written into the log file
    /// on disk.  Messages written with this mode could be lost if the application crashes and Gibraltar
    /// is not able to flush its queue to the log file on disk.  Under certain unusual conditions and
    /// during normal application exit, even calls with this explicit setting may be forced to behave as
    /// WaitForCommit to provide better logging integrity in those scenarios.
    /// </para>
    /// <para>WaitForCommit<br/>
    /// This setting indicates that the caller needs to make sure this message makes it into the
    /// log file on disk because the application is exiting or may crash when it continues.  The call will
    /// force a flush of the queue and block until Gibraltar has committed the message to the log file on
    /// disk.  Messages written with this mode will not be lost--IF the call completes and returns--but
    /// will incur a significant performance hit, so it should generally only be used for critical information
    /// which needs to survive a crash scenario to help diagnose the cause of the crash.
    /// </para>
    /// <para>WaitForCommit mode still can not guarantee that the log message will make it to disk in pathological cases,
    /// but it designates this as a higher priority than performance for an individual log message for scenarios
    /// where the persistence of this information is expected to be important in diagnosing a condition which
    /// might prevent normal Queued messages from making it to disk--for example, when the caller will be causing
    /// the application to exit abruptly upon return and needs the log to reflect the reason why.</para></remarks>
    public enum LogWriteMode
    {
        /// <summary>
        /// Return after placing the message on the queue. (Normal operation)
        /// </summary>
        Queued,

        /// <summary>
        /// Don't return until the message has been committed to disk.
        /// </summary>
        WaitForCommit,
    }
}
