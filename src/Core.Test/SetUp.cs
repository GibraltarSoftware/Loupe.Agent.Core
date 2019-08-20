using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Loupe.Core.Monitor;
using Loupe.Configuration;
using Loupe.Extensibility.Data;
using NUnit.Framework;

namespace Loupe.Core.Test
{
    [SetUpFixture]
    public class SetUp
    {
        private bool m_HaveCanceled = false;

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Log.SilentMode = false;

            //connect up to the log event so we can test configuration.
            Log.Initializing += Log_Initializing;

            //Do our first initialize.
            Log.Initialize(null);

            //we shouldn't be up...
            if (Log.Initialized)
            {
                Trace.WriteLine("Logging is already initialized while starting up, so we won't be able to test delayed initialization.");
            }
            else
            {
                //try again which should really initialize it.
                Log.Initialize(null);
            }

            if (Log.Initialized == false)
            {
                //now we have a problem.
                Trace.WriteLine("Logging failed to initialize.");
            }

            //See how many Log items there are
            if (Log.Metrics.Count > 0)
            {
                //write out a message to the log indicating we're starting with metrics defined.
                Log.Write(LogMessageSeverity.Information, "Unit Tests", "Existing Metrics Detected", "There are already {0} metrics defined in the global log class.", Log.Metrics.Count);
            }

            //now we want to wait for our performance monitor to initialize before we proceed.
            DateTime perfMonitorInitStart = DateTime.Now;
            while ((Loupe.Core.Monitor.Monitor.Initialized == false) 
                && ((DateTime.Now - perfMonitorInitStart).TotalMilliseconds < 5000))
            {
                //just wait for it...
                Thread.Sleep(20);
            }

            //if we exited the while loop and it isn't done yet, it didn't get done fast enough.
            if (Loupe.Core.Monitor.Monitor.Initialized == false)
            {
                Log.Write(LogMessageSeverity.Warning, "Unit Tests", "Performance Monitor Initialization Failed", "Performance Monitor failed to complete its initialization after we waited {0} milliseconds.", 
                          (DateTime.Now - perfMonitorInitStart).TotalMilliseconds);
            }
            else
            {
                Log.Write(LogMessageSeverity.Information, "Unit Tests", "Performance Monitor Initialization Complete", "Performance Monitor completed initialization after we waited {0} milliseconds.",
                          (DateTime.Now - perfMonitorInitStart).TotalMilliseconds);
            }
        }

        void Log_Initializing(object sender, LogInitializingEventArgs e)
        {
            if (m_HaveCanceled == false)
            {
                //we haven't done our first cancel test.
                e.Cancel = true;
                m_HaveCanceled = true;
            }
            else
            {
                //set up our logging.
                PublisherConfiguration publisher = e.Configuration.Publisher;
                publisher.ProductName = "NUnit";
                publisher.ApplicationName = "Loupe.Test";

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

                publisher.ApplicationDescription = "NUnit tests of the Loupe Core Library";

                e.Configuration.SessionFile.EnableFilePruning = false;
            }
        }

        [OneTimeTearDown]
        public void RunAfterAllTests()
        {
            //Tell our central log session we're shutting down nicely
            Log.EndSession(SessionStatus.Normal, 0, "Ending unit tests in Loupe.Test");
        }
    }
}
