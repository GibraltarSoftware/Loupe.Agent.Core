using System;
using Gibraltar.Agent;
using NUnit.Framework;

namespace Loupe.Agent.Test.LogMessages
{
    [TestFixture]
    public class ApplicationUserTests
    {

        [Test]
        public void ApplicationUserAssignForCurrentPrincipal()
        {
            Log.ResolveApplicationUser += OnResolveUserForCurrentPrincipal;
            try
            {
                Log.Write(LogMessageSeverity.Information, "Loupe", null, "ApplicationUserAssignJustOnce", null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Assign For Current Principal", "This message should be attributed to the current user",
                    "And we should get the resolution event following it.");
            }
            finally
            {
                Log.ResolveApplicationUser -= OnResolveUserForCurrentPrincipal;
            }
        }

        private void OnResolveUserForCurrentPrincipal(object sender, ApplicationUserResolutionEventArgs e)
        {
            var identity = e.Principal?.Identity;
            var userName = e.UserName;
            var newUser = e.GetUser();
            newUser.Caption = Environment.GetEnvironmentVariable("USERNAME"); 
            newUser.Organization = "Unit test";
            newUser.EmailAddress = "support@gibraltarsoftware.com";
            newUser.Phone = "443 738-0680";
            newUser.Properties.Add("Customer Key", "1234-5678-90");
            newUser.Properties.Add("License Check", null);
        }

        [Test]
        public void ApplicationUserAssignJustOnce()
        {
            try
            {
                //the first message is done with wait for commit so we know we've written everything through the publisher before we connect up our event handler.
                Log.Write(LogMessageSeverity.Information, "Loupe", null, "ApplicationUserAssignJustOnce", null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Assign Just Once", "Flushing message queue prior to doing resolve once test",
                    "We should get no resolution as we shouldn't have the resolver bound yet.");

                Log.ResolveApplicationUser += OnResolveUserJustOnce;
                m_UserResolutionRequests = 0;
                Log.Write(LogMessageSeverity.Information, "Loupe", null, "ApplicationUserAssignJustOnce", null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Assign Just Once", "This message should be attributed to ApplicationUserAssignJustOnce",
                    "And we should get the resolution event following it.");
                Assert.AreEqual(1, m_UserResolutionRequests, "We didn't get exactly one resolution after the first message");

                Log.Write(LogMessageSeverity.Information, "Loupe", null, "ApplicationUserAssignJustOnce", null,
                    LogWriteMode.WaitForCommit, null, "LogTests.ApplicationUser.Assign Just Once", "This message should be attributed to ApplicationUserAssignJustOnce",
                    "And we should NOT get the resolution event following it.");
                Assert.AreEqual(1, m_UserResolutionRequests, "We didn't get exactly one resolution after the second message");
            }
            finally
            {
                Log.ResolveApplicationUser -= OnResolveUserJustOnce;
            }
        }

        private void OnResolveUserJustOnce(object sender, ApplicationUserResolutionEventArgs e)
        {
            m_UserResolutionRequests++;

            //all I have to do to lock in the user is get it..
            var newUser = e.GetUser();
        }

        private volatile int m_UserResolutionRequests;
    }
}
