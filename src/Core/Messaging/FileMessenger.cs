using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Gibraltar.Data;
using Gibraltar.Data.Internal;
using Gibraltar.Monitor;
using Gibraltar.Monitor.Serialization;
using Loupe.Configuration;
using Loupe.Extensibility.Data;

namespace Gibraltar.Messaging
{
    internal class FileMessenger : MessengerBase
    {
        private new const string LogCategory = MessengerBase.LogCategory + ".File Messenger";

        public const string LogExtension = "glf";
        public const string PackageExtension = "glp";
        public const string SessionLockFolderName = "runningsessions";

        private static readonly Random m_RandomGenerator = new Random(); //static is important so multiple instances created close together get different values

        private string m_RepositoryFolder;
        private string m_SessionLockFolder;
        private int m_CurrentSessionFile;
        private DateTime m_FileExpiration;

        private FileStream m_CurrentFile;
        private GLFWriter m_CurrentSerializer;
        private RepositoryMaintenance m_Maintainer;
        private InterprocessLock m_SessionFileLock;

        private int m_MaxLocalDiskUsage;
        private int m_MaxLocalFileAge;
        private long m_MaxFileSizeBytes;
        private long m_MaxLogDurationSeconds;
        private bool m_RepositoryMaintenanceEnabled;
        private bool m_RepositoryMaintenanceRequested;
        private DateTimeOffset m_RepositoryMaintenanceScheduledDateTime;  //once maintenance has been requested, when we will do it.

        #region Public Properties and Methods

        public FileMessenger() 
            : base("File", true)
        {
            
        }

        /// <summary>
        /// Creates the appropriate start of a session file name for a product/application
        /// </summary>
        /// <param name="productName"></param>
        /// <param name="applicationName"></param>
        /// <returns></returns>
        public static string SessionFileNamePrefix(string productName, string applicationName)
        {
            return FileSystemTools.SanitizeFileName(string.Format(CultureInfo.InvariantCulture, "{0}_{1}", productName, applicationName), true);
        }

        #endregion

        #region Base Object Overrides

        /// <summary>
        /// Inheritors should override this method to implement custom Command handling functionality
        /// </summary>
        /// <remarks>Code in this method is protected by a Queue Lock.
        /// This method is called with the Message Dispatch thread exclusively.
        /// Some commands (Shutdown, Flush) are handled by MessengerBase and redirected into specific
        /// method calls.</remarks>
        /// <param name="command">The MessagingCommand enum value of this command.</param>
        /// <param name="state"></param>
        /// <param name="writeThrough">Whether write-through (synchronous) behavior was requested.</param>
        /// <param name="maintenanceRequested">Specifies whether maintenance mode has been requested and the type (source) of that request.</param>
        protected override void OnCommand(MessagingCommand command, object state, bool writeThrough, ref MaintenanceModeRequest maintenanceRequested)
        {
            if (command == MessagingCommand.CloseFile)
            {
                // This command is for us!  It means issue maintenance mode to close and roll over to a new file.
                maintenanceRequested = MaintenanceModeRequest.Explicit;
            }
        }

