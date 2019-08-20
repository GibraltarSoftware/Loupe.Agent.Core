using System;
using System.Security.Principal;

namespace Loupe.Monitor
{
    /// <summary>
    /// Arguments for the ResolveUser Event
    /// </summary>
    public sealed class ResolveUserEventArgs : EventArgs
    {
        private readonly DateTimeOffset m_Timestamp;
        private readonly long m_Sequence;
        private ApplicationUser m_User;

        internal ResolveUserEventArgs(string userName, IPrincipal principal, DateTimeOffset timestamp, long sequence)
        {
            m_Timestamp = timestamp;
            m_Sequence = sequence;
            UserName = userName;
            Principal = principal;
        }

        /// <summary>
        /// The user name being resolved
        /// </summary>
        /// <remarks>This value is treated as a key for the duration of the current session.  If an ApplicationUser
        /// object is returned from this event it will be associated with this user and the event will not be raised
        /// again for this user name for the duration of this session.</remarks>
        public string UserName { get; set; }

        /// <summary>
        /// The current thread principal that often contains additional information about the current user
        /// </summary>
        /// <remarks>Most authentication systems will add additional user information here which you can
        /// map to the ApplicationUser object</remarks>
        public IPrincipal Principal { get; set; }

        /// <summary>
        /// The application user being populated for the current user.
        /// </summary>
        /// <remarks>Update this user with the information available.  If this method is called then
        /// the configured user will be stored as the definitive information for this user name.</remarks>
        public ApplicationUser GetUser()
        {
            if (m_User == null)
                m_User = new ApplicationUser(UserName, m_Timestamp, m_Sequence);

            return m_User;
        }

        /// <summary>
        /// The underlying user object, if it was ever configured
        /// </summary>
        internal ApplicationUser User { get { return m_User; } }
    }
}
