using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Gibraltar.Data;
using Gibraltar.Data.Internal;
using Gibraltar.Monitor;
using Loupe.Extensibility.Data;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// A web channel request to upload a session file or full session stream
    /// </summary>
    internal class SessionUploadRequest : WebChannelRequestBase, IDisposable
    {

        private const string SessionTempFolder = "Session_Upload";

        private const int SinglePassCutoffBytes = 3000000; //about 3MB.   
        private const int DefaultSegmentSizeBytes = 1000000; //about 1MB.

        private bool m_Initialized;
        private bool m_PerformCleanup;
        private int m_BytesWritten;
        private string m_TempSessionProgressFileNamePath; //the transfer tracking file.
        private Stream m_SessionFileStream; //the actual file stream to be transfered.
        private string m_SessionFileHash;

        private InterprocessLock m_SessionTransportLock;
        private bool m_DeleteTemporaryFilesOnDispose;

        /// <summary>
        /// Create a new session upload request.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="repository"></param>
        /// <param name="sessionId"></param>
        /// <param name="fileId"></param>
        /// <param name="purgeSessionOnSuccess">Indicates if the session should be purged from the repository once it has been sent successfully.</param>
        public SessionUploadRequest(Guid clientId, LocalRepository repository, Guid sessionId, Guid? fileId, bool purgeSessionOnSuccess)
            :base(true, false)
        {
            ClientId = clientId;
            Repository = repository;
            SessionId = sessionId;
            FileId = fileId;
            PurgeSessionOnSuccess = purgeSessionOnSuccess;
            m_Initialized = false;
        }

        /// <summary>
        /// Initialize the upload request and underlying session data for transport.
        /// </summary>
        /// <returns>True if the session has been initialized and this is the only upload request trying to process this data.</returns>
        /// <remarks>If the session isn't already being transported to this endpoint then a lock will be set for transport.
        /// This request must be disposed to ensure this lock is released in a timely manner.</remarks>
        public bool PrepareSession()
        {
            Initialize();

            return (m_SessionTransportLock != null);
        }

        /// <summary>
        /// The repository the session upload request is coming from
        /// </summary>
        public LocalRepository Repository { get; private set; }

        /// <summary>
        /// The unique id to use for the client sending the session
        /// </summary>
        public Guid ClientId { get; private set; }

        /// <summary>
        /// The unique id of the session being sent.
        /// </summary>
        public Guid SessionId { get; private set; }

        /// <summary>
        /// Optional.  The unique id of the file within the session being sent.
        /// </summary>
        public Guid? FileId { get; set; }

        /// <summary>
        /// Indicates if the session should be purged from the repository once it has been sent successfully.
        /// </summary>
        public bool PurgeSessionOnSuccess { get; private set; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        #region Protected Properties and Methods

        /// <summary>
        /// Dispose managed resources
        /// </summary>
        /// <param name="releaseManaged"></param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (releaseManaged)
            {
                //we have to get rid of the lock if we have it.
                m_SessionTransportLock.SafeDispose();
                m_SessionTransportLock = null;

                m_SessionFileStream.SafeDispose();
                m_SessionFileStream = null;

                //and flush any temporary files we have if they aren't in a persistent repository.
                if (m_DeleteTemporaryFilesOnDispose)
                {
                    SafeDeleteTemporaryData();
                }
            }
        }

        private void PerformCleanup(IWebChannelConnection connection)
        {
            try
            {
                //we're going to upload zero bytes as a delete to the right URL.
                connection.UploadData(GenerateResourceUri(), HttpMethod.Delete, BinaryContentType, new byte[] { });
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                    Log.RecordException(0, ex, null, RepositoryPublishClient.LogCategory, true);
#endif
            }            
        }

        /// <summary>
        /// Implemented by inheritors to perform the request on the provided web client.
        /// </summary>
        /// <param name="connection"></param>
        protected override async Task OnProcessRequest(IWebChannelConnection connection)
        {
            if (m_Initialized == false)
            {
                Initialize();
            }

            if (m_SessionTransportLock == null)
            {
                throw new InvalidOperationException("The session is currently being transported by another process.");
            }

            //if we might have left a file fragment on the server we need to send a delete call to remove any partial file
            if (m_PerformCleanup)
            {
                m_PerformCleanup = false; //even if we fail, don't try again.
                PerformCleanup(connection);
            }

            var resourceUri = GenerateResourceUri();
            var additionalHeaders = new List<NameValuePair<string>>();
            if (string.IsNullOrEmpty(m_SessionFileHash) == false)
            {
                additionalHeaders.Add(new NameValuePair<string>(HubConnection.SHA1HashHeader, m_SessionFileHash));
            }

            //if it's SMALL we just put the whole thing up as a single action.
            if (m_SessionFileStream.Length < SinglePassCutoffBytes)
            {
                var sessionData = new byte[m_SessionFileStream.Length ];
                await m_SessionFileStream.ReadAsync(sessionData, 0, sessionData.Length).ConfigureAwait(false);
                await connection.UploadData(resourceUri, HttpMethod.Put, BinaryContentType, sessionData, additionalHeaders).ConfigureAwait(false);
            }
            else
            {
                //we need to do a segmented post operation.  Note that we may be restarting a request after an error, so don't reset
                //our bytes written.
                m_SessionFileStream.Position = m_BytesWritten;
                var restartCount = 0;
                var sessionData = new byte[DefaultSegmentSizeBytes];
                while (m_BytesWritten < m_SessionFileStream.Length)
                {
                    //Read the next segment which is either our segment size or the last fragment of the file, exactly sized.
                    if ((m_SessionFileStream.Length - m_SessionFileStream.Position) < sessionData.Length)
                    {
                        //we're at the last block - resize our buffer down.
                        sessionData = new byte[(m_SessionFileStream.Length - m_SessionFileStream.Position)];
                    }

                    await m_SessionFileStream.ReadAsync(sessionData, 0, sessionData.Length).ConfigureAwait(false);

                    var isComplete = (m_SessionFileStream.Position == m_SessionFileStream.Length);
                    var requestUrl =
                        $"{resourceUri}?Start={m_BytesWritten}&Complete={isComplete}&FileSize={m_SessionFileStream.Length}";

                    try
                    {
                        await connection.UploadData(requestUrl, HttpMethod.Post, BinaryContentType, sessionData, additionalHeaders).ConfigureAwait(false);

                        //and now that we've written the bytes and not gotten an exception we can mark these bytes as done!
                        m_BytesWritten = (int)m_SessionFileStream.Position;
                        UpdateProgressTrackingFile();
                    }
                    catch (WebChannelBadRequestException ex)
                    {
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, RepositoryPublishClient.LogCategory, "Server exchange error, we will assume client and server are out of sync.",
                                "The server returned a Bad Request Error (400) which generally means there is either a session-specific transfer problem that may be resolved by restarting the transfer from zero or an internal server problem.\r\nException: {0}", ex);

                        if (restartCount < 4)
                        {
                            //if we experience this type of Server-level transport error, assume there's some out of sync condition and start again.
                            restartCount++;
                            PerformCleanup(connection);
                            m_SessionFileStream.Position = 0;
                            m_BytesWritten = 0;
                            UpdateProgressTrackingFile();
                        }
                        else
                        {
                            //we didn't find a reason to restart the transfer, we need to let the exception fly.
                            throw;
                        }
                    }
                }
            }

            //and since we're now good & done...  clean up our temp stuff.
            SafeDeleteTemporaryData();

            //finally, if we are supposed to purge a session once we sent we need to give that a shot.
            if (PurgeSessionOnSuccess)
            {
                SafePurgeSession();
            }
        }

        #endregion

        #region Private Properties and Methods

        private int LoadProgressTrackingFile()
        {
            using (var sessionTrackingFileStream = new FileStream(m_TempSessionProgressFileNamePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                using (var reader = new BinaryReader(sessionTrackingFileStream, Encoding.UTF8))
                {
                    return reader.ReadInt32();
                }
            }            
        }

        private void UpdateProgressTrackingFile()
        {
            using (var sessionTrackingFileStream = new FileStream(m_TempSessionProgressFileNamePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                using (var writer = new BinaryWriter(sessionTrackingFileStream, Encoding.UTF8))
                {
                    writer.Write(m_BytesWritten);
                    writer.Flush();
                    sessionTrackingFileStream.SetLength(sessionTrackingFileStream.Position);
                }
            }
        }

        private void SafePurgeSession()
        {
            try
            {
                if (FileId.HasValue)
                    Repository.Remove(SessionId, FileId.Value); //this will just remove the one file, not the whole session.
                else
                    Repository.Remove(SessionId);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);

#if DEBUG
                Log.RecordException(0, ex, null, RepositoryPublishClient.LogCategory, true);
#endif
            }
        }

        /// <summary>
        /// Removes all of the temporary data used to transfer the session without allowing exceptions to propagate on failure.
        /// </summary>
        private void SafeDeleteTemporaryData()
        {
            SafeDeleteFile(m_TempSessionProgressFileNamePath);
            m_DeleteTemporaryFilesOnDispose = false; //because we already did.
        }

        private static void SafeDeleteFile(string fileNamePath)
        {
            if (string.IsNullOrEmpty(fileNamePath))
                return;

            try
            {
                File.Delete(fileNamePath);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);

#if DEBUG
                Log.RecordException(0, ex, null, RepositoryPublishClient.LogCategory, true);
#endif
            }            
        }

        private string GenerateResourceUri()
        {
            return FileId.HasValue ? $"/Hub/Hosts/{ClientId}/Sessions/{SessionId}/Files/{FileId}.zip"
                : $"/Hub/Hosts/{ClientId}/Sessions/{SessionId}/session.glf";
        }

        /// <summary>
        /// The temporary path to put all of the transfer information for this session.
        /// </summary>
        /// <returns></returns>
        private string GenerateTemporarySessionPath()
        {
            //find the right temporary directory for us...
            //if this is a real repository, we'll use a persistent path. 
            //otherwise we'll use something truly temporary and have to clean up after ourselves.
            string tempDirectory;
            if (Repository != null)
            {
                tempDirectory = Path.Combine(Repository.TempPath, SessionTempFolder);
            }
            else
            {
                tempDirectory = Path.GetTempFileName();
                File.Delete(tempDirectory); //we just want it as a directory, not a file.
                m_DeleteTemporaryFilesOnDispose = true;
            }

            //make damn sure it exists.
            Directory.CreateDirectory(tempDirectory);

            return tempDirectory;
        }

        /// <summary>
        /// The file name (without extension) for this session
        /// </summary>
        /// <returns></returns>
        private string GenerateTemporarySessionFileName()
        {
            //the path needs to be generated reliably, but uniquely.
            return FileId.HasValue ? $"{SessionId}_{ClientId}_{FileId}" : $"{SessionId}_{ClientId}";            
        }

        /// <summary>
        /// The full file name and path (without extension) for the transfer information for this session
        /// </summary>
        /// <returns></returns>
        internal string GenerateTemporarySessionFileNamePath()
        {
            return Path.Combine(GenerateTemporarySessionPath(), GenerateTemporarySessionFileName() + ".txt");
        }

        private void Initialize()
        {
            if (m_Initialized)
                return;

            //we need to grab a lock on this session to prevent it from being transported to the same endpoint at the same time.
            var sessionWorkingFileNamePath = GenerateTemporarySessionFileNamePath();

            //we aren't going to do Using - we keep the lock!
            if (m_SessionTransportLock == null) //if we are retrying to initialize after a failure we may already have it.
            {
                m_SessionTransportLock = InterprocessLockManager.Lock(this, GenerateTemporarySessionPath(), GenerateTemporarySessionFileName(), 0, true);
            }

            //we aren't waiting to see if we can get the lock - if anyone else has it they must be transferring the session.
            if (m_SessionTransportLock != null)
            {
                //grab a lock on the actual data file - if it's not there, no point in continuing.
                m_SessionFileStream = Repository.LoadSessionFileStream(SessionId, FileId.Value);

                //calculate our SHA1 Hash...
                try
                {
                    using (var csp = SHA1.Create())
                    {
                        m_SessionFileHash = BitConverter.ToString(csp.ComputeHash(m_SessionFileStream));
                    }

                    //now back up the stream to the beginning so we can send the actual data.
                    m_SessionFileStream.Position = 0;
                }
                catch (Exception ex)
                {
                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, RepositoryPublishClient.LogCategory, "Unable to calculate hash for session file due to " + ex.GetType() + " exception.", "The upload will proceed but without the hash to check the accuracy of the upload.\r\nException: {0}\r\n{1}\r\n", ex.GetType(), ex.Message);
                }

                //Lets figure out if we're restarting a previous send or starting a new one.
                m_TempSessionProgressFileNamePath = sessionWorkingFileNamePath;

                m_BytesWritten = 0;
                if (File.Exists(m_TempSessionProgressFileNamePath))
                {
                    //load up the existing transfer state.
                    try
                    {
                        m_BytesWritten = LoadProgressTrackingFile();
                    }
                    catch
                    {
                        //oh well, assume no progress.
                        m_PerformCleanup = true;

                        SafeDeleteFile(m_TempSessionProgressFileNamePath);
                    }
                }
                else
                {
                    //make sure we didn't get started, but not finish, writing our temp file.
                    SafeDeleteFile(m_TempSessionProgressFileNamePath);
                }

                m_Initialized = true;
            }
        }

        #endregion
    }
}
