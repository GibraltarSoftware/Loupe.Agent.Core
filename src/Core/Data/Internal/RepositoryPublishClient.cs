using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Loupe.Core.Monitor;
using Loupe.Core.Server.Client;
using Loupe.Core.Server.Client.Data;
using Loupe.Configuration;
using Loupe.Extensibility.Data;
using Loupe.Logging;

namespace Loupe.Core.Data.Internal
{
    /// <summary>
    /// Publishes sessions from the specified repository to a remote destination repository.
    /// </summary>
    internal class RepositoryPublishClient: IDisposable
    {
        internal const string LogCategory = "Loupe.Repository.Publish";

        private readonly LocalRepository m_SourceRepository;
        private readonly string m_ProductName;
        private readonly string m_ApplicationName;
        private readonly HubConnection m_HubConnection;

        private volatile bool m_IsActive;
        private volatile bool m_Disposed;

        /// <summary>
        /// Create a new repository publish engine for the specified repository.
        /// </summary>
        /// <param name="source">The repository to publish</param>
        public RepositoryPublishClient(LocalRepository source)
            : this(source, null, null, Log.Configuration.Server)
        {
            //everything was handled in the constructor overload we passed off to
        }

        /// <summary>
        /// Create a new repository publish engine for the specified repository.
        /// </summary>
        /// <param name="source">The repository to publish</param>
        /// <param name="productName">Optional.  A product name to restrict operations to.</param>
        /// <param name="applicationName">Optional.  An application name within a product to restrict operations to.</param>
        /// <param name="configuration">The server connection information.</param>
        public RepositoryPublishClient(LocalRepository source, string productName, string applicationName, ServerConfiguration configuration)
            : this(source, configuration)
        {
            m_ProductName = productName;
            m_ApplicationName = applicationName;
        }

        /// <summary>
        /// Create a new repository publish engine for the specified repository.
        /// </summary>
        /// <param name="source">The repository to publish</param>
        /// <param name="serverConfiguration">The configuration of the connection to the server</param>
        public RepositoryPublishClient(LocalRepository source, ServerConfiguration serverConfiguration)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            m_SourceRepository = source;

            m_HubConnection = new HubConnection(serverConfiguration);
        }

        #region Public Properties and Methods

        /// <summary>
        /// The repository this publish engine is associated with.
        /// </summary>
        public LocalRepository Repository { get { return m_SourceRepository; } }

        /// <summary>
        /// Indicates if this is the active repository publish engine for the specified repository.
        /// </summary>
        public bool IsActive
        {
            get
            {
                return m_IsActive;
            }
        }

        /// <summary>
        /// Attempts to connect to the server and returns information about the connection status.
        /// </summary>
        /// <returns>True if the configuration is valid and the server is available, false otherwise.</returns>
        public async Task<HubConnectionStatus> CanConnect()
        {
            return await m_HubConnection.CanConnect().ConfigureAwait(false);
        }

