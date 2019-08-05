using System;
using System.Security.Principal;
using System.Threading;
using Gibraltar.Agent;
using Gibraltar.Monitor;
using NUnit.Framework;
using Log = Gibraltar.Agent.Log;
using LogWriteMode = Gibraltar.Agent.LogWriteMode;

namespace Loupe.Agent.Test.LogMessages
{
    [TestFixture]
    public class ApplicationUserTests
    {

        [Test]
        public void ApplicationUserAssignForCurrentPrincipal()
        {
            Log.ApplicationUserResolver = new ResolveUserForCurrentPrincipal();
            try
            {
                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Assign For Current Principal", "This message should be attributed to the current user",
                    "And we should get the resolution event following it.");
            }
            finally
            {
                Log.ApplicationUserResolver = null;
            }
        }


        private class ResolveUserForCurrentPrincipal : IApplicationUserResolver
        {
            public ApplicationUser ResolveApplicationUser(IPrincipal principal, Func<ApplicationUser> userFactory)
            {
                var identity = principal.Identity;
                var newUser = userFactory();
                newUser.Caption = Environment.GetEnvironmentVariable("USERNAME");
                newUser.Organization = "Unit test";
                newUser.EmailAddress = "support@gibraltarsoftware.com";
                newUser.Phone = "443 738-0680";
                newUser.Properties.Add("Customer Key", "1234-5678-90");
                newUser.Properties.Add("License Check", null);

                return newUser;
            }
        }

        [Test]
        public void ApplicationUserAssignJustOnce()
        {
            try
            {
                //the first message is done with wait for commit so we know we've written everything through the publisher before we connect up our event handler.
                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Assign Just Once", "Flushing message queue prior to doing resolve once test",
                    "We should get no resolution as we shouldn't have the resolver bound yet.");

                var justOnceResolver = new ResolveUserJustOnceResolver();

                Log.ApplicationUserResolver = justOnceResolver;
                Log.PrincipalResolver = new RandomPrincipalResolver();
                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Assign Just Once", "This message should be attributed to ApplicationUserAssignJustOnce",
                    "And we should get the resolution event following it.");
                Assert.AreEqual(1, justOnceResolver.ResolutionRequests, "We didn't get exactly one resolution after the first message");

                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Assign Just Once", "This message should be attributed to ApplicationUserAssignJustOnce",
                    "And we should NOT get the resolution event following it.");
                Assert.AreEqual(1, justOnceResolver.ResolutionRequests, "We got an additional ResolveApplicationUser event after our initial attempt.");
            }
            finally
            {
                Log.ApplicationUserResolver = null;
            }
        }


        #region Private Class ResolveUserJustOnceResolver 

        private class ResolveUserJustOnceResolver : IApplicationUserResolver
        {
            private volatile int m_UserResolutionRequests;

            public ApplicationUser ResolveApplicationUser(IPrincipal principal, Func<ApplicationUser> userFactory)
            {
                Interlocked.Increment(ref m_UserResolutionRequests);

                return userFactory();
            }

            public int ResolutionRequests => m_UserResolutionRequests;
        }

        #endregion

        #region Private Class RandomPrincipalResolver

        /// <summary>
        /// Create a unique principle for each request to force user name resolution.
        /// </summary>
        private class RandomPrincipalResolver : IPrincipalResolver
        {
            public IPrincipal ResolveCurrentPrincipal()
            {
                return new GenericPrincipal(new GenericIdentity(DateTime.UtcNow.ToLongTimeString()), null);
            }
        }

        #endregion
    }
}
