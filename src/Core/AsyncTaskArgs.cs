
using System;



namespace Gibraltar
{
    /// <summary>
    /// Arguments used to execute tasks asynchronously using progress monitors.
    /// </summary>
    public class AsyncTaskArguments : IDisposable
    {
        private readonly string m_Title;
        private readonly ProgressMonitorStack m_ProgressMonitors;
        private readonly object m_State;

        private bool m_WeAreDisposed;

        /// <summary>
        /// Create a new asynchronous execution task
        /// </summary>
        /// <param name="title"></param>
        /// <param name="state"></param>
        /// <remarks>Creates its own progress monitor stack</remarks>
        public AsyncTaskArguments(string title, object state)
        {
            m_Title = title;
            m_State = state;
            m_ProgressMonitors = new ProgressMonitorStack(title);
        }

        #region public Properties and Methods

        /// <summary>
        /// Retrieve the progress monitor stack for this asynchronous task
        /// </summary>
        public ProgressMonitorStack ProgressMonitors { get { return m_ProgressMonitors; } }

        /// <summary>
        /// The state object for this asynchronous task (contains any arguments necessary to execute the task)
        /// </summary>
        public object State { get { return m_State; } }

        /// <summary>
        /// An end-user display title for this task.
        /// </summary>
        public string Title { get { return m_Title; } }

        /// <summary>
        /// The final results of the asynchronous task, set upon completion.
        /// </summary>
        public AsyncTaskResultEventArgs TaskResult { get; set; }

        ///<summary>
        ///Performs application-defined tasks associated with freeing, releasing, or resetting managed resources.
        ///</summary>
        ///<filterpriority>2</filterpriority>
        public void Dispose()
        {
            // Call the underlying implementation
            Dispose(true);

            // and SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.
        /// </summary>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (!m_WeAreDisposed)
            {
                m_WeAreDisposed = true; // Only Dispose stuff once

                if (releaseManaged)
                {
                    // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                    // Other objects may be referenced in this case

                    // We create a ProgressMonitorStack in our constructor, so we must release it here
                    m_ProgressMonitors.Dispose();
                }
                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here
            }
        }

        #endregion
    }
}