        /// <summary>
        /// Publish qualifying local sessions and upload any details requested by the server
        /// </summary>
        /// <param name="async"></param>
        /// <param name="purgeSentSessions">Indicates if the session should be purged from the repository once it has been sent successfully.</param>
        public async Task PublishSessions(bool async, bool purgeSentSessions)
        {
            if (m_IsActive)
                return; //we're already publishing, we can't queue more.

            //we do the check for new sessions on the foreground thread since it won't block.
            var sessions = GetSessions();

            if ((sessions != null) && (sessions.Count > 0))
            {
                //go ahead and use the threadpool to publish the sessions.
                m_IsActive = true; //this gets set to false by the publish sessions routine when it's done.

                object[] state = new object[] { sessions, -1, purgeSentSessions };//retry until successful (-1)
                if (async)
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Run(() =>AsyncPublishSessions(state));
#pragma warning restore CS4014
                }
                else
                {
                    await AsyncPublishSessions(state).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Send the specified session with details, even if other publishers are running.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="maxRetries"></param>
        /// <param name="purgeSentSession">Indicates if the session should be purged from the repository once it has been sent successfully.</param>
        /// <remarks>Throws an exception if it fails</remarks>
        public async Task UploadSession(Guid sessionId, int maxRetries, bool purgeSentSession)
        {
            await PerformSessionDataUpload(sessionId, maxRetries, purgeSentSession).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private Properties and Methods

        private void Dispose(bool releaseManaged)
        {
            if (releaseManaged && !m_Disposed)
            {
                if (m_HubConnection != null)
                {
                    m_HubConnection.Dispose();
                }

                m_Disposed = true;
            }
        }

        /// <summary>
        /// Publish the latest session data and find out what sessions should be uploaded.
        /// </summary>
        private async Task AsyncPublishSessions(object state)
        {
            try
            {
                object[] arguments = (object[])state;

                IList<ISessionSummary> sessions = (IList<ISessionSummary>)arguments[0];
                int maxRetries = (int)arguments[1];
                bool purgeSentSessions = (bool)arguments[2];

                if (sessions == null)
                    return;

                //lets make sure the server is connectible.
                var status = await m_HubConnection.CanConnect().ConfigureAwait(false);
                if (status.IsValid == false && (maxRetries >=0))
                {
                    //we are stopping right here because the server isn't there, so no point in trying anything else.
#if DEBUG
                    Log.Write(LogMessageSeverity.Warning, LogCategory, "Unable to publish sessions because the server is not available", "While verifying that the server is available the following status information was returned:\r\nStatus: {0}\r\nMessage: {1}", status.Status, status.Message);
#endif
                }
                else
                {
                    //OK, now we've released the session information from RAM (all we wanted were the GUID's anyway)
                    //and we can send these one by one as long as the connection is up.
                    foreach (ISessionSummary session in sessions)
                    {
                        //try to upload it.
                        try
                        {
                            await PerformSessionHeaderUpload(session).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            GC.KeepAlive(ex);
#if DEBUG
                            Log.RecordException(0, ex, null, LogCategory, true);
#endif
                        }
                    }

                    //now find out what sessions they want us to upload
                    List<Guid> requestedSessions = await GetRequestedSessions().ConfigureAwait(false);

                    foreach (Guid sessionId in requestedSessions)
                    {
                        //we want to try each, even if they fail.
                        try
                        {
                            await PerformSessionDataUpload(sessionId, maxRetries, purgeSentSessions).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            GC.KeepAlive(ex);
#if DEBUG
                            Log.RecordException(0, ex, null, LogCategory, true);
#endif
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                Log.RecordException(0, ex, null, LogCategory, true);
#endif
            }
            finally
            {
                m_IsActive = false; //so others can go now.
            }
        }

        /// <summary>
        /// Find out what sessions the server wants details for.
        /// </summary>
        private async Task<List<Guid>> GetRequestedSessions()
        {
            RequestedSessionsGetRequest request = new RequestedSessionsGetRequest(m_SourceRepository.Id);

            try
            {
                await m_HubConnection.ExecuteRequest(request, 1).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                Log.RecordException(0, ex, null, LogCategory, true);
#endif
            }

            var requestedSessions = new List<Guid>();

            var serverRepository = await m_HubConnection.GetRepository().ConfigureAwait(false);

            if ((request.RequestedSessions != null) 
                && (request.RequestedSessions.sessions != null)
                && (request.RequestedSessions.sessions.Length > 0))
            {
                foreach (SessionXml requestedSession in request.RequestedSessions.sessions)
                {
                    //we want to either queue the session to be sent (if not queued already) or
                    //mark the session as complete on the server is no data is available.
                    try
                    {
                        Guid sessionId = new Guid(requestedSession.id);
                        if (m_SourceRepository.SessionDataExists(sessionId))
                        {
                            //queue for transmission
                            requestedSessions.Add(sessionId);
                        }
                        else
                        {
                            if (serverRepository.ProtocolVersion < HubConnection.Hub30ProtocolVersion)
                            {
                                if (!Log.SilentMode)
                                    Log.Write(LogMessageSeverity.Information, LogCategory, "Server requesting completed session that's no longer available", "There's no way for us to tell the server that it should stop asking for this session.\r\nSession Id: {0}", sessionId);
                            }
                            else
                            {
                                //it's complete, there's nothing more we can give them.
                                //KM: We can't assume there is no data - it could be in another local repository, so we shouldn't do this.
                                //PerformSessionMarkComplete(sessionId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        GC.KeepAlive(ex);
#if DEBUG
                        Log.RecordException(0, ex, null, LogCategory, true);
#endif
                    }
                }
            }

            return requestedSessions;
        }

        /// <summary>
        /// Find the list of all sessions that haven't been published yet and match our filter
        /// </summary>
        /// <returns></returns>
        private IList<ISessionSummary> GetSessions()
        {
            //find the list of all sessions that haven't been published yet and match our filter
            IList<ISessionSummary> sessions = null;

            try
            {
                m_SourceRepository.Refresh(); //we want a picture of the latest data as of the start of this process.

                sessions = m_SourceRepository.Find(UnsentSessionsPredicate);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                Log.RecordException(0, ex, null, LogCategory, true);
#endif
            }

            return sessions;
        }

        /// <summary>
        /// A predicate filter for the repository to identify unsent, qualifying sessions
        /// </summary>
        /// <param name="candidateSession"></param>
        /// <returns></returns>
        private bool UnsentSessionsPredicate(ISessionSummary candidateSession)
        {
            bool matchesPredicate = candidateSession.IsNew;

            if (matchesPredicate)
                matchesPredicate = candidateSession.Product.Equals(m_ProductName, StringComparison.OrdinalIgnoreCase);

            if (matchesPredicate && !string.IsNullOrEmpty(m_ApplicationName))
                matchesPredicate = candidateSession.Application.Equals(m_ApplicationName, StringComparison.OrdinalIgnoreCase);

            return matchesPredicate;
        }

        /// <summary>
        /// Sends a session, either as a single stream or a set of fragments, to the server.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="maxRetries">The maximum number of times to retry the session data upload.</param>
        /// <param name="purgeSentSessions">Indicates whether to purge sessions that have been successfully sent from the repository</param>
        /// <returns>Throws an exception if the upload fails.</returns>
        private async Task PerformSessionDataUpload(Guid sessionId, int maxRetries, bool purgeSentSessions)
        {
            m_SourceRepository.Refresh(); //we want a picture of the latest data as of the start of this process.

            //this can get a little complicated:  Do we use the new fragment-based protocol or the old single session mode?
            var serverRepository = await m_HubConnection.GetRepository().ConfigureAwait(false);
            bool useSingleStreamMode;
            Dictionary<Guid, SessionFileXml> serverFiles = new Dictionary<Guid, SessionFileXml>();
            if (serverRepository.ProtocolVersion < HubConnection.Hub30ProtocolVersion)
            {
                //use the legacy single stream mode
                useSingleStreamMode = true;
            }
            else
            {
                //see if we can use the more efficient mode, and if so what files we should send.
                var sessionFilesRequest = new SessionFilesGetRequest(m_SourceRepository.Id, sessionId);
                await m_HubConnection.ExecuteRequest(sessionFilesRequest, maxRetries).ConfigureAwait(false);

                useSingleStreamMode = sessionFilesRequest.Files.singleStreamOnly;

                if ((!useSingleStreamMode) && (sessionFilesRequest.Files.files != null))
                {
                    //since individual files are immutable we don't need to upload any file the server already has.
                    foreach (var sessionFileXml in sessionFilesRequest.Files.files)
                    {
                        serverFiles.Add(new Guid(sessionFileXml.id), sessionFileXml);
                    }
                }
            }

            if (useSingleStreamMode)
            {
                await PerformSessionFileUpload(sessionId, null, maxRetries, purgeSentSessions).ConfigureAwait(false);
            }
            else
            {
                //it's a bit more complicated:  We need to update each file they don't have.  
                m_SourceRepository.LoadSessionFiles(sessionId, out var sessionHeader, out var sessionFragments);

                foreach (var sessionFragment in sessionFragments)
                {
                    //if they already have this one, skip it.
                    var fileHeader = LocalRepository.LoadSessionHeader(sessionFragment.FullName);
                    if (fileHeader == null)
                        break; //the file must be gone, it certainly isn't valid.

                    if (serverFiles.ContainsKey(fileHeader.FileId))
                    {
                        //skip this file.  If we're supposed to be purging sent data then drop this fragment.
                        if (purgeSentSessions)
                            m_SourceRepository.Remove(sessionId, fileHeader.FileId);
                    }
                    else
                    {
                        //ohhkay, lets upload this bad boy.
                        await PerformSessionFileUpload(sessionId, fileHeader.FileId, maxRetries, purgeSentSessions).ConfigureAwait(false);
                    }
                }

                if (!m_SourceRepository.SessionIsRunning(sessionId))
                {
                    //finally, mark this session as complete.  We've sent all the data we have.  But, we won't fail if we can't.
                    try
                    {
                        await PerformSessionMarkComplete(sessionId).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, true, LogCategory, "Unable to inform server a session is complete due to " + ex.GetBaseException().GetType(),
                                "The server may continue to ask for data for this session until we can tell it we have no more data.\r\nSession Id: {0}\r\nException: {1}", sessionId, ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Sends a merged session stream or a single session fragment file to the server.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="fileId"></param>
        /// <param name="maxRetries">The maximum number of times to retry the session data upload.</param>
        /// <param name="purgeSentSessions">Indicates whether to purge sessions that have been successfully sent from the repository</param>
        /// <returns>Throws an exception if the upload fails.</returns>
        private async Task PerformSessionFileUpload(Guid sessionId, Guid? fileId, int maxRetries, bool purgeSentSessions)
        {
            using (var request = new SessionUploadRequest(m_SourceRepository.Id, m_SourceRepository, sessionId, fileId, purgeSentSessions))
            {
                //because upload request uses a multiprocess lock we put it in a using to ensure it gets disposed.
                //explicitly prepare the session - this returns true if we got the lock meaning no one else is actively transferring this session right now.
                if (request.PrepareSession() == false)
                {
                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Information, LogCategory, "Skipping sending session to server because another process already is transferring it", "We weren't able to get a transport lock on the session '{0}' so we assume another process is currently sending it.", sessionId);
                }
                else
                {
                    await m_HubConnection.ExecuteRequest(request, maxRetries).ConfigureAwait(false);
                }
            }
            
        }

        /// <summary>
        /// Upload the session summary for one session.
        /// </summary>
        /// <param name="sessionSummary"></param>
        private async Task PerformSessionHeaderUpload(ISessionSummary sessionSummary)
        {
            SessionXml sessionSummaryXml = DataConverter.ToSessionXml(sessionSummary); 
            await PerformSessionHeaderUpload(sessionSummaryXml).ConfigureAwait(false);
        }

        /// <summary>
        /// Upload the session summary for one session.
        /// </summary>
        /// <param name="sessionSummary"></param>
        private async Task PerformSessionHeaderUpload(SessionXml sessionSummary)
        {
            var sessionId = new Guid(sessionSummary.id);

#if DEBUG
            Debug.Assert(!String.IsNullOrEmpty(sessionSummary.sessionDetail.productName));
            Debug.Assert(!String.IsNullOrEmpty(sessionSummary.sessionDetail.applicationName));
            Debug.Assert(!String.IsNullOrEmpty(sessionSummary.sessionDetail.applicationVersion));
#endif

            //we consider a session complete (since we're the source repository) with just the header if there
            //is no session file.
            sessionSummary.sessionDetail.isComplete = !m_SourceRepository.SessionDataExists(sessionId);

            var uploadRequest = new SessionHeaderUploadRequest(sessionSummary, m_SourceRepository.Id);

            //get our web channel to upload this request for us.
            await m_HubConnection.ExecuteRequest(uploadRequest, -1).ConfigureAwait(false);

            //and if we were successful (must have been - we got to here) then mark the session as not being new any more.
            m_SourceRepository.SetSessionsNew(new[] { sessionId }, false);
        }

        /// <summary>
        /// Mark the specified session as being complete.
        /// </summary>
        private async Task PerformSessionMarkComplete(Guid sessionId)
        {
            var uploadRequest = new SessionMarkComplete(sessionId, m_SourceRepository.Id);

            //get our web channel to upload this request for us.
            await m_HubConnection.ExecuteRequest(uploadRequest, -1).ConfigureAwait(false);
        }

        #endregion
    }
}
