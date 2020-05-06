using System;
using System.Text;

namespace Loupe.Configuration
{
    /// <summary>
    /// File Messenger Configuration
    /// </summary>
    public sealed class SessionFileConfiguration : IMessengerConfiguration
    {
        /// <summary>
        /// Initialize the session file configuration from the application configuration
        /// </summary>
        public SessionFileConfiguration()
        {
            Enabled = true;
            AutoFlushInterval = 15;
            IndexUpdateInterval = 15;
            MaxFileSize = 20;
            MaxFileDuration = 1440;
            EnableFilePruning = true;
            MaxLocalDiskUsage = 150;
            MaxLocalFileAge = 90;
            MinimumFreeDisk = 200;
            ForceSynchronous = false;
            MaxQueueLength = 2000;
        }

        /// <summary>
        /// The maximum number of seconds data can be held before it is flushed.
        /// </summary>
        /// <remarks>In addition to the default automatic flush due to the amount of information waiting to be written out 
        /// the messenger will automatically flush to disk based on the number of seconds specified.</remarks>
        public int AutoFlushInterval { get; set; }

        /// <summary>
        /// The folder to store session files in unless explicitly overridden at runtime.
        /// </summary>
        /// <remarks>If null or empty, files will be stored in a central local application data folder which is the preferred setting.</remarks>
        public string Folder { get; set; }

        /// <summary>
        /// The number of seconds between index updates.
        /// </summary>
        /// <remarks>An index is maintained of session information including the number and types
        /// of messages and session status.  It is updated automatically when a session is stopped
        /// and in some other situations.</remarks>
        public int IndexUpdateInterval { get; set; }

        /// <summary>
        /// The maximum number of megabytes in a single session file before a new file is started.
        /// </summary>
        /// <remarks>When the file reaches the maximum size it will be closed and a new file started. 
        /// Due to compression effects and other data storage considerations, final files may end up slightly 
        /// larger on disk or somewhat smaller.  Setting to zero will allow files to grow to the maximum
        /// size allowed by the file format (2 GB)</remarks>
        public int MaxFileSize { get; set; }

        /// <summary>
        /// The maximum number of minutes in a single session file before a new file is started.
        /// </summary>
        /// <remarks>When the file reaches the maximum age it will be closed and a new file started.  Setting to zero
        /// will allow the file to cover an unlimited period of time.</remarks>
        public int MaxFileDuration { get; set; }

        /// <summary>
        /// When true, session files will be pruned for size or age.
        /// </summary>
        /// <remarks>By default session files older than a specified number of days are automatically
        /// deleted and the oldest files are removed when the total storage of all files for the same application
        /// exceeds a certain value.  Setting this option to false will disable pruning.</remarks>
        public bool EnableFilePruning { get; set; }

        /// <summary>
        /// The maximum number of megabytes for all log files in megabytes on the local drive before older files are purged.
        /// </summary>
        /// <remarks><para>When the maximum local disk usage is approached, files are purged by selecting the oldest files first.
        /// This limit may be exceeded temporarily by the maximum log size because the active file will not be purged.
        /// Size is specified in megabytes.</para>
        /// <para>Setting to any integer less than 1 will disable pruning by disk usage.</para></remarks>
        public int MaxLocalDiskUsage { get; set; }

        /// <summary>
        /// The number of days that log files are retained.
        /// </summary>
        /// <remarks>
        ///   <para>Log files that were collected longer than the retention interval ago will be removed regardless of space constraints.</para>
        ///   <para>Setting to any integer less than 1 will disable pruning by age.</para>
        /// </remarks>
        public int MaxLocalFileAge { get; set; }

        /// <summary>
        /// The minimum amount of free disk space for logging.
        /// </summary>
        /// <remarks>If the amount of free disk space falls below this value, existing log files will be removed to free space.
        /// If no more log files are available, logging will stop until adequate space is freed.</remarks>
        public int MinimumFreeDisk { get; set; }

        /// <summary>
        /// When true, the session file will treat all write requests as write-through requests.
        /// </summary>
        /// <remarks>This overrides the write through request flag for all published requests, acting
        /// as if they are set true.  This will slow down logging and change the degree of parallelism of 
        /// multithreaded applications since each log message will block until it is committed.</remarks>
        public bool ForceSynchronous { get; set; }

        /// <summary>
        /// The maximum number of queued messages waiting to be processed by the session file
        /// </summary>
        /// <remarks>Once the total number of messages waiting to be processed exceeds the
        /// maximum queue length the session file will switch to a synchronous mode to 
        /// catch up.  This will not cause the application to experience synchronous logging
        /// behavior unless the publisher queue is also filled.</remarks>
        public int MaxQueueLength { get; set; }

        /// <summary>
        /// When false, the session file is disabled even if otherwise configured.
        /// </summary>
        /// <remarks>This allows for explicit disable/enable without removing the existing configuration
        /// or worrying about the default configuration.</remarks>
        public bool Enabled { get; set; }

        /// <summary>
        /// Normalize the configuration
        /// </summary>
        public void Sanitize()
        {
            if (string.IsNullOrEmpty(Folder))
                Folder = null;

            if (AutoFlushInterval <= 0)
                AutoFlushInterval = 15;

            if (IndexUpdateInterval <= 0)
                IndexUpdateInterval = 15;

            if (MaxFileDuration < 1)
                MaxFileDuration = 1576800; //three years, treated as infinite because really - is a process going to run longer than that?

            if (MaxFileSize <= 0)
                MaxFileSize = 1024; //1GB override when set to zero.

            if (MaxLocalDiskUsage <= 0)
            {
                MaxLocalDiskUsage = 0; //we intelligently disable at this point
            }
            else
            {
                //make sure our max file size can fit within our max local disk usage
                if (MaxLocalDiskUsage < MaxFileSize)
                    MaxFileSize = MaxLocalDiskUsage;
            }

            if (MaxLocalFileAge <= 0)
                MaxLocalFileAge = 0; //we intelligently disable at this point

            if (MinimumFreeDisk <= 0)
                MinimumFreeDisk = 50;

            if (MaxQueueLength <= 0)
                MaxQueueLength = 2000;
            else if (MaxQueueLength > 50000)
                MaxQueueLength = 50000;
        }

        /// <inheritdoc />
        string IMessengerConfiguration.MessengerTypeName => "Gibraltar.Messaging.FileMessenger";

        /// <inheritdoc />
        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat("\tEnabled: {0}\r\n", Enabled);
            stringBuilder.AppendFormat("\tAuto Flush Interval: {0}\r\n", AutoFlushInterval);
            stringBuilder.AppendFormat("\tIndex Update Interval: {0}\r\n", IndexUpdateInterval);
            stringBuilder.AppendFormat("\tMax File Size: {0}\r\n", MaxFileSize);
            stringBuilder.AppendFormat("\tMax File Duration: {0}\r\n", MaxFileDuration);
            stringBuilder.AppendFormat("\tEnable File Pruning: {0}\r\n", EnableFilePruning);
            stringBuilder.AppendFormat("\tMax Local Disk Usage: {0}\r\n", MaxLocalDiskUsage);
            stringBuilder.AppendFormat("\tMax Local File Age: {0}\r\n", MaxLocalFileAge);
            stringBuilder.AppendFormat("\tMinimum Free Disk: {0}\r\n", MinimumFreeDisk);
            stringBuilder.AppendFormat("\tForce Synchronous: {0}\r\n", ForceSynchronous);
            stringBuilder.AppendFormat("\tMax Queued Length: {0}\r\n", MaxQueueLength);

            return stringBuilder.ToString();
        }
    }
}