        /// <summary>
        /// Inheritors should override this method to implement custom initialize functionality.
        /// </summary>
        /// <remarks>This method will be called exactly once before any call to OnFlush or OnWrite is made.  
        /// Code in this method is protected by a Thread Lock.
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected override void OnInitialize(IMessengerConfiguration configuration)
        {
            //do our first time initialization
            Caption = "Standard File Messenger";
            Description = "Messenger implementation that writes messages to files through a buffer.  Supports synchronous and asynchronous messaging.";

            //try to up cast the configuration to our specific configuration type
            var fileConfiguration = (SessionFileConfiguration)configuration;

            //If the max file size is unbounded (zero or less) then we want 1GB.
            m_MaxFileSizeBytes = fileConfiguration.MaxFileSize < 1 ? 1024 : fileConfiguration.MaxFileSize;
            m_MaxFileSizeBytes = m_MaxFileSizeBytes * 1048576; //the configured value is in MB, we use bytes for faster comparisons

            m_MaxLogDurationSeconds = Math.Max(fileConfiguration.MaxFileDuration, 1) * 60;  //the configured value is in minutes, we use seconds for consistency

            m_RepositoryMaintenanceEnabled = fileConfiguration.EnableFilePruning;
            m_MaxLocalDiskUsage = fileConfiguration.MaxLocalDiskUsage;
            m_MaxLocalFileAge = fileConfiguration.MaxLocalFileAge;

            //what are the very best folders for us to use?
            m_RepositoryFolder = LocalRepository.CalculateRepositoryPath(Publisher.SessionSummary.Product, fileConfiguration.Folder);
            m_SessionLockFolder = Path.Combine(m_RepositoryFolder, SessionLockFolderName);

            //we also have to be sure the path exists now.
            FileSystemTools.EnsurePathExists(m_RepositoryFolder);
            FileSystemTools.EnsurePathExists(m_SessionLockFolder);

            //Since we update the index during a flush, and the index update is about as bad as a flush we look at both together.
            AutoFlush = true;
            AutoFlushInterval = Math.Min(fileConfiguration.AutoFlushInterval, fileConfiguration.IndexUpdateInterval);

            //If we aren't able to initialize our log folder, throw an exception
            if (string.IsNullOrEmpty(m_RepositoryFolder))
            {
                throw new DirectoryNotFoundException("No log folder could be determined, so the file messenger can't log.");
            }

            ScheduleRepositoryMaintenance(0, 0);

            GetSessionFileLock();
        }

        /// <summary>
        /// Inheritors must override this method to implement their custom message writing functionality.
        /// </summary>
        /// <remarks>Code in this method is protected by a Queue Lock
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected override void OnWrite(IMessengerPacket packet, bool writeThrough, ref MaintenanceModeRequest maintenanceRequested)
        {
            //Do we have a serializer opened?
            if (m_CurrentSerializer == null)
            {
                //we do not.  we need to open a file.
                OpenFile();
            }

            //now write to the file
            m_CurrentSerializer.Write(packet);

            if (writeThrough)
                OnFlush(ref maintenanceRequested);

            //and do we need to request maintenance?
            if (m_CurrentFile.Length > m_MaxFileSizeBytes)
            {
                maintenanceRequested = MaintenanceModeRequest.Regular;
            }
            else if (DateTime.Now > m_FileExpiration)
            {
                maintenanceRequested = MaintenanceModeRequest.Regular;
            }
        }

        /// <summary>
        /// Inheritors should override this method to implement custom Exit functionality
        /// </summary>
        /// <remarks>Code in this method is protected by a Queue Lock.
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected override void OnExit()
        {
            //we want to switch into the appropriate exit mode; we don't want to leave it running now
            //even if we close abruptly.
            if (m_CurrentSerializer != null)
            {
                m_CurrentSerializer.SessionHeader.StatusName = m_CurrentSerializer.SessionHeader.ToString();
            }
        }

        /// <summary>
        /// Inheritors should override this method to implement custom flush functionality.
        /// </summary>
        /// <remarks>Code in this method is protected by a Queue Lock.        
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected override void OnFlush(ref MaintenanceModeRequest maintenanceRequested)
        {
            //push the serializer to flush to disk
            if (m_CurrentSerializer != null)
            {
                // The order of these two operations is related in a non-obvious way:  flushing the current
                // serializer updates the session header we write to the index, so it must be done first.
                m_CurrentSerializer.Flush();
            }

            //do we need to request maintenance?
            //This is duplicated from the OnWrite so we can trigger roll over *even when there are no messages*
            if (m_CurrentFile.Length > m_MaxFileSizeBytes)
            {
                maintenanceRequested = MaintenanceModeRequest.Regular;
            }
            else if (DateTime.Now > m_FileExpiration)
            {
                maintenanceRequested = MaintenanceModeRequest.Regular;
            }

            //and do repository maintenance if it was requested.  It won't be requested if maintenance is disabled.
            //This is over here to ensure we DO it eventually but we can do it on a lazy schedule, 
            //and don't bother if we're Exiting (includes closing) or it's dangerous (we're in a debugger)
            if ((m_RepositoryMaintenanceRequested 
                && (Exiting == false))
                && (DateTimeOffset.Now > m_RepositoryMaintenanceScheduledDateTime))
            {
                m_RepositoryMaintenanceRequested = false;

                //do we actually have a maintainer?  If not create it now.
                if (m_Maintainer == null)
                {
                    //initialize the repository maintenance object with our configuration.
                    try
                    {
                        m_Maintainer = new RepositoryMaintenance(m_RepositoryFolder, Publisher.SessionSummary.Product, Publisher.SessionSummary.Application,
                                                                 m_MaxLocalFileAge, m_MaxLocalDiskUsage, !Log.SilentMode);
                    }
                    catch (Exception ex)
                    {
                        GC.KeepAlive(ex);
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Unable to initialize repository maintenance", "While attempting to initialize the repository maintenance class in the file messenger an exception was thrown:\r\n{0}", ex.Message);
#if DEBUG
                        Log.DebugBreak();
#endif
                    }                    
                }

                //and only continue if we did create a good maintainer.
                if (m_Maintainer != null)
                {
                    m_Maintainer.PerformMaintenance(true);
                }
            }
        }

