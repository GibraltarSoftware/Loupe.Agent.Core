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
            Log.PrincipalResolver = new DefaultPrincipalResolver();
            Log.ApplicationUserProvider = new ResolveUserForCurrentPrincipal();
            try
            {
                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Assign For Current Principal", "This message should be attributed to the current user",
                    "And we should get the resolution event following it.");
            }
            finally
            {
                Log.PrincipalResolver = null;
                Log.ApplicationUserProvider = null;
            }
        }

        #region Private Class ResolveUserForCurrentPrincipal

        private class ResolveUserForCurrentPrincipal : IApplicationUserProvider
        {
            public bool TryGetApplicationUser(IPrincipal principal, Lazy<ApplicationUser> applicationUser)
            {
                //this is really a quite poor set of data for a user - you wouldn't want to touch the environment
                //and naturally these details wouldn't be hard-coded.
                var newUser = applicationUser.Value;
                newUser.Caption = Environment.GetEnvironmentVariable("USERNAME");
                newUser.Organization = "Unit test";
                newUser.EmailAddress = "support@gibraltarsoftware.com";
                newUser.Phone = "443 738-0680";
                newUser.Properties.Add("Customer Key", "1234-5678-90");
                newUser.Properties.Add("License Check", null);

                return true;
            }
        }

        #endregion

        [Test]
        public void ApplicationUserAssignJustOnce()
        {
            try
            {
                //the first message is done with wait for commit so we know we've written everything through the publisher before we connect up our event handler.
                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Assign Just Once", "Flushing message queue prior to doing resolve once test",
                    "We should get no resolution as we shouldn't have the resolver bound yet.");

                var principal = new GenericPrincipal(new GenericIdentity(Guid.NewGuid().ToString()), null); //we want a unique, but consistent, principal.

                var justOnceProvider = new ResolveUserJustOnceProvider();
                Log.ApplicationUserProvider = justOnceProvider;
                
                Log.Write(LogMessageSeverity.Information, "Loupe", null, principal, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Assign Just Once",
                    "This message should be attributed to ApplicationUserAssignJustOnce",
                    "And we should get the resolution event following it.");
                Assert.AreEqual(1, justOnceProvider.ResolutionRequests, "We didn't get exactly one resolution after the first message");

                Log.Write(LogMessageSeverity.Information, "Loupe", null, principal, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Assign Just Once",
                    "This message should be attributed to ApplicationUserAssignJustOnce",
                    "And we should NOT get the resolution event following it.");
                Assert.AreEqual(1, justOnceProvider.ResolutionRequests, "We got an additional ResolveApplicationUser event after our initial attempt.");
            }
            finally
            {
                Log.ApplicationUserProvider = null;
            }
        }

        #region Private Class ResolveUserJustOnceResolver 

        private class ResolveUserJustOnceProvider : IApplicationUserProvider
        {
            private volatile int m_ResolutionRequests;

            public bool TryGetApplicationUser(IPrincipal principal, Lazy<ApplicationUser> applicationUser)
            {
                Interlocked.Increment(ref m_ResolutionRequests);

                var user = applicationUser.Value;
                user.Caption = principal?.Identity?.Name;

                return true;
            }

            public int ResolutionRequests => m_ResolutionRequests;
        }

        #endregion

        #region Private Class RandomPrincipalResolver

        /// <summary>
        /// Create a unique principle for each request to force user name resolution.
        /// </summary>
        private class RandomPrincipalResolver : IPrincipalResolver
        {
            public bool TryResolveCurrentPrincipal(out IPrincipal principal)
            {
                principal = new GenericPrincipal(new GenericIdentity(Guid.NewGuid().ToString()), null);
                return true;
            }
        }

        #endregion

        [Test]
        public void Log_While_Resolving_Principal_Does_Not_Deadlock()
        {
            try
            {
                //the first message is done with wait for commit so we know we've written everything through the publisher before we connect up our event handler.
                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Logging Doesn't Deadlock",
                    "Flushing message queue prior to doing deadlock test",
                    null);

                var principalResolver = new LoggingPrincipalResolver();
                Log.PrincipalResolver = principalResolver;
                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Logging Doesn't Deadlock",
                    "This message should be attributed to ApplicationUserAssignJustOnce",
                    "And we should get the resolution event following it.");
                Assert.AreEqual(1, principalResolver.ResolutionRequests, "We didn't get exactly one resolution after the first message");
            }
            finally
            {
                Log.PrincipalResolver = null;
            }
        }

        #region Private Class LoggingPrincipalResolver

        /// <summary>
        /// Log to Loupe while creating a unique principle for each request to force user name resolution
        /// </summary>
        private class LoggingPrincipalResolver : IPrincipalResolver
        {
            private volatile int m_ResolutionRequests;

            public bool TryResolveCurrentPrincipal(out IPrincipal principal)
            {
                principal = new GenericPrincipal(new GenericIdentity(DateTime.UtcNow.ToLongTimeString()), null);
                Interlocked.Increment(ref m_ResolutionRequests);

                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Logging Principal",
                    "This message was logged during an IPrincipal Resolve method", 
                    "It should not have a user principal and should not prompt resolution of a user principal.");

                return true;
            }

            public int ResolutionRequests => m_ResolutionRequests;
        }

        #endregion

        [Test]
        public void Log_While_Resolving_Application_User_Does_Not_Deadlock()
        {
            try
            {
                //the first message is done with wait for commit so we know we've written everything through the publisher before we connect up our event handler.
                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Logging Doesn't Deadlock",
                    "Flushing message queue prior to doing deadlock test",
                    null);

                var userProvider = new LoggingUserResolver();
                Log.PrincipalResolver = new RandomPrincipalResolver();
                Log.ApplicationUserProvider = userProvider;

                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Logging Doesn't Deadlock",
                    "This message should be attributed to ApplicationUserAssignJustOnce",
                    "And we should get the resolution event following it.");
            }
            finally
            {
                Log.PrincipalResolver = null;
                Log.ApplicationUserProvider = null;
            }
        }

        #region Private Class LoggingUserResolver 

        private class LoggingUserResolver : IApplicationUserProvider
        {
            private volatile int m_ResolutionRequests;

            public bool TryGetApplicationUser(IPrincipal principal, Lazy<ApplicationUser> applicationUser)
            {
                Interlocked.Increment(ref m_ResolutionRequests);

                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Logging User",
                    "This message was logged during an IApplicationUserProvider Resolve method",
                    "It should not have an application user.");

                var user = applicationUser.Value;
                return true;
            }

            public int ResolutionRequests => m_ResolutionRequests;
        }

        #endregion

        [Test]
        public void Can_Use_Lambda_For_Principal_Resolver()
        {
            try
            {
                //the first message is done with wait for commit so we know we've written everything through the publisher before we connect up our event handler.
                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Lambda Principal Resolver", "Flushing message queue prior to doing resolve once test",
                    "We should get no resolution as we shouldn't have the resolver bound yet.");

                Log.PrincipalResolver = new DelegatePrincipalResolver(() => new GenericPrincipal(new GenericIdentity("Can_Use_Lambda_For_Principal_Resolver"), null));
                var justOnceProvider = new ResolveUserJustOnceProvider();
                Log.ApplicationUserProvider = justOnceProvider;

                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Lambda Principal Resolver",
                    "This message should be attributed to user Can_Use_Lambda_For_Principal_Resolver",
                    "And we should get the resolution event following it.");
                Assert.AreEqual(1, justOnceProvider.ResolutionRequests, "We didn't get exactly one resolution after the first message");

                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Lambda Principal Resolver",
                    "This message should be attributed to user Can_Use_Lambda_For_Principal_Resolver",
                    "And we should NOT get the resolution event following it.");
                Assert.AreEqual(1, justOnceProvider.ResolutionRequests, "We got an additional ResolveApplicationUser event after our initial attempt.");
            }
            finally
            {
                Log.PrincipalResolver = null;
                Log.ApplicationUserProvider = null;
            }
        }

        [Test]
        public void Can_Use_Lambda_For_Application_User_Provider()
        {
            try
            {
                //the first message is done with wait for commit so we know we've written everything through the publisher before we connect up our resolvers.
                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Lambda Application User Provider",
                    "Flushing message queue prior to doing test",
                    "We should get no resolution as we shouldn't have the resolver bound yet.");

                Log.PrincipalResolver = new RandomPrincipalResolver();
                Log.ApplicationUserProvider = new DelegateApplicationUserProvider((principal, lazy) =>
                {
                    var user = lazy.Value;
                    user.Caption = "Can_Use_Lambda_For_Application_User_Provider";
                    return true;
                });

                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Lambda Application User Provider",
                    "This message should be attributed to user caption Can_Use_Lambda_For_Application_User_Provider",
                    "And we should get the resolution event following it.");
            }
            finally
            {
                Log.PrincipalResolver = null;
                Log.ApplicationUserProvider = null;
            }
        }

        [Test]
        public void Specified_IPrincipal_Overrides_Resolved_IPrincipal()
        {
            try
            {
                //the first message is done with wait for commit so we know we've written everything through the publisher before we connect up our resolvers.
                Log.Write(LogMessageSeverity.Information, "Loupe", null, null, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Override IPrincipal",
                    "Flushing message queue prior to doing test",
                    "We should get no resolution as we shouldn't have the resolver bound yet.");

                //Test scenario: Have a principal resolver, but specify something *else* manually to the API.
                var principal =
                    new GenericPrincipal(
                        new GenericIdentity("Specified_IPrincipal_Overrides_Resolved_IPrincipal-" + Guid.NewGuid()),
                        null);

                Log.PrincipalResolver = new RandomPrincipalResolver();
                var userProvider = new CapturePrincipalUserProvider();
                Log.ApplicationUserProvider = userProvider;

                Log.Write(LogMessageSeverity.Information, "Loupe", null, principal, null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Override IPrincipal",
                    "This message should be attributed to user caption Specified_IPrincipal_Overrides_Resolved_IPrincipal",
                    "And we should get the resolution event following it.");

                Assert.That(userProvider.LastPrincipal, Is.Not.Null);
                Assert.That(userProvider.LastPrincipal, Is.SameAs(principal), "The user principal was not the object we expected");
            }
            finally
            {
                Log.PrincipalResolver = null;
                Log.ApplicationUserProvider = null;
            }
        }

        private class CapturePrincipalUserProvider : IApplicationUserProvider
        {
            public IPrincipal LastPrincipal { get; set; }

            public bool TryGetApplicationUser(IPrincipal principal, Lazy<ApplicationUser> applicationUser)
            {
                LastPrincipal = principal;
                var user = applicationUser.Value;
                return true;
            }
        }
    }
}
