#pragma warning disable 420 //volatile warn

using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;
using NUnit.Framework;

namespace Loupe.Agent.Test.Metrics
{
    [TestFixture]
    public class EventMetricsByMethodsTests
    {
        private readonly object m_SyncLock = new object();
        private volatile int m_ThreadCounter;
        private volatile bool m_ThreadFailed;

        /// <summary>
        /// Ensures each of the metrics we test with are actually defined.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            // This is an example of how to create an event metric definition programatically.

            EventMetricDefinition newMetricDefinition;

            // Define an event metric manually (the long way).  First, see if it's already registered...
            if (EventMetricDefinition.TryGetValue("EventMetricTests", "Gibraltar.Monitor.Test", "Manual", out newMetricDefinition) == false)
            {
                // It's not registered yet, so we need to fill out the template to define it.
                EventMetricDefinition newEventMetric = new EventMetricDefinition("EventMetricTests", "Gibraltar.Monitor.Test", "Manual");

                // Now we want to add a few value columns to make it useful.
                // NOTE:  This is designed to exactly match UserEventObject for convenience in analzing results.
                // The dummy data values we're using are unitless, so we'll just use null for the required unitCaption parameter.
                newEventMetric.AddValue("short_average", typeof(short), SummaryFunction.Average, null, "Short Average", "Data of type Short");
                newEventMetric.AddValue("short_sum", typeof(short), SummaryFunction.Sum, null, "Short Sum", "Data of type Short");
                newEventMetric.AddValue("short_runningaverage", typeof(short), SummaryFunction.RunningAverage, null, "Short Running Average", "Data of type Short");
                newEventMetric.AddValue("short_runningsum", typeof(short), SummaryFunction.RunningSum, null, "Short Running Sum", "Data of type Short");
                newEventMetric.AddValue("ushort_average", typeof(ushort), SummaryFunction.Average, null, "UShort Average", "Data of type UShort");
                newEventMetric.AddValue("ushort_sum", typeof(ushort), SummaryFunction.Sum, null, "UShort Sum", "Data of type UShort");

                // Pick an interesting value column as the default to be graphed for this metric.  We'll pass it below.
                EventMetricValueDefinition defaultValue =
                    newEventMetric.AddValue("int_average", typeof(int), SummaryFunction.Average, null, "Int Average", "Data of type Int");

                newEventMetric.AddValue("int_sum", typeof(int), SummaryFunction.Sum, null, "Int Sum", "Data of type Int");
                newEventMetric.AddValue("uint_average", typeof(uint), SummaryFunction.Average, null, "UInt Average", "Data of type UInt");
                newEventMetric.AddValue("uint_sum", typeof(uint), SummaryFunction.Sum, null, "UInt Sum", "Data of type UInt");
                newEventMetric.AddValue("long_average", typeof(long), SummaryFunction.Average, null, "Long Average", "Data of type Long");
                newEventMetric.AddValue("long_sum", typeof(long), SummaryFunction.Sum, null, "Long Sum", "Data of type Long");
                newEventMetric.AddValue("ulong_average", typeof(ulong), SummaryFunction.Average, null, "ULong Average", "Data of type ULong");
                newEventMetric.AddValue("ulong_sum", typeof(ulong), SummaryFunction.Sum, null, "ULong Sum", "Data of type ULong");
                newEventMetric.AddValue("decimal_average", typeof(decimal), SummaryFunction.Average, null, "Decimal Average", "Data of type Decimal");
                newEventMetric.AddValue("decimal_sum", typeof(decimal), SummaryFunction.Sum, null, "Decimal Sum", "Data of type Decimal");
                newEventMetric.AddValue("double_average", typeof(double), SummaryFunction.Average, null, "Double Average", "Data of type Double");
                newEventMetric.AddValue("double_sum", typeof(double), SummaryFunction.Sum, null, "Double Sum", "Data of type Double");
                newEventMetric.AddValue("float_average", typeof(float), SummaryFunction.Average, null, "Float Average", "Data of type Float");
                newEventMetric.AddValue("float_sum", typeof(float), SummaryFunction.Sum, null, "Float Sum", "Data of type Float");
                newEventMetric.AddValue("timespan_average", typeof(TimeSpan), SummaryFunction.Average, null, "TimeSpan Average", "Data of type TimeSpan");
                newEventMetric.AddValue("timespan_sum", typeof(TimeSpan), SummaryFunction.Sum, null, "TimeSpan Sum", "Data of type TimeSpan");
                newEventMetric.AddValue("timespan_runningaverage", typeof(TimeSpan), SummaryFunction.RunningAverage, null, "TimeSpan Running Average", "Data of type TimeSpan represented as a running average.");
                newEventMetric.AddValue("timespan_runningsum", typeof(TimeSpan), SummaryFunction.RunningSum, null, "TimeSpan Running Sum", "Data of type TimeSpan represented as a running sum.");
                newEventMetric.AddValue("string", typeof(string), SummaryFunction.Count, null, "String", "Data of type String");
                newEventMetric.AddValue("system.enum", typeof(UserDataEnumeration), SummaryFunction.Count, null, "System.Enum", "Data of type System.Enum");
                
                // Finally, register it with Gibraltar, and specify the default value column we saved above.
                EventMetricDefinition.Register(ref newEventMetric, defaultValue);
            }

