using System;
using System.Security.Principal;
using Gibraltar.Messaging;
using Gibraltar.Monitor.Internal;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// Monitors packets going through the publisher to add user information as needed.
    /// </summary>
    public class UserResolutionNotifier : IDisposable
    {
        private static readonly ApplicationUserCollection s_Users = new ApplicationUserCollection();

        /// <summary>
        /// Handler for the ResolveUser event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void ResolveUserHandler(object sender, ResolveUserEventArgs e);

        /// <summary>
        /// Raised whenever a log message is submitted with an unknown user.
        /// </summary>
        /// <remarks>If you provide an ApplicationUser for the requested user name that user will
        /// be cached and not requested again for the duration of the process.</remarks>
        public event ResolveUserHandler ResolveUser;

        /// <summary>
        /// Create a new instance of the user resolution notifier
        /// </summary>
        public UserResolutionNotifier(bool anonymousMode)
        {            
            if (anonymousMode == false)
                Publisher.MessageDispatching += PublisherOnMessageDispatching;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Publisher.MessageDispatching -= PublisherOnMessageDispatching;

            GC.SuppressFinalize(this);
        }

        private void PublisherOnMessageDispatching(object sender, PacketEventArgs e)
        {
            var logPacket = e.Packet as LogMessagePacket;
            if (ReferenceEquals(logPacket, null))
                return; //that's the only type of packet we care about, time to fast bail.

            //if we don't have a user name at all then there's nothing to do
            if (string.IsNullOrEmpty(logPacket.UserName))
                return;

            //now lookup the ApplicationUser to see if we have it already mapped..            
            var user = GetCurrentApplicationUser(logPacket.UserName, logPacket.UserPrincipal, logPacket.Timestamp, logPacket.Sequence);
            if (user != null)
            {
                logPacket.UserPacket = user.Packet;
            }
        }

        [ThreadStatic]
        private static bool t_InResolveUserEvent;

        private ApplicationUser GetCurrentApplicationUser(string userName, IPrincipal principal, DateTimeOffset timestamp, long sequence)
        {
            //prevent infinite recursion
            if (t_InResolveUserEvent)
                return null;

            var tempEvent = ResolveUser;
            if ((tempEvent == null) && (s_Users.Count == 0))
                return null; //we have nothing we can resolve...

            if (string.IsNullOrEmpty(userName))
                return null; //should never happen, we always have a user name BUT...

            ApplicationUser applicationUser;
            if (s_Users.TryFindUserName(userName, out applicationUser) == false)
            {
                if (tempEvent == null)
                    return null; //there's no way for us to resolve this missing item...

                //since we have a miss we want to give our event subscribers a shot..
                var resolveEventArgs = new ResolveUserEventArgs(userName, principal, timestamp, sequence);
                try
                {
                    t_InResolveUserEvent = true;
                    tempEvent.Invoke(null, resolveEventArgs);
                }
                catch (Exception ex)
                {
                    //we can't log this because that would create an infinite loop (ignoring our protection for same)
                    GC.KeepAlive(ex);
                }
                finally
                {
                    t_InResolveUserEvent = false;
                }

                applicationUser = resolveEventArgs.User;
                if (applicationUser != null)
                {
                    //cache this so we don't keep going after it.
                    applicationUser = s_Users.TrySetValue(applicationUser);
                }
            }

            return applicationUser;
        }

    }
}
