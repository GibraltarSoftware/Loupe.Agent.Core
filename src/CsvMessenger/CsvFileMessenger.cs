using System;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Gibraltar;
using Gibraltar.Data;
using Gibraltar.Messaging;
using Gibraltar.Monitor;
using Gibraltar.Monitor.Serialization;
using Loupe.Configuration;
using Loupe.Extensibility.Data;

namespace Loupe.Agent.Messaging.Export
{
    internal class CsvFileMessenger : MessengerBase
    {
        internal const string LogExtension = "csv";

        private static readonly Random m_RandomGenerator = new Random(); //static is important so multiple instances created close together get different values

        private string m_RepositoryFolder;
        private int m_CurrentSessionFile;
        private DateTime m_FileExpiration;

        private StreamWriter m_CurrentFile;
        private CsvWriter m_CurrentCsvFile;
        private ExportFileMaintenance m_Maintainer;

        private int m_MaxLocalDiskUsage;
        private int m_MaxLocalFileAge;
        private long m_MaxFileSizeBytes;
        private long m_MaxLogDurationSeconds;
        private bool m_RepositoryMaintenanceEnabled;
        private bool m_RepositoryMaintenanceRequested;
        private DateTimeOffset m_RepositoryMaintenanceScheduledDateTime;  //once maintenance has been requested, when we will do it.

        public CsvFileMessenger()
            : base("CsvMessenger")
        {
        }

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
            Caption = "CSV File Messenger";
            Description = "Messenger implementation that exports messages to CSV files through a buffer.  Supports synchronous and asynchronous messaging.";

            //try to up cast the configuration to our specific configuration type
            var fileConfiguration = (ExportFileConfiguration)configuration;

            //If the max file size is unbounded (zero or less) then we want 1GB.
            m_MaxFileSizeBytes = fileConfiguration.MaxFileSize < 1 ? 1024 : fileConfiguration.MaxFileSize;
            m_MaxFileSizeBytes = m_MaxFileSizeBytes * 1048576; //the configured value is in MB, we use bytes for faster comparisons

            m_MaxLogDurationSeconds = fileConfiguration.MaxFileDuration * 60;  //the configured value is in minutes, we use seconds for consistency

            m_RepositoryMaintenanceEnabled = fileConfiguration.EnableFilePruning;
            m_MaxLocalDiskUsage = fileConfiguration.MaxLocalDiskUsage;
            m_MaxLocalFileAge = fileConfiguration.MaxLocalFileAge;

            //what are the very best folders for us to use?
            m_RepositoryFolder = Path.Combine(fileConfiguration.Folder,
                FileSystemTools.SanitizeDirectoryName(Publisher.SessionSummary.Product));

            m_RepositoryFolder = Path.Combine(m_RepositoryFolder,
                FileSystemTools.SanitizeDirectoryName(Publisher.SessionSummary.Application));

            //we also have to be sure the path exists now.
            FileSystemTools.EnsurePathExists(m_RepositoryFolder);

            //Since we update the index during a flush, and the index update is about as bad as a flush we look at both together.
            AutoFlush = true;
            AutoFlushInterval = fileConfiguration.AutoFlushInterval;

            //If we aren't able to initialize our log folder, throw an exception
            if (string.IsNullOrEmpty(m_RepositoryFolder))
            {
                throw new DirectoryNotFoundException("No log folder could be determined, so the file messenger can't log.");
            }

            ScheduleRepositoryMaintenance(0, 0);
        }

        /// <summary>
        /// Inheritors must override this method to implement their custom message writing functionality.
        /// </summary>
        /// <remarks>Code in this method is protected by a Queue Lock
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected override void OnWrite(IMessengerPacket packet, bool writeThrough, ref MaintenanceModeRequest maintenanceRequested)
        {
            var logMessage = packet as LogMessagePacket;
            if (ReferenceEquals(logMessage, null))
                return;

            //Do we have a serializer opened?
            if (m_CurrentCsvFile == null)
            {
                //we do not.  we need to open a file.
                OpenFile();
            }

            //now write to the file
            m_CurrentCsvFile.WriteField(logMessage.Timestamp.ToString("O"));
            m_CurrentCsvFile.WriteField(logMessage.Severity);
            m_CurrentCsvFile.WriteField(logMessage.Caption);
            m_CurrentCsvFile.WriteField(logMessage.Description);
            m_CurrentCsvFile.WriteField(logMessage.Details);
            m_CurrentCsvFile.WriteField(logMessage.CategoryName);
            m_CurrentCsvFile.WriteField(logMessage.UserName);
            m_CurrentCsvFile.WriteField(logMessage.MethodName);
            m_CurrentCsvFile.WriteField(logMessage.ClassName);
            m_CurrentCsvFile.WriteField(logMessage.FileName);
            m_CurrentCsvFile.WriteField(logMessage.LineNumber);
            m_CurrentCsvFile.WriteField(logMessage.ThreadId);

            //render the exceptions into a single field.
            m_CurrentCsvFile.WriteField(RenderException(logMessage.Exceptions));

            m_CurrentCsvFile.WriteField(logMessage.LogSystem);
            m_CurrentCsvFile.NextRecord();

            if (writeThrough)
                OnFlush(ref maintenanceRequested);

            //and do we need to request maintenance?
            if (m_CurrentFile.BaseStream.Length > m_MaxFileSizeBytes)
            {
                maintenanceRequested = MaintenanceModeRequest.Regular;
            }
            else if (DateTime.Now > m_FileExpiration)
            {
                maintenanceRequested = MaintenanceModeRequest.Regular;
            }
        }

