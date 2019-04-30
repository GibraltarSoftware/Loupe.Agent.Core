using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Gibraltar.Agent;
using Loupe.Agent.PerformanceCounters;
using Loupe.Configuration;
using NUnit.Framework;

namespace Loupe.PerformanceCounters.Tests
{
    [SetUpFixture]
    public class SetUp
    {
        private AgentConfiguration m_Configuration;

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Log.Initializing += Log_Initializing;
            m_Configuration = new AgentConfiguration();
            PublisherConfiguration publisher = m_Configuration.Publisher;
            publisher.ProductName = "NUnit";
            publisher.ApplicationName = "Loupe.PerformanceCounters.Tests";

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

            publisher.ApplicationDescription = "NUnit tests of the Loupe Agent PerformanceCounter Library";

            m_Configuration.SessionFile.EnableFilePruning = false;

            m_Configuration.Monitors.Add(new PerformanceConfiguration());

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
