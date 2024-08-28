using System;
using System.Diagnostics;
using System.Threading;

namespace Gibraltar
{
    /// <summary>
    /// Execute an asynchronous execution task without any user interface.
    /// </summary>
    public class AsynchronousTask : IAsynchronousTask
    {
        private ProgressMonitorStack m_ProgressMonitors;
        private bool m_ReadOnly; //prevents subsequent events from displaying if they come after our canceled or complete event.

        /// <inheritdoc />
        public void Execute(WaitCallback callBack, string title, object state)
        {
            Execute(callBack, new AsyncTaskArguments(title, state), true);
        }

        /// <inheritdoc />
        public bool Execute(WaitCallback callBack, AsyncTaskArguments arguments)
        {
            return Execute(callBack, arguments, true);
        }

        /// <inheritdoc />
        public bool Execute(WaitCallback callBack, AsyncTaskArguments arguments, bool cancelable)
        {
            m_ProgressMonitors = arguments.ProgressMonitors;

            //and initialize our different UI elements
            m_ProgressMonitors.Canceled += Monitors_Canceled;
            m_ProgressMonitors.Completed += Monitors_Completed;

            //we are going to have the thread pool asynchronously execute our async interface.

            ThreadPool.QueueUserWorkItem(AsyncTaskExec, new object[] { callBack, arguments });

            //and we return right away.  The whole point is that the caller can check status as they wish.
            return false;
        }

        /// <summary>
        /// True if the task is completed and was canceled by the user.  False if the task is still executing or was not canceled.
        /// </summary>
        public bool Canceled { get; private set; }

        /// <summary>
        /// True if the task has completed executing.  Check Canceled to determine if it completed successfully or not.
        /// </summary>
        public bool Completed { get; private set; }

        /// <summary>
        /// Optional. Extended information about the result of the task once it is complete.
        /// </summary>
        public AsyncTaskResultEventArgs TaskResults { get; private set; }


        /// <summary>
        /// Executes the application's callback function
        /// </summary>
        /// <remarks>This lets us better control exception handling and make sure the delegate is complete
        /// before we return to our caller while still allowing us to use the thread pool.</remarks>
        private void AsyncTaskExec(object stateInfo)
        {
            try //because we're called from the threadpool we have to catch exceptions.
            {

                //unbundle the state information
                object[] asyncExecParams = (object[])stateInfo;
                WaitCallback callBack = (WaitCallback)asyncExecParams[0];
                AsyncTaskArguments arguments = (AsyncTaskArguments)asyncExecParams[1];

                if ((callBack == null) || (arguments == null))
                {
                    //we can't execute, this isn't a valid state
                    Trace.TraceError("Unable to execute progress dialog task because one of the arguments is null.  This represents an implementation defect.");
                    return;
                }

                callBack.Invoke(arguments);
                CompleteProgress(); //assume if the caller did nothing then we completed.  Ensures we always set our complete state.

                //and set our result.
                TaskResults = arguments.TaskResult;
            }
            catch (Exception ex)
            {
                try //while it seems really unlikely we'll get an exception here, we need to be extra cautious because we're called from the tread pool
                {
#if DEBUG
                    Trace.TraceError("User task threw exception {0}:\r\n{1}", ex.GetType().Name, ex.Message, ex);
#endif
                    //set a result..
                    TaskResults = new AsyncTaskResultEventArgs(AsyncTaskResult.Error, ex.Message, ex);
                    CancelProgress(); //if we don't the caller will spin forever.
                }
                catch
                {
                }
            }
        }


        private void CancelProgress()
        {
            //make sure we haven't already completed or canceled.  Ignore out of order events.
            if (m_ReadOnly)
                return;

            //mark we canceled so we return the right thing to our caller
            Canceled = true;
            m_ReadOnly = true; //to prevent subsequent events from messing with us.
            Completed = true;
        }

        private void CompleteProgress()
        {
            //make sure we haven't already completed or canceled.  Ignore out of order events.
            if (m_ReadOnly)
                return;

            m_ReadOnly = true; //to prevent subsequent events from messing with us.
            Completed = true;
        }


        private void Monitors_Canceled(object sender, ProgressMonitorStackEventArgs e)
        {
            CancelProgress();
        }

        private void Monitors_Completed(object sender, ProgressMonitorStackEventArgs e)
        {
            CompleteProgress();
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