        /// <summary>
        /// Inheritors should override this to implement a periodic maintenance capability
        /// </summary>
        /// <remarks>Maintenance is invoked by a return value from the OnWrite method.  When invoked,
        /// this method is called and all log messages are buffered for the duration of the maintenance period.
        /// Once this method completes, normal log writing will resume.  During maintenance, any queue size limit is ignored.
        /// This method is not called with any active locks to allow messages to continue to queue during maintenance.  
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected override void OnMaintenance()
        {
            //close the existing file and open a new one. We rely on OpenFile doing both.
            OpenFile();

            //and if repository maintenance is enabled, kick that off as well.
            ScheduleRepositoryMaintenance(0, 0);
        }

        /// <summary>
        /// Inheritors should override this method to implement custom Close functionality
        /// </summary>
        /// <remarks>Code in this method is protected by a Queue Lock.
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected override void OnClose()
        {
            CloseFile(true); //closes the file and serializer safely
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Get the unique lock for the active session, to be held until the session exits.
        /// </summary>
        private void GetSessionFileLock()
        {
            if (m_SessionFileLock != null)
                return;

            var sessionId = Log.SessionSummary.Id;
            var sessionLockName = sessionId.ToString();

            try
            {
                m_SessionFileLock = InterprocessLockManager.Lock(this, m_SessionLockFolder, sessionLockName, 0, true);

                if (m_SessionFileLock == null)
                {
                    Log.Write(LogMessageSeverity.Information, LogCategory, "Loupe Agent unable to get the unique lock for a new session",
                        "The Loupe Agent's FileMessenger was not able to lock this active session as Running.  " +
                        "This could interfere with efficiently distinguishing whether this session has crashed or is still running.");
#if DEBUG
                    if (Debugger.IsAttached)
                        Debugger.Break();
#endif
                }
            }
            catch (Exception ex) //we don't want failure to get the session file lock to be fatal...
            {
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Information, LogWriteMode.Queued, ex, true, LogCategory, 
                        "Loupe Agent unable to get the unique lock for a new session due to " + ex.GetBaseException().GetType(),
                        "The Loupe Agent's FileMessenger was not able to lock this active session as Running.  " +
                        "This could interfere with efficiently distinguishing whether this session has crashed or is still running.");
            }
        }

        /// <summary>
        /// Release the unique lock for the active session, to be called when the FileMessenger gets disposed.
        /// </summary>
        private void ReleaseSessionFileLock()
        {
            if (m_SessionFileLock == null)
                return;

            try
            {
                m_SessionFileLock.Dispose();
                m_SessionFileLock = null;
            }
            catch (Exception ex)
            {
                Log.Write(LogMessageSeverity.Information, LogWriteMode.Queued, ex, LogCategory, "Loupe Agent got an error while releasing the unique lock for this session",
                    "The Loupe Agent's FileMessenger was not able to properly release the lock as this session exits.  "+
                    "This will likely take care of itself as the process exits, but an exception here is unexpected and unusual.");

                if (m_SessionFileLock != null && m_SessionFileLock.IsDisposed)
                    m_SessionFileLock = null;
            }
        }

