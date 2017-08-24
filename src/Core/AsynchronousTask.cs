using System;
using System.Threading;

namespace Gibraltar
{
    /// <summary>
    /// Execute an asynchronous execution task without any user interface.
    /// </summary>
    public class AsynchronousTask
    {
        private ProgressMonitorStack m_ProgressMonitors;
        private bool m_ReadOnly; //prevents subsequent events from displaying if they come after our canceled or complete event.

        #region Public Properties and Methods

        /// <summary>
        /// Execute the requested delegate asynchronously with the specified arguments.
        /// </summary>
        /// <remarks>A progress dialog is displayed after a few moments and updated asynchronously as the task continues.  If the user
        /// elects ot cancel the task, execution attempts to stop immediately and True is returned indicating the user canceled.</remarks>
        /// <param name="callBack">The method to be executed asynchronously</param>
        /// <param name="title">An end-user display title for this task.</param>
        /// <param name="state">Arguments to pass to the callBack delegate</param>
        public void Execute(WaitCallback callBack, string title, object state)
        {
            AsyncTaskArguments arguments = new AsyncTaskArguments(title, state);
            m_ProgressMonitors = arguments.ProgressMonitors;

            //and initialize our different UI elements
            m_ProgressMonitors.Canceled += Monitors_Canceled;
            m_ProgressMonitors.Completed += Monitors_Completed;

            //we are going to have the thread pool asynchronously execute our async interface.

            ThreadPool.QueueUserWorkItem(AsyncTaskExec, new object[] {callBack, arguments});

            //and we return right away.  The whole point is that the caller can check status as they wish.
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

        #endregion


        #region Private Properties and Methods

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
                    return;
                }

                callBack.Invoke(arguments);
                CompleteProgress(); //assume if the caller did nothing then we completed.  Ensures we always set our complete state.

                //and set our result.
                TaskResults = arguments.TaskResult;
            }
            catch (Exception ex)
            {
                try //while it seems really unlikely we'll get an exception here, we need to be extra cautious beacuse we're called from the tread pool
                {
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

        #endregion


        #region Event Handlers

        private void Monitors_Canceled(object sender, ProgressMonitorStackEventArgs e)
        {
            CancelProgress();
        }

        private void Monitors_Completed(object sender, ProgressMonitorStackEventArgs e)
        {
            CompleteProgress();
        }

        #endregion
    }
}