            EventMetricDefinition metricDefinition;
            Assert.IsTrue(EventMetricDefinition.TryGetValue("EventMetricTests", "Gibraltar.Monitor.Test", "Manual", out metricDefinition));
            Assert.IsNotNull(metricDefinition);
        }

        [Test]
        public void RecordEventMetric()
        {
            // Internally we want to make this comparable to the reflection test, just varying the part that uses reflection.
            EventMetricDefinition metricDefinition;
            Assert.IsTrue(EventMetricDefinition.TryGetValue("EventMetricTests", "Gibraltar.Monitor.Test", "Manual", out metricDefinition));
            Assert.IsNotNull(metricDefinition);

            EventMetric thisExperimentMetric = EventMetric.Register(metricDefinition, "RecordEventMetric");
            Assert.IsNotNull(thisExperimentMetric);

            // To write a sample manually, we must first create an empty sample for this event metric instance.
            EventMetricSample newSample = thisExperimentMetric.CreateSample();

            // Then we set the values.
            newSample.SetValue("short_average", 1);
            newSample.SetValue("short_sum", 1);
            newSample.SetValue("short_runningaverage", 1);
            newSample.SetValue("short_runningsum", 1);
            newSample.SetValue("ushort_average", (ushort)1);
            newSample.SetValue("ushort_sum", (ushort)1);
            newSample.SetValue("int_average", 1);
            newSample.SetValue("int_sum", 1);
            newSample.SetValue("uint_average", (uint)1);
            newSample.SetValue("uint_sum", (uint)1);
            newSample.SetValue("long_average", 1);
            newSample.SetValue("long_sum", 1);
            newSample.SetValue("ulong_average", (ulong)1);
            newSample.SetValue("ulong_sum", (ulong)1);
            newSample.SetValue("decimal_average", 1);
            newSample.SetValue("decimal_sum", 1);
            newSample.SetValue("double_average", 1);
            newSample.SetValue("double_sum", 1);
            newSample.SetValue("float_average", 1);
            newSample.SetValue("float_sum", 1);
            newSample.SetValue("timespan_average", new TimeSpan(1));
            newSample.SetValue("timespan_sum", new TimeSpan(1));
            newSample.SetValue("timespan_runningaverage", new TimeSpan(1));
            newSample.SetValue("timespan_runningsum", new TimeSpan(1));
            newSample.SetValue("string", string.Format(CultureInfo.CurrentCulture, "The current manual sample is {0}", 1));
            newSample.SetValue("system.enum", (UserDataEnumeration)1);

            // And finally, tell the sample to write itself to the Gibraltar log.
            newSample.Write();
        }