        /// <summary>
        /// Schedule repository maintenance to happen at the next opportunity if maintenance is enabled.
        /// </summary>
        private void ScheduleRepositoryMaintenance(int minDelaySec, int maxDelaySec)
        {
            if (m_RepositoryMaintenanceEnabled == false || Exiting)
                return; //nothing to do

            int repositoryMaintenanceDelay = (maxDelaySec == 0) ? 0 : m_RandomGenerator.Next(minDelaySec, maxDelaySec);
            
            //now we have to make sure we don't move out the current maintenance time if it's already set.
            DateTimeOffset proposedMaintenanceTime = DateTimeOffset.Now.AddSeconds(repositoryMaintenanceDelay);
            if (m_RepositoryMaintenanceRequested)
            {
                m_RepositoryMaintenanceScheduledDateTime = (proposedMaintenanceTime < m_RepositoryMaintenanceScheduledDateTime) 
                    ? proposedMaintenanceTime : m_RepositoryMaintenanceScheduledDateTime;
            }
            else
            {
                m_RepositoryMaintenanceScheduledDateTime = proposedMaintenanceTime;
                m_RepositoryMaintenanceRequested = true;
            }
        }

        private void CloseFile(bool isLastFile)
        {
            //close any existing serializer
            if (m_CurrentSerializer != null)
            {
                try
                {
                    // The order of these two operations is related in a non-obvious way:  closing the current
                    // serializer updates the session header we write to the index, so it must be done first.
                    m_CurrentSerializer.Close(isLastFile);

                    // Now update our index information with the final session header info.
                    if (isLastFile == false)
                    {
                        //we need to keep our state as being running, Close just changed it.
                        m_CurrentSerializer.SessionHeader.StatusName = SessionStatus.Running.ToString();
                    }
                }
                finally
                {
                    m_CurrentSerializer = null;    
                
                    //and pack the string reference list since we dumped the unique string list, which may be holding a lot of strings.
                    GC.Collect();
                    StringReference.Pack(); //We used to wait for pending finalizers but that turns out to be bad if a UI Thread is waiting on us.
                }
            }

            //close any existing file stream
            if (m_CurrentFile != null)
            {
                try
                {
                    m_CurrentFile.Flush();
                }
                finally
                {
                    m_CurrentFile.Dispose();
                    m_CurrentFile = null;
                }
            }

            // And if it's the last file, release our unique lock for this session.
            if (isLastFile)
                ReleaseSessionFileLock();
        }

        /// <summary>
        /// Open a new output file.
        /// </summary>
        /// <remarks>Any existing file will be closed.</remarks>
        private void OpenFile()
        {
            //clear the existing file pointer to make sure if we fail, it's gone.
            //we also rely on this to distinguish adding a new file to an existing stream.
            CloseFile(false);

            //increment our session file counter since we're going to open a new file
            m_CurrentSessionFile++;

            //Calculate our candidate file name (with path) based on what we know.
            string fileNamePath = Path.Combine(m_RepositoryFolder, MakeFileName());

            //now double check that the candidate path is unique
            fileNamePath = FileSystemTools.MakeFileNamePathUnique(fileNamePath);

            //we now have a unique file name, create the file.
            FileSystemTools.EnsurePathExists(fileNamePath);
            m_CurrentFile = new FileStream(fileNamePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

            //and open a serializer on it
            m_CurrentSerializer = new GLFWriter(m_CurrentFile, Publisher.SessionSummary, m_CurrentSessionFile, DateTimeOffset.Now);

            //write out every header packet to the stream
            ICachedMessengerPacket[] headerPackets = Publisher.HeaderPackets;
            if (headerPackets != null)
            {
                foreach (ICachedMessengerPacket packet in headerPackets)
                {
                    m_CurrentSerializer.Write(packet);
                }
            }

            //and set a time for us to do our next index update.
            m_FileExpiration = DateTime.Now.AddSeconds(m_MaxLogDurationSeconds);
        }

        private string MakeFileName()
        {
            string fileName = string.Format(CultureInfo.InvariantCulture, "{0}_{1:yyyy-MM-dd-HH-mm-ss}-{2}.{3}", SessionFileNamePrefix(Publisher.SessionSummary.Product, Publisher.SessionSummary.Application), 
                Publisher.SessionSummary.StartDateTime.UtcDateTime, m_CurrentSessionFile, LogExtension);

            return FileSystemTools.SanitizeFileName(fileName, true);
        }

#endregion
    }
}
