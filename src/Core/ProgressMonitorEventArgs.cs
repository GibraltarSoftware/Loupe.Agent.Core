using System;



namespace Gibraltar
{
    /// <summary>
    /// Information about monitor changes
    /// </summary>
    public class ProgressMonitorEventArgs : EventArgs
    {
        private readonly ProgressMonitorStack m_ProgressMonitors;
        private readonly ProgressMonitor m_ProgressMonitor;

        /// <summary>
        /// Create a new monitor changed event arguments object.
        /// </summary>
        /// <param name="progressMonitors">The monitor stack that changed.</param>
        /// <param name="progressMonitor">The monitor object (if any) affected by the change.</param>
        public ProgressMonitorEventArgs(ProgressMonitorStack progressMonitors, ProgressMonitor progressMonitor)
        {
            m_ProgressMonitors = progressMonitors;
            m_ProgressMonitor = progressMonitor;
        }

        /// <summary>
        /// The stack of all monitors currently in use.
        /// </summary>
        public ProgressMonitorStack ProgressMonitors
        {
            get { return m_ProgressMonitors; }
        }

        /// <summary>
        /// The monitor that was changed (may not be the top monitor on the stack)
        /// </summary>
        public ProgressMonitor ProgressMonitor
        {
            get { return m_ProgressMonitor; }
        }
    }

}
