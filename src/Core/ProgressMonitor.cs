
using System;
using System.Diagnostics;



namespace Gibraltar
{
    /// <summary>
    /// Observes the progress of one process for the purpose of progress reporting in user interfaces.
    /// </summary>
    /// <remarks></remarks>
    [DebuggerDisplay("Status: {StatusMessage}, Progress: {CompletedSteps} of {MaxSteps}")]
    public class ProgressMonitor : IDisposable
    {
        private readonly object m_Lock = new object();  //for multithreaded locking
        private readonly object m_Owner;
        private readonly ProgressMonitorStack m_ProgressMonitors;

        private string m_StatusMessage;
        private double m_PercentComplete;
        private int m_CompletedSteps;
        private int m_MaxSteps;
        private bool m_PercentCompleteValid;
        private bool m_Complete;
        private bool m_ReadOnly;


        /// <summary>
        /// Raised when the monitor stack is in the process of cancelling.
        /// </summary>
        public event EventHandler<ProgressMonitorEventArgs> Canceled;

        /// <summary>
        /// Raised whenever the monitor's information is updated.
        /// </summary>
        public event EventHandler<ProgressMonitorEventArgs> Updated;

        internal ProgressMonitor(ProgressMonitorStack progressMonitors, object owner, string status, int maxSteps)
        {
            m_Owner = owner;
            m_ProgressMonitors = progressMonitors;
            m_PercentComplete = 0.0;
            m_CompletedSteps = 0;
            m_MaxSteps = maxSteps;

            StatusMessage = status;
            m_PercentCompleteValid = true;
            m_Complete = false;
            m_ReadOnly = false;
        }

        #region Public Properties and Methods

        /// <summary>
        /// Explicitly indicate this monitored task is complete.
        /// </summary>
        /// <remarks>Once marked complete, the monitor will be set to 100% and will not accept future updates.</remarks>
        public bool Complete
        {
            get { return m_Complete; }
            set
            {
                //we really only do something if value is true
                if (value)
                {
                    //we will allow multiple calls to Complete, but only the first does anything.
                    lock(m_Lock)
                    {
                        if (m_Complete)
                            return;

                        if (m_ReadOnly)
                        {
                            //we are no longer updatable
                            throw new InvalidOperationException("The monitor has been marked complete and is now read only.");
                        }

                        //Now we are going to force the percentage to 100% and mark it valid
                        m_PercentComplete = 1;
                        m_PercentCompleteValid = true;
                        m_Complete = true;

                        //and now we're read only
                        m_ReadOnly = true;

                        //fire off one last status event
                        OnMonitorUpdated();

                        //we are explicitly complete, pop ourself off the monitor stack.
                        m_ProgressMonitors.PopMonitor(this);
                    }
                }
            }
        }                

        /// <summary>
        /// The number of steps that have been completed.
        /// </summary>
        /// <remarks>Used to calculate percent complete by comparing with MaxSteps.  To update, call Update 
        /// which will notify subscribers that this object has changed.</remarks>
        public int CompletedSteps
        {
            get { return m_CompletedSteps; }
            private set
            {
                //do some sanity checking on current step - can't be negative.
                if ((value > 0) && (m_CompletedSteps != value))
                {
                    m_CompletedSteps = value;

#if DEBUG
                    Debug.Assert(m_CompletedSteps <= m_MaxSteps);
#endif

                    //and invalidate the cached percentage
                    m_PercentCompleteValid = false;
                }
            }
        }

        /// <summary>
        /// Indicates that the monitor is complete and should be immediately disposed
        /// </summary>
        /// <remarks>Internally equivalent to setting Complete to true.</remarks>
        public void Dispose()
        {
            // Call the underlying implementation.
            Dispose(true);

            // SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.
        /// </summary>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (releaseManaged)
            {
                // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                // Other objects may be referenced in this case
                if (!m_ReadOnly)
                    // mark ourself complete, which will perform appropriate cleanup actions
                    Complete = true;
            }
            // Free native resources here (alloc's, etc)
            // May be called from within the finalizer, so don't reference other objects here
        }

        /// <summary>
        /// The maximum number of steps in the process being monitored
        /// </summary>
        /// <remarks>Used to calculate percent complete by comparing with CurrentStep.  To update, call Update
        /// which will notify subscribers that this object has changed.  For the best user experience, attempt to change
        /// this value as rarely as feasible so that the percentage doesn't appear to go down.</remarks>
        public int MaxSteps
        {
            get { return m_MaxSteps; }
            private set
            {
                //do some sanity checking on value - can't be negative.
                if ((value > 0) && (m_MaxSteps != value))
                {
                    m_MaxSteps = value;

                    //and invalidate the cached percentage
                    m_PercentCompleteValid = false;
                }
            }
        }

        /// <summary>
        /// The object that owns this monitor.
        /// </summary>
        /// <remarks>Occasionally useful for development/debugging purposes when the status message isn't sufficiently descriptive.</remarks>
        public object Owner { get { return m_Owner; } }

