using System;
using System.Diagnostics;
using Gibraltar.Agent;
using NUnit.Framework;

namespace Loupe.Agent.Test.LogMessages
{
    [TestFixture]
    public class MessageAlertTests
    {
        private readonly object m_Lock = new object();
        private int m_Count;
        private TimeSpan m_Latency;
        private TimeSpan m_Span;

        private void Log_MessageAlertMeasurement(object sender, LogMessageAlertEventArgs e)
        {
            int count = e.TotalCount;
            TimeSpan latency = DateTimeOffset.Now - e.Messages[0].Timestamp;
            TimeSpan span = e.Messages[count - 1].Timestamp - e.Messages[0].Timestamp;

            lock (m_Lock)
            {
                m_Latency = latency;
                m_Span = span;
                m_Count = count;

                System.Threading.Monitor.PulseAll(m_Lock);
            }

            //e.MinimumDelay = TimeSpan.Zero; //for our unit tests we don't want to hold things.
        }

        private int WaitForEvent(out TimeSpan latency, out TimeSpan span)
        {
            int count;
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            TimeSpan timeoutWait = new TimeSpan(0, 0, 1); // Wait up to 1 second (1000 ms) for the event to fire.
            DateTimeOffset endTime = startTime + timeoutWait;
            lock (m_Lock)
            {
                timeoutWait = endTime - DateTimeOffset.UtcNow; // Grab it again; we don't know how long we waited for the lock.
                while (m_Count == 0 && timeoutWait > TimeSpan.Zero)
                {
                    System.Threading.Monitor.Wait(m_Lock, timeoutWait);

                    timeoutWait = endTime - DateTimeOffset.UtcNow;
                }

                latency = m_Latency;
                span = m_Span;
                count = m_Count;

                m_Count = 0;
                m_Latency = TimeSpan.Zero;
                m_Span = TimeSpan.Zero;
            }
            if (count > 0)
                Trace.TraceInformation("Message Alert event received: Count = {0}; Latency = {1:F4} ms; Span = {2:F4} ms;",
                                       count, latency.TotalMilliseconds, span.TotalMilliseconds);
            else
                Trace.TraceInformation("WaitForEvent timed out {0:F4} ms after target (1000 ms).", timeoutWait.Negate().TotalMilliseconds);

            return count;
        }

        [Test]
        public void MessageAlertTest()
        {
            try
            {
                Log.Verbose(LogWriteMode.WaitForCommit, "Gibraltar.Agent.Unit Tests.MessageAlert", "Pre-test flush", null);

                Log.MessageAlert += Log_MessageAlertMeasurement;
                TimeSpan latency;
                TimeSpan span;

                DateTimeOffset start = DateTimeOffset.UtcNow;
                int count = WaitForEvent(out latency, out span);
                Assert.AreEqual(0, count, "WaitForEvent did not timeout as expected");
                span = DateTimeOffset.UtcNow - start;
                Assert.GreaterOrEqual(span.TotalMilliseconds, 1000, "WaitForEvent timed out in less than 1000 ms.");
                Assert.LessOrEqual(span.TotalMilliseconds, 1050, "WaitForEvent took more than 1050 ms to time out.");

                Log.Warning("Gibraltar.Agent.Unit Tests.MessageAlert", "Single warning to test Message Alert", null);
                count = WaitForEvent(out latency, out span);
                Assert.AreNotEqual(0, count, "Message Alert event didn't fire within 1 second timeout.");
                Assert.AreEqual(1, count, "Message Alert event included more than the expected message.");
                Assert.LessOrEqual(latency.TotalMilliseconds, 200, "Initial event latency exceeded 200 ms");

                Log.Error("Gibraltar.Agent.Unit Tests.MessageAlert", "Single error to test Message Alert", null);
                count = WaitForEvent(out latency, out span);
                Assert.AreNotEqual(0, count, "Message Alert event didn't fire within 1 second timeout.");
                Assert.AreEqual(1, count, "Message Alert event included more than the expected message.");
                Assert.LessOrEqual(latency.TotalMilliseconds, 75, "Second event latency exceeded 75 ms");

                Log.Critical("Gibraltar.Agent.Unit Tests.MessageAlert", "Single critical to test Message Alert", null);
                Log.Information("Gibraltar.Agent.Unit Tests.MessageAlert", "Single information to test Message Alert", null);
                count = WaitForEvent(out latency, out span);
                Assert.AreNotEqual(0, count, "Message Alert event didn't fire within 1 second timeout.");
                Assert.LessOrEqual(latency.TotalMilliseconds, 75, "Event latency exceeded 75 ms");

                Log.Error("Gibraltar.Agent.Unit Tests.MessageAlert", "Triple error to test Message Alert", "1 of 3");
                Log.Verbose("Gibraltar.Agent.Unit Tests.MessageAlert", "Single verbose to test Message Alert", null);
                Log.Error("Gibraltar.Agent.Unit Tests.MessageAlert", "Triple error to test Message Alert", "2 of 3");
                Log.Verbose("Gibraltar.Agent.Unit Tests.MessageAlert", "Double verbose to test Message Alert", "1 of 2");
                Log.Verbose("Gibraltar.Agent.Unit Tests.MessageAlert", "Double verbose to test Message Alert", "2 of 2");
                Log.Error("Gibraltar.Agent.Unit Tests.MessageAlert", "Triple error to test Message Alert", "3 of 3");
                count = WaitForEvent(out latency, out span);
                Assert.AreNotEqual(0, count, "Message Alert event didn't fire within 1 second timeout.");
                Assert.LessOrEqual(count, 3, "Message Alert event included extra messages.");
                Assert.AreEqual(3, count, "Message Alert did not include the expected burst of 3 messages.");
                Assert.LessOrEqual(latency.TotalMilliseconds, 75, "Event latency exceeded 75 ms");

                Log.Warning("Gibraltar.Agent.Unit Tests.MessageAlert", "Warning in burst to test Message Alert", "1 of 3");
                Log.Information("Gibraltar.Agent.Unit Tests.MessageAlert", "Double information to test Message Alert", "1 of 2");
                Log.Information("Gibraltar.Agent.Unit Tests.MessageAlert", "Double information to test Message Alert", "2 of 2");
                Log.Error("Gibraltar.Agent.Unit Tests.MessageAlert", "Error in burst to test Message Alert", "2 of 3");
                Log.Information("Gibraltar.Agent.Unit Tests.MessageAlert", "Single information to test Message Alert", null);
                Log.Critical("Gibraltar.Agent.Unit Tests.MessageAlert", "Critical in burst to test Message Alert", "3 of 3");
                Log.Verbose("Gibraltar.Agent.Unit Tests.MessageAlert", "Triple verbose to test Message Alert", "1 of 3");
                Log.Verbose("Gibraltar.Agent.Unit Tests.MessageAlert", "Triple verbose to test Message Alert", "2 of 3");
                Log.Verbose("Gibraltar.Agent.Unit Tests.MessageAlert", "Triple verbose to test Message Alert", "3 of 3");
                count = WaitForEvent(out latency, out span);
                Assert.AreNotEqual(0, count, "Message Alert event didn't fire within 1 second timeout.");
                Assert.LessOrEqual(count, 3, "Message Alert event included extra messages.");
                Assert.AreEqual(3, count, "Message Alert did not include the expected burst of 3 messages.");
                Assert.LessOrEqual(latency.TotalMilliseconds, 75, "Event latency exceeded 75 ms");

                for (int i=0; i < 50; i++)
                    Log.Warning("Gibraltar.Agent.Unit Tests.MessageAlert", "Numerous warnings to test Message Alert",
                                "{0} of 50", i);

                count = WaitForEvent(out latency, out span);
                Assert.AreNotEqual(0, count, "Message Alert event didn't fire within 1 second timeout.");
                Assert.LessOrEqual(latency.TotalMilliseconds, 100, "Event latency exceeded 100 ms");
                Assert.LessOrEqual(span.TotalMilliseconds, 75, "Event spanned more than 75 ms");
                Assert.AreEqual(50, count, "Event did not include all 50 warnings");
            }
            finally
            {
                Log.MessageAlert -= Log_MessageAlertMeasurement;
            }
        }

        [Ignore("local debugging test")]
        [Test]
        public void MessageAlertDemo()
        {
            try
            {
                Log.MessageAlert += Log_MessageAlert;
                Log.Warning("Gibraltar.Agent.Unit Tests.MessageAlert", "This should not trigger a package", "Because this is not an error");
                Log.Error("Gibraltar.Agent.Unit Tests.MessageAlert", "Red Alert!", "Here is an error that definitely should cause our handler to package up stuff!");
            }
            finally
            {
                Log.MessageAlert -= Log_MessageAlert;
            }
        }

        private void Log_MessageAlert(object sender, LogMessageAlertEventArgs e)
        {
            //if there are any errors (or worse - criticals) we want to send the
            //up to date information on this session immediately.
            if (e.TopSeverity <= LogMessageSeverity.Error) //numeric values DROP for more severe enum values
            {
                //set our auto-send to true.
                e.SendSession = true;

                //and lets make sure we don't send again for at least a few minutes
                //to ensure we don't flood in the event of a storm of errors.
                e.MinimumDelay = new TimeSpan(0, 5, 0); //5 minutes
            }
        }
    }
}
