using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

namespace Gibraltar
{
    /// <summary>
    /// The default progress monitor stack implementation.
    /// </summary>
    [DebuggerDisplay("{Title}:{StatusMessage}, Completed {CompletedSteps} of {MaximumSteps}. {Count} monitors")]
    public class ProgressMonitorStack : IDisposable
    {
        /// <summary>
        /// The default maximum number of steps for tracking progress if not otherwise specified.
        /// </summary>
        public const int DefaultMaximumSteps = 100;

        private readonly object m_Lock = new object();  //used for multithreaded safety
        private readonly Stack<ProgressMonitor> m_Stack = new Stack<ProgressMonitor>();
        private readonly string m_Title;

        private bool m_Complete;
        private bool m_ReadOnly;
        private string m_StatusMessage;
        private int m_CompletedSteps;
        private int m_MaximumSteps;
        private bool m_Canceled;

        /// <summary>
        /// Raised when the monitor stack is cancelled.  No complete event will then be raised.
        /// </summary>
        public event EventHandler<ProgressMonitorStackEventArgs> Canceled;

        /// <summary>
        /// Raised whenever a monitor object is pushed or popped from the stack, effectively changing what monitor is on the top of the stack.
        /// </summary>
        public event EventHandler<ProgressMonitorEventArgs> Changed;

        /// <summary>
        /// Raised when the monitor stack completes.
        /// </summary>
        public event EventHandler<ProgressMonitorStackEventArgs> Completed;

        /// <summary>
        /// Raised when the progress information is updated.
        /// </summary>
        public event EventHandler<ProgressMonitorStackEventArgs> Updated;

        /// <summary>
        /// Create a new monitor stack to monitor a process.
        /// </summary>
        /// <param name="title">An end-user display title for the overall business process</param>
        public ProgressMonitorStack(string title)
        {
            m_Title = title;
        }

        #region Public Properties and Methods

        /// <summary>
        /// Request to cancel the monitor stack.
        /// </summary>
        /// <remarks>This is designed to signal each monitor object to cancel their work.  It will block until the cancel is complete.</remarks>
        public void Cancel()
        {
            lock(m_Lock)
            {
                if (m_Complete)
                {
                    //nothing more to do - complete beat us to it
                    return;
                }

                //otherwise, we now need to signal that we're canceled.
                if (m_ReadOnly)
                {
                    //we don't want to throw an exception, this may be a panic multiple call method.
                    return;
                }

                m_ReadOnly = true;

                //mark each monitor in the stack complete incrementally.
                while (m_Stack.Count > 0)
                {
                    //get the top item and ask it to signal it to cancel
                    ProgressMonitor topProgressMonitor = m_Stack.Peek();

                    topProgressMonitor.Cancel(); //which will have this monitor object pop itself as well
                }

                m_Canceled = true;

                //and we're read only and cancelled.
                m_ReadOnly = true;

                OnCanceled();
            }
        }

        /// <summary>
        /// Explicitly indicate the entire monitor stack is complete
        /// </summary>
        /// <remarks>Each monitor in the stack will be marked complete.  Once marked complete, 
        /// the monitor will be set to 100% and will not accept future updates.</remarks>
        public bool Complete
        {
            get { return m_Complete; }
            set
            {
                //we only care if the value is true.
                if (value)
                {
                    lock(m_Lock)
                    {
                        //if we are already complete, nothing to do.
                        if (m_Complete)
                            return;

                        //if we're read only then big problem.
                        if (m_ReadOnly)
                        {
                            //we are no longer updatable
                            throw new ReadOnlyException("The monitor has been marked complete and is now read only.");
                        }

                        //and we're read only and complete.
                        m_Complete = true;
                        m_ReadOnly = true;

                        //mark each monitor in the stack complete incrementally.
                        //NOTE to maintainers:  Be careful about changes to this area because the PopMonitor routine can call this area too
                        //which could create an infinite loop
                        while (m_Stack.Count > 0)
                        {
                            //get the top item and ask it to signal it's complete
                            ProgressMonitor topProgressMonitor = m_Stack.Peek();

                            topProgressMonitor.Complete = true; //which will have this monitor object pop itself as well
                        }

                        OnCompleted();
                    }
                }
            }
        }