        /// <summary>
        /// The percentage of steps currently complete for this monitor, with 1 representing 100 percent.
        /// </summary>
        public double PercentComplete
        {
            get
            {
                lock (m_Lock)
                {
                    if (m_PercentCompleteValid == false)
                    {
                        //recalculate the percentage
                        if (m_MaxSteps > 0)
                        {
                            m_PercentComplete = m_CompletedSteps / (double)m_MaxSteps;
                        }
                        else
                        {
                            //in the divide by zero case, assume we are incomplete - just peg at 0%.
                            m_PercentComplete = 0;
                        }

                        //if we go over 1, cap at 1. (which is 100%)
                        if (m_PercentComplete > 1)
                        {
                            m_PercentComplete = 1;
                        }

                        m_PercentCompleteValid = true;
                    }

                    return m_PercentComplete;
                }
            }
        }

        /// <summary>
        /// An end-user status message to display for the current state of the monitor
        /// </summary>
        public string StatusMessage
        {
            get { return m_StatusMessage; }
            private set {
                m_StatusMessage = string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
            }
        }

        /// <summary>
        /// Update the progress of the monitor.
        /// </summary>
        /// <remarks>Subscribers will be notified of changes whenever Update is called.</remarks>
        /// <param name="status">A new user status message</param>
        public void Update(string status)
        {
            lock (m_Lock)
            {
                if (m_ReadOnly)
                {
                    //we are no longer updatable
                    throw new InvalidOperationException("The monitor has been marked complete and is now read only.");
                }

                StatusMessage = status;
                OnMonitorUpdated();
            }
        }

        /// <summary>
        /// Update the progress of the monitor.
        /// </summary>
        /// <remarks>Subscribers will be notified of changes whenever Update is called.</remarks>
        /// <param name="status">A new user status message</param>
        /// <param name="completedSteps">The number of steps that have been completed. (should be less than or equal to the maximum number of steps)</param>
        public void Update(string status, int completedSteps)
        {
            lock (m_Lock)
            {
                if (m_ReadOnly)
                {
                    //we are no longer updatable
                    throw new InvalidOperationException("The monitor has been marked complete and is now read only.");
                }

                StatusMessage = status;
                CompletedSteps = completedSteps;
                OnMonitorUpdated();
            }
        }

        /// <summary>
        /// Update the progress of the monitor.
        /// </summary>
        /// <remarks>Subscribers will be notified of changes whenever Update is called.
        /// When updating the maximum number of steps it is best to guess high at first then reduce over time
        /// instead of the reverse to give the user the impression of continuous forward progress instead of 
        /// losing progress.</remarks>
        /// <param name="status">A new user status message</param>
        /// <param name="completedSteps">The number of steps that have been completed. (should be less than or equal to the maximum number of steps)</param>
        /// <param name="maxSteps">The maximum number of steps in this process.</param>
        public void Update(string status, int completedSteps, int maxSteps)
        {
            lock (m_Lock)
            {
                if (m_ReadOnly)
                {
                    //we are no longer updatable
                    throw new InvalidOperationException("The monitor has been marked complete and is now read only.");
                }

                StatusMessage = status;
                MaxSteps = maxSteps;
                CompletedSteps = completedSteps;
                OnMonitorUpdated();
            }
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Called to raise the MonitorCanceled event.
        /// </summary>
        /// <remarks>Any inheritors that override this event must add a call to Base.OnMonitorCanceled at the end of their routine to ensure the event is raised.</remarks>
        protected virtual void OnMonitorCanceled()
        {
            ProgressMonitorEventArgs e = new ProgressMonitorEventArgs(m_ProgressMonitors, this);

            //save the delegate field in a temporary field for thread safety
            EventHandler<ProgressMonitorEventArgs> tempEvent = Canceled;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        /// <summary>
        /// Called to raise the MonitorUpdated event.
        /// </summary>
        /// <remarks>Any inheritors that override this event must add a call to Base.OnMonitorUpdated at the end of their routine to ensure the event is raised.</remarks>
        protected virtual void OnMonitorUpdated()
        {
            ProgressMonitorEventArgs e = new ProgressMonitorEventArgs(m_ProgressMonitors, this);

            //save the delegate field in a temporary field for thread safety
            EventHandler<ProgressMonitorEventArgs> tempEvent = Updated;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Called by the monitor stack to indicate that this monitor should cancel.
        /// </summary>
        /// <remarks>This is only for use by the progress monitor stack. Any other use will cause problems.</remarks>
        internal void Cancel()
        {
            lock (m_Lock)
            {
                if (m_Complete)
                {
                    //nothing more to do - complete beat us to it
                    return;
                }

                //otherwise, we now need to signal that we're canceled.
                if (m_ReadOnly)
                {
                    //we don't want to throw an exception, this is an internal method
                    return;
                }

                m_ReadOnly = true;

                OnMonitorCanceled();

                //we are done, pop ourself off the monitor stack.
                m_ProgressMonitors.PopMonitor(this);
            }
        }

        #endregion
    }
}