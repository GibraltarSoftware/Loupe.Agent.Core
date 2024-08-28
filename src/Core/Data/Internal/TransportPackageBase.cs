using System;
using System.Collections.Generic;
using System.Diagnostics;
using Gibraltar.Monitor;
using Loupe.Extensibility.Data;



namespace Gibraltar.Data.Internal
{
    internal abstract class TransportPackageBase : IDisposable
    {
        private bool m_IsDisposed;

        protected TransportPackageBase(string product, string application, SimplePackage package)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));

            if (string.IsNullOrEmpty(product))
                throw new ArgumentNullException(nameof(product));

            //application IS allowed to be null.

            Package = package;
            Product = product;
            Application = application;
        }

        public SimplePackage Package { get; private set; }

        /// <summary>
        /// The product the package was restricted to.
        /// </summary>
        public string Product { get; private set; }

        /// <summary>
        /// The application the package was restricted to (optional, may be null)
        /// </summary>
        public string Application { get; private set; }

        public bool HasProblemSessions { get; set; }

        public void Dispose()
        {
            if (m_IsDisposed == false)
            {
                m_IsDisposed = true;
                Dispose(true);
            }

            GC.SuppressFinalize(this);
        }

        public PackageSendEventArgs Status { get; private set; }

        /// <summary>
        /// Mark all of the sessions contained in the source package as being read.
        /// </summary>
        public void MarkContentsAsRead(LocalRepository repository)
        {
            //we need to make sure we have the package lock at least long enough to keep the working package and grab sessions.
            if (Package == null)
                throw new NullReferenceException("There is no working package available, indicating an internal Loupe programming error.");

            try
            {
                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, Packager.LogCategory, "Marking all sessions in the package as read", null);

                IList<SessionHeader> allSessions = Package.GetSessionHeaders();

                if (allSessions.Count == 0)
                {
                    if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, Packager.LogCategory, "No Sessions to Mark Read", "There are unexpectedly no sessions in the working package, so none can be marked as read.  We shouldn't have gotten to this point if tehre are no sessions in the package.");
                }
                else
                {
                    if (!Log.SilentMode) Log.Write(LogMessageSeverity.Verbose, Packager.LogCategory, "Found sessions to mark as read", "There are {0} sessions to be marked as read (although some may have been marked as read before).", allSessions.Count);

                    //assemble the array of sessions to change
                    List<Guid> sessionIds = new List<Guid>(allSessions.Count);
                    foreach (var session in allSessions)
                    {
                        sessionIds.Add(session.Id);
                    }

                    try
                    {
                        repository.SetSessionsNew(sessionIds.ToArray(), false);
                    }
                    catch (Exception ex)
                    {
                        if (!Log.SilentMode) Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, Packager.LogCategory, "Error marking an included session as read",
                                  "Unable to mark one or more of the sessions included in the package as read.  This won't prevent the package from being sent.  Exception:\r\n{0}",
                                  ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!Log.SilentMode) Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, Packager.LogCategory, "General error while marking included sessions as read",
                          "A general error occurred while marking the source sessions included in the package as read.  This won't prevent the package from being sent.\r\nException ({0}):\r\n{1}",
                          ex.GetType().FullName, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Perform the actual package transport
        /// </summary>
        /// <param name="progressMonitors"></param>
        /// <returns></returns>
        public void Send(ProgressMonitorStack progressMonitors)
        {
            Status = OnSend(progressMonitors);
#if DEBUG
            Debug.Assert(Status != null);
#endif
        }


        #region Protected Properties and Methods

        /// <summary>
        /// Overridden by our inheritors to implement the package send routine.
        /// </summary>
        /// <param name="progressMonitors"></param>
        /// <returns></returns>
        protected abstract PackageSendEventArgs OnSend(ProgressMonitorStack progressMonitors);


        #endregion

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// </summary>
        /// <remarks>
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization. Note to inheritors:  Be sure to call base to enable the base class to release resources.</remarks>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (releaseManaged)
            {
                try
                {
                    if (Package != null)
                    {
                        Package.Dispose();
                        Package = null;
                    }
                }
                catch (Exception ex)
                {
                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, Packager.LogCategory, "Unable to properly dispose working email package file",
                                  "Unable to properly dispose the transport package due to an exception ({0}): {1}",
                                  ex.GetType().FullName, ex.Message);
                }
            }
        }
    }


}