        /// <summary>
        /// The number of completed steps (between zero and the maximum number of steps).
        /// </summary>
        public int CompletedSteps { get { lock (m_Lock) { return m_CompletedSteps; } } }

        /// <summary>
        /// The current number of monitors in the stack
        /// </summary>
        public int Count { get { lock (m_Lock) { return m_Stack.Count; } } }
 
        /// <summary>
        /// Consider the monitor stack complete and prepare it for immediate disposal.
        /// </summary>
        /// <remarks>Internally this is the same as setting Complete to true.</remarks>
        public void Dispose()
        {
            // Call the underlying implementation
            Dispose(true);

            //SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The current maximum number of steps.
        /// </summary>
        public int MaximumSteps { get { lock (m_Lock) { return m_MaximumSteps; } } }

        /// <summary>
        /// Create a new monitor with the specified owning object, status, and number of steps
        /// </summary>
        /// <remarks>While it is possible to specify zero for the number of steps, it is recommended
        /// that a guess be made for the maximum number of steps to provide the best UI experience.  When in
        /// doubt, guess high and reduce later which will make the progress appear to accelerate.</remarks>
        /// <param name="owner">The object that is responsible for the process being tracked (for development purposes, never displayed)</param>
        /// <param name="status">A short status message for the user</param>
        /// <param name="maxSteps">The maximum number of steps in the process being monitored</param>
        /// <returns>A new monitor object which will be at the top of the stack</returns>
        public ProgressMonitor NewMonitor(object owner, string status, int maxSteps)
        {
            lock (m_Lock)
            {
                //if we're read only then big problem.
                if (m_ReadOnly)
                {
                    //we are no longer updatable
                    throw new ReadOnlyException("The monitor has been marked complete and is now read only.");
                }

                //create a new monitor object
                ProgressMonitor newProgressMonitor = new ProgressMonitor(this, owner, status, maxSteps);

                //push it to the top of the stack
                PushMonitor(newProgressMonitor);

                //and return it
                return newProgressMonitor;
            }
        }

        /// <summary>
        /// A user display message for the current status.
        /// </summary>
        public string StatusMessage { get { lock (m_Lock) { return m_StatusMessage; } } }

        /// <summary>
        /// An end-user display title of the overall process being monitored
        /// </summary>
        public string Title { get { return m_Title; } }

        /// <summary>
        /// Indicates if the progress monitor stack was canceled.
        /// </summary>
        public bool IsCanceled { get { lock (m_Lock) { return m_Canceled; } } }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Raised by the progress monitor stack when canceling the operation being monitored/
        /// </summary>
        /// <remarks>If overriding this method, be sure to call Base.OnCanceled to ensure that the event is still raised to its caller.</remarks>
        protected virtual void OnCanceled()
        {
            ProgressMonitorStackEventArgs e = new ProgressMonitorStackEventArgs(this, m_StatusMessage, m_CompletedSteps, m_MaximumSteps);

            //save the delegate field in a temporary field for thread safety
            EventHandler<ProgressMonitorStackEventArgs> tempEvent = Canceled;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        /// <summary>
        /// Called when the ProgressMonitor that is the top monitor is changed.
        /// </summary>
        /// <remarks>If overriding this method, be sure to call Base.OnChanged to ensure that the event is still raised to its caller.</remarks>
        protected virtual void OnChanged()
        {
            ProgressMonitor curTopMonitor = null;

            //if we have a current top monitor, we want to pass that along, but be careful in case we don't.
            if (m_Stack.Count > 0)
            {
                curTopMonitor = m_Stack.Peek();
            }

            //Notice that this is a different event argument type than we use in all our other events.
            ProgressMonitorEventArgs e = new ProgressMonitorEventArgs(this, curTopMonitor);

            //save the delegate field in a temporary field for thread safety
            EventHandler<ProgressMonitorEventArgs> tempEvent = Changed;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        /// <summary>
        /// Called to raise the Completed event when the task being monitored has been completed.
        /// </summary>
        /// <remarks>If overriding this method, be sure to call Base.OnCompleted to ensure that the event is still raised to its caller.</remarks>
        protected virtual void OnCompleted()
        {
            ProgressMonitorStackEventArgs e = new ProgressMonitorStackEventArgs(this, m_StatusMessage, m_CompletedSteps, m_MaximumSteps);

            //save the delegate field in a temporary field for thread safety
            EventHandler<ProgressMonitorStackEventArgs> tempEvent = Completed;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        /// <summary>
        /// Called to raise the Updated event whenever the progress information (message, steps, etc.) is updated.
        /// </summary>
        /// <remarks>If overriding this method, be sure to call Base.OnUpdated to ensure that the event is still raised to its caller.</remarks>
        protected virtual void OnUpdated()
        {
            ProgressMonitorStackEventArgs e = new ProgressMonitorStackEventArgs(this, m_StatusMessage, m_CompletedSteps, m_MaximumSteps);

            //save the delegate field in a temporary field for thread safety
            EventHandler<ProgressMonitorStackEventArgs> tempEvent = Updated;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// </summary>
        /// <remarks>
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.
        /// </remarks>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (releaseManaged)
            {
                // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                // Other objects may be referenced in this case

                // mark ourself complete, which will perform appropriate cleanup
                if (!m_ReadOnly)
                    Complete = true;
            }
            // Free native resources here (alloc's, etc)
            // May be called from within the finalizer, so don't reference other objects here
        }

        /// <summary>
        /// Called whenever the information that drives progress should be updated.
        /// </summary>
        protected virtual void UpdateProgress()
        {
            if (m_ReadOnly)
                return; //we aren't going to recalculate progress if we're already in a read-only state (completed or canceled)

            //we store everything in working variables so we can make one determination of there were any changes.
            string statusMessage = string.Empty; //just so we never get a null
            int completedSteps;
            int maximumSteps;

            //If we don't have any items in the stack, clear everything.
            if (m_Stack.Count == 0)
            {
                statusMessage = m_Title;
                completedSteps = 0;
                maximumSteps = 0;
            }
            else
            {
                //we do a fair amount of passing through the stack, so get an array of it.
                ProgressMonitor[] monitors = m_Stack.ToArray();

                //The status message is always the first message that is set going from the end of the stack back, which
                //means since this is a stack we can iterate FORWARD to get it.
                for (int curMonitorIndex = 0; curMonitorIndex < monitors.Length; curMonitorIndex++)
                {
                    ProgressMonitor curMonitor = monitors[curMonitorIndex];
                    if (string.IsNullOrEmpty(curMonitor.StatusMessage) == false)
                    {
                        //here we are, our first non-null status messages
                        statusMessage = curMonitor.StatusMessage;
                        break;
                    }
                }


                //If the base item on the stack has zero steps, we aren't ready to make a progress bar yet (and we'll display the marquis)
                if (monitors[monitors.Length - 1].MaxSteps == 0) //remember - it's a stack, so the last item in it is the first item put in it
                {
                    completedSteps = 0;
                    maximumSteps = 0;
                }
                else
                {
                    //figure out the current maximum number of steps and current steps by moving through the stack
                    int curMonitorWindowStart = 0, curMonitorWindowEnd = 100;

                    //now we have to tail first iterate our array so we are going from the first item to the last item added.
                    for (int curMonitorIndex = monitors.Length - 1; curMonitorIndex >= 0; curMonitorIndex--)
                    {
                        ProgressMonitor curMonitor = monitors[curMonitorIndex];

                        //scale the # of steps for this monitor into the current window
                        if (curMonitorWindowEnd == curMonitorWindowStart)
                        {
                            //the window is zero, it doesn't matter.  We can't represent any more resolution
                            break;
                        }

                        if (curMonitor.MaxSteps == 0)
                        {
                            //the monitor we're on doesn't have any resolution to it, we can't subset our window, keep drilling in.
                            //(so really nothing to do)
                        }
                        else
                        {
                            //divide the current window based on the number of steps we have to determine the new window for our current step
                            double interval = ((curMonitorWindowEnd - curMonitorWindowStart) / (double)curMonitor.MaxSteps);
                            
                            //and how far in are we?
                            curMonitorWindowStart += (int)(interval * curMonitor.CompletedSteps);
                            curMonitorWindowEnd = (int)(curMonitorWindowStart + interval);
                        }
                    }   

                    //whatever the current window start is, that's where we are (it represents where the next drill-in monitor would start)
                    completedSteps = curMonitorWindowStart;
                    maximumSteps = DefaultMaximumSteps;
                    
                    //OK, we don't want to march backwards which can happen transiently in some cases.
                    if ((completedSteps / (double) maximumSteps) < (m_CompletedSteps / (double) m_MaximumSteps))
                    {
                        //leave what we have alone.
                        completedSteps = m_CompletedSteps;
                        maximumSteps = m_MaximumSteps;
                    }
                }
            }


            //Did we change anything?
            if ((m_StatusMessage != statusMessage) || (m_CompletedSteps != completedSteps) || (m_MaximumSteps != maximumSteps))
            {
                //we did. Set it all and we'll have to let people know that the progress changed.
                m_StatusMessage = statusMessage;
                m_CompletedSteps = completedSteps;
                m_MaximumSteps = maximumSteps;

                OnUpdated();
            }
        }

        #endregion

        #region Internal Properties and Methods

        internal void PopMonitor(ProgressMonitor progressMonitor)
        {
            lock (m_Lock)
            {
                //if the provided monitor is a current monitor we need to pop down to the specified monitor
                if (m_Stack.Contains(progressMonitor))
                {
                    ProgressMonitor topProgressMonitor = m_Stack.Pop();

                    //clear our top monitor event monitor, it's whatever we last bound to.
                    topProgressMonitor.Updated -= TopMonitor_MonitorUpdated;

                    //but they may be popping off from a higher part of the monitor stack, so we need to
                    //keep going up until we get to the right one.
                    while (topProgressMonitor != progressMonitor)
                    {
                        topProgressMonitor = m_Stack.Pop();
                    }

                    //What we do now depends on whether that was the last progress monitor or not
                    if (m_Stack.Count == 0)
                    {
                        //but wait - are we already in a completed or cancel process?
                        if ((m_ReadOnly == false) && (m_Complete == false))
                        {
                            //we need to mark ourself as complete.  Note to maintainers:  Be careful to 
                            //not create an infinite loop when changing complete or here.  It's important
                            //that this is after the pop we did above.
                            Complete = true;   
                        }
                    }
                    else
                    {
                        //we are now at a different point in the overall progress.
                        UpdateProgress();

                        //and since we've changed our top monitor, we need to raise our event.
                        OnChanged();

                        //and now switch our top monitor event subscription.
                        topProgressMonitor = m_Stack.Peek();
                        topProgressMonitor.Updated += TopMonitor_MonitorUpdated;
                    }
                }
            }
        }

        internal void PushMonitor(ProgressMonitor progressMonitor)
        {
            lock (m_Lock)
            {
                //if we have a top monitor we need to unsubscribe from its events
                ProgressMonitor topProgressMonitor;
                if (m_Stack.Count > 0)
                {
                    topProgressMonitor = m_Stack.Peek();
                    topProgressMonitor.Updated -= TopMonitor_MonitorUpdated;
                }

                m_Stack.Push(progressMonitor);

                //we are now at a different point in the overall progress.
                UpdateProgress();

                //and since we've changed our top monitor, we need to raise our event.
                OnChanged();

                topProgressMonitor = m_Stack.Peek();
                topProgressMonitor.Updated += TopMonitor_MonitorUpdated;
            }
        }

        #endregion

        #region Private Properties and methods

        private void TopMonitor_MonitorUpdated(object sender, ProgressMonitorEventArgs e)
        {
            //when we are notified that the top monitor changed we need to update our progress
            UpdateProgress();
        }

        #endregion 
    }
}