        private string RenderException(IExceptionInfo[] exceptions)
        {
            if (exceptions == null || exceptions.Length == 0)
                return null;

            var stringBuilder = new StringBuilder();

            foreach (var exception in exceptions)
            {
                stringBuilder.AppendFormat("{0}: {1}\r\n", exception.TypeName, exception.Message);
                stringBuilder.AppendLine(exception.StackTrace);
            }

            return stringBuilder.ToString();
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
            if (m_CurrentFile != null)
            {
                m_CurrentFile.Flush();
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
            if (m_CurrentFile != null)
            {
                // The order of these two operations is related in a non-obvious way:  flushing the current
                // serializer updates the session header we write to the index, so it must be done first.
                m_CurrentFile.Flush();
            }

            //and do we need to request maintenance?
            //This is duplicated from the OnWrite so we can trigger roll over *even when there are no messages*
            if ((m_CurrentFile != null) && (m_CurrentFile.BaseStream.Length > m_MaxFileSizeBytes))
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
                        m_Maintainer = new ExportFileMaintenance(m_RepositoryFolder, Publisher.SessionSummary.Product, Publisher.SessionSummary.Application,
                                                                 m_MaxLocalFileAge, m_MaxLocalDiskUsage, !Log.SilentMode);
                    }
                    catch (Exception ex)
                    {
                        GC.KeepAlive(ex);
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, "Gibraltar.Repository.Maintenance", "Unable to initialize repository maintenance", "While attempting to initialize the repository maintenance class in the file messenger an exception was thrown:\r\n{0}", ex.Message);
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
            CloseFile(); //closes the file and serializer safely
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

        private void CloseFile()
        {
            //close any existing serializer
            if (m_CurrentCsvFile != null)
            {
                try
                {
                    m_CurrentCsvFile.Dispose();
                }
                finally
                {
                    m_CurrentCsvFile = null;
                    m_CurrentFile = null; //it is captively disclosed as part of the CSV.
                }
            }

            //close any existing file stream
            if (m_CurrentFile != null)
            {
                try
                {
                    m_CurrentFile.Close();
                }
                finally
                {
                    m_CurrentFile.Dispose();
                    m_CurrentFile = null;
                }
            }
        }

        /// <summary>
        /// Open a new output file.
        /// </summary>
        /// <remarks>Any existing file will be closed.</remarks>
        private void OpenFile()
        {
            //clear the existing file pointer to make sure if we fail, it's gone.
            //we also rely on this to distinguish adding a new file to an existing stream.
            CloseFile();

            //increment our session file counter since we're going to open a new file
            m_CurrentSessionFile++;

            //Calculate our candidate file name (with path) based on what we know.
            string fileNamePath = Path.Combine(m_RepositoryFolder, MakeFileName());

            //now double check that the candidate path is unique
            fileNamePath = FileSystemTools.MakeFileNamePathUnique(fileNamePath);

            //we now have a unique file name, create the file.
            FileSystemTools.EnsurePathExists(fileNamePath);
            var fileStream = new FileStream(fileNamePath, FileMode.CreateNew, FileAccess.Write);
            m_CurrentFile = new StreamWriter(fileStream, Encoding.UTF8);

            //and open a serializer on it
            var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                ShouldQuote = args => true //quote all
            };

            m_CurrentCsvFile = new CsvWriter(m_CurrentFile, configuration);

            //write out the file headers...
            m_CurrentCsvFile.WriteField("Timestamp");
            m_CurrentCsvFile.WriteField("Severity");
            m_CurrentCsvFile.WriteField("Caption");
            m_CurrentCsvFile.WriteField("Description");
            m_CurrentCsvFile.WriteField("Details");
            m_CurrentCsvFile.WriteField("CategoryName");
            m_CurrentCsvFile.WriteField("UserName");
            m_CurrentCsvFile.WriteField("MethodName");
            m_CurrentCsvFile.WriteField("ClassName");
            m_CurrentCsvFile.WriteField("FileName");
            m_CurrentCsvFile.WriteField("LineNumber");
            m_CurrentCsvFile.WriteField("ThreadId");
            m_CurrentCsvFile.WriteField("Exceptions");
            m_CurrentCsvFile.WriteField("LogSystem");
            m_CurrentCsvFile.NextRecord();

            //and set a time for us to do our next index update.
            m_FileExpiration = DateTime.Now.AddSeconds(m_MaxLogDurationSeconds);
        }

        private string MakeFileName()
        {
            string fileName = string.Format(CultureInfo.InvariantCulture, "{0} {1:yyyy-MM-dd-HH-mm-ss}-{2}.{3}", FileMessenger.SessionFileNamePrefix(Publisher.SessionSummary.Product, Publisher.SessionSummary.Application),
                Publisher.SessionSummary.StartDateTime.UtcDateTime, m_CurrentSessionFile, LogExtension);

            return FileSystemTools.SanitizeFileName(fileName);
        }
    }
}
