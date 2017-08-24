using System;
using System.Security.Principal;
using Gibraltar.Agent.Data;
using Gibraltar.Monitor;

namespace Gibraltar.Agent
{
    /// <summary>
    /// Arguments for the Log.ResolveApplicationUser event
    /// </summary>
    public class ApplicationUserResolutionEventArgs : EventArgs
    {
        private readonly ResolveUserEventArgs m_WrappedEventArgs;
        private Data.IApplicationUser m_ApplicationUser;

        internal ApplicationUserResolutionEventArgs(ResolveUserEventArgs wrappedEventArgs)
        {
            m_WrappedEventArgs = wrappedEventArgs;
        }

        /// <summary>
        /// The user name being resolved
        /// </summary>
        /// <remarks>This value is treated as a key for the duration of the current session.  If an ApplicationUser
        /// object is returned from this event it will be associated with this user and the event will not be raised
        /// again for this user name for the duration of this session.</remarks>
        public string UserName { get { return m_WrappedEventArgs.UserName; } }

        /// <summary>
        /// The current thread principal that often contains additional information about the current user
        /// </summary>
        /// <remarks>Most authentication systems will add additional user information here which you can
        /// map to the ApplicationUser object</remarks>
        public IPrincipal Principal { get { return m_WrappedEventArgs.Principal; } }

        /// <summary>
        /// The application user being populated for the current user.
        /// </summary>
        /// <remarks>Update this user with the information available.  If this method is called then
        /// the configured user will be stored as the definitive information for this user name.</remarks>
        public IApplicationUser GetUser()
        {
            if (m_ApplicationUser == null)
            {
                m_ApplicationUser = new Internal.ApplicationUser(m_WrappedEventArgs.GetUser());
            }

            return m_ApplicationUser;
        }
    }
}
