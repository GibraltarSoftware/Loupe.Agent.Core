using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Gibraltar.Agent;
using Loupe.Configuration;
using NUnit.Framework;

namespace Loupe.Agent.Test
{
    [SetUpFixture]
    public class SetUp
    {
        private AgentConfiguration m_Configuration;

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            //delete the existing local logs folder for us...
            try
            {
                var path = Path.Combine(Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ProgramData" : "Home"), @"Gibraltar\Local Logs\NUnit");
                Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Unable to clean out local logs directory due to " + ex.GetType());
            }

            Log.Initializing += Log_Initializing;
            m_Configuration = new AgentConfiguration();
            PublisherConfiguration publisher = m_Configuration.Publisher;
            publisher.ProductName = "NUnit";
            publisher.ApplicationName = "Gibraltar.Agent.Test";

            //and now try to get the file version.  This is risky.
            var fileVersionAttributes = this.GetType().GetTypeInfo().Assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute));

            if (fileVersionAttributes != null)
            {
                AssemblyFileVersionAttribute leadAttribute = fileVersionAttributes.FirstOrDefault() as AssemblyFileVersionAttribute;

                if (leadAttribute != null)
                {
                    publisher.ApplicationVersion = new Version(leadAttribute.Version);
                }
            }

            publisher.ApplicationDescription = "NUnit tests of the Loupe Agent Library";

            m_Configuration.SessionFile.EnableFilePruning = false;

            //if we need email server information set that
#if CONFIGURE_EMAIL
            EmailConfiguration email = e.Configuration.Email;
            email.Server = EmailServer;
            email.Port = EmailServerPort;
            email.User = EmailServerUser;
            email.Password = EmailServerPassword;
            email.UseSsl = EmailUseSsl;

            PackagerConfiguration packager = e.Configuration.Packager;
            packager.DestinationEmailAddress = EmailToAddress;
            packager.FromEmailAddress = EmailFromAddress;
#endif

            //force us to initialize logging
            Log.StartSession(m_Configuration);
            Trace.TraceInformation("Starting testing at {0} on computer {1}", DateTimeOffset.UtcNow, Log.SessionSummary.HostName);
        }

        void Log_Initializing(object sender, LogInitializingEventArgs e)
        {
            //lets spot check a few values to be sure they're the same.
            Assert.AreEqual(m_Configuration.Publisher.ProductName, e.Configuration.Publisher.ProductName);
            Assert.AreEqual(m_Configuration.Publisher.ApplicationName, e.Configuration.Publisher.ApplicationName);
            Assert.AreEqual(m_Configuration.Publisher.ApplicationVersion, e.Configuration.Publisher.ApplicationVersion);
            Assert.AreEqual(m_Configuration.SessionFile.EnableFilePruning, e.Configuration.SessionFile.EnableFilePruning);
        }        

        [OneTimeTearDown]
        public void RunAfterAllTests()
        {
            //Tell our central log session we're shutting down nicely
            Log.EndSession();
        }
    }
}