        [Test]
        public void RecordEventMetricPerformanceTest()
        {
            // Internally we want to make this comparable to the reflection test, just varying the part that uses reflection.
            EventMetricDefinition metricDefinition;
            Assert.IsTrue(EventMetricDefinition.TryGetValue("EventMetricTests", "Gibraltar.Monitor.Test", "Manual", out metricDefinition));
            Assert.IsNotNull(metricDefinition);

            EventMetric thisExperimentMetric = EventMetric.Register(metricDefinition, "RecordEventMetricPerformanceTest");
            Assert.IsNotNull(thisExperimentMetric);

            // We're going to write out a BUNCH of samples...
            Trace.TraceInformation("Starting performance test");
            DateTime curTime = DateTime.Now; //for timing how fast we are
            int curSample;
            for (curSample = 0; curSample < 32000; curSample++)
            {
                EventMetricSample newSample = thisExperimentMetric.CreateSample();
                newSample.SetValue("short_average", curSample);
                newSample.SetValue("short_sum", curSample);
                newSample.SetValue("short_runningaverage", curSample);
                newSample.SetValue("short_runningsum", curSample);
                newSample.SetValue("ushort_average", (ushort)curSample);
                newSample.SetValue("ushort_sum", (ushort)curSample);
                newSample.SetValue("int_average", curSample);
                newSample.SetValue("int_sum", curSample);
                newSample.SetValue("uint_average", (uint)curSample);
                newSample.SetValue("uint_sum", (uint)curSample);
                newSample.SetValue("long_average", curSample);
                newSample.SetValue("long_sum", curSample);
                newSample.SetValue("ulong_average", (ulong)curSample);
                newSample.SetValue("ulong_sum", (ulong)curSample);
                newSample.SetValue("decimal_average", curSample);
                newSample.SetValue("decimal_sum", curSample);
                newSample.SetValue("double_average", curSample);
                newSample.SetValue("double_sum", curSample);
                newSample.SetValue("float_average", curSample);
                newSample.SetValue("float_sum", curSample);
                newSample.SetValue("timespan_average", new TimeSpan(curSample));
                newSample.SetValue("timespan_sum", new TimeSpan(curSample));
                newSample.SetValue("timespan_runningaverage", new TimeSpan(curSample));
                newSample.SetValue("timespan_runningsum", new TimeSpan(curSample));
                newSample.SetValue("string", string.Format(CultureInfo.CurrentCulture, "The current manual sample is {0}", curSample));
                newSample.SetValue("system.enum", (UserDataEnumeration)curSample);

                newSample.Write(); //only now does it get written because we had to wait until you populated the metrics
            }
            TimeSpan duration = DateTime.Now - curTime;
            Trace.TraceInformation("Completed performance test in {0} milliseconds for {1} samples", duration.TotalMilliseconds, curSample);

            Log.Verbose(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.EventMetric.Methods", "Event Metrics performance test flush", null);
        }

        /// <summary>
        /// Deliberately attempt to register the same metric simultaneously on multiple threads to test threadsafety.
        /// </summary>
        [Test]
        public void EventMetricThreadingCollisionTest()
        {
            Log.Information("Unit Tests.Metrics.EventMetric.Reflection", "Starting EventMetric threading collision test", null);
            const int ThreadCount = 9;

            int loopCount;
            lock (m_SyncLock)
            {
                m_ThreadFailed = false;
                m_ThreadCounter = 0;

                for (int i = 1; i <= ThreadCount; i++)
                {
                    Thread newThread = new Thread(SynchronizedMetricRegistration);
                    newThread.Name = "Sync thread " + i;
                    newThread.IsBackground = true;
                    newThread.Start();
                }

                loopCount = 0;
                while (m_ThreadCounter < ThreadCount)
                {
                    Thread.Sleep(100);
                    loopCount++;
                    if (loopCount > 40)
                        break;
                }

                Thread.Sleep(2000);
                Trace.TraceInformation("Releasing SyncLock");
                System.Threading.Monitor.PulseAll(m_SyncLock);
            }

            loopCount = 0;
            while (m_ThreadCounter > 0)
            {
                Thread.Sleep(100);
                loopCount++;
                if (loopCount > 40)
                    break;
            }

            Thread.Sleep(100);
            if (m_ThreadCounter > 0)
                Trace.TraceWarning("Not all threads finished before timeout");

            if (m_ThreadFailed)
                Assert.Fail("At least one thread got an exception");
        }

        private void SynchronizedMetricRegistration()
        {
            string name = Thread.CurrentThread.Name;
            Trace.TraceInformation("{0} started", name);
            EventMetricDefinition newDefinition = new EventMetricDefinition("EventMetricTests", "Gibraltar.Monitor.Test", "Sync");
            newDefinition.AddValue("delta", typeof(double), SummaryFunction.RunningSum, null, "Delta", "The applied delta");

            try
            {
                Interlocked.Increment(ref m_ThreadCounter);
                lock (m_SyncLock)
                {
                    // Do nothing, just release it immediately.
                }

                EventMetricDefinition.Register(ref newDefinition);

                EventMetric metric = EventMetric.Register(newDefinition, name);

                Trace.TraceInformation("{0} completed registration of event metric", name);

                EventMetricSample sample = metric.CreateSample();
                sample.SetValue("delta", Thread.CurrentThread.ManagedThreadId);
                sample.Write();
            }
            catch (Exception ex)
            {
                m_ThreadFailed = true;
                Trace.TraceError("{0} got {1}: {2}", name, ex.GetType().Name, ex.Message, ex);
            }

            Interlocked.Decrement(ref m_ThreadCounter);
        }
    }

}
