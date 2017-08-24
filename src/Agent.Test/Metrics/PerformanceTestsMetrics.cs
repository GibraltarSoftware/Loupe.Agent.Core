using System;
using System.Diagnostics;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;
using NUnit.Framework;

namespace Loupe.Agent.Test.Metrics
{
    [TestFixture]
    public class PerformanceTestsMetrics
    {
        private const int LoopsPerSampledTest = 10000;
        private const int LoopsPerEventTest = 60000;

        private const int MessagesPerSampledLoop = 6;
        private const int MessagesPerEventLoop = 1;
        private const int ValuesPerEventMessage = 3;

        [SetUp]
        public void MetricsPerformanceSetUp()
        {
            // Start a new session file so it won't do maintenance in the middle of our tests.
            Log.EndFile("Preparing for Performance Test");
            Trace.TraceInformation("Session File wrapped to new segment.");
        }

        [Test]
        public void SampledMetricsByMethodsPerformanceTest()
        {
            Log.TraceVerbose("Registering new sampled metric definitions");
            //go ahead and register a few metrics
            //int curMetricDefinitionCount = Log.MetricDefinitions.Count;

            SampledMetricDefinition incrementalCountDefinition =
                SampledMetricDefinition.Register("PerformanceTestsMetrics", "Performance.SampledMetrics.Methods", "IncrementalCount",
                                                 SamplingType.IncrementalCount, null, "Incremental Count",
                                                 "Unit test sampled metric using the incremental count calculation routine.");

            SampledMetricDefinition incrementalFractionDefinition =
                SampledMetricDefinition.Register("PerformanceTestsMetrics", "Performance.SampledMetrics.Methods", "IncrementalFraction",
                                                 SamplingType.IncrementalFraction, null, "Incremental Fraction",
                                                 "Unit test sampled metric using the incremental fraction calculation routine.  Rare, but fun.");

            SampledMetricDefinition totalCountDefinition =
                SampledMetricDefinition.Register("PerformanceTestsMetrics", "Performance.SampledMetrics.Methods", "TotalCount",
                                                 SamplingType.TotalCount, null, "Total Count",
                                                 "Unit test sampled metric using the Total Count calculation routine.  Very common.");

            SampledMetricDefinition totalFractionDefinition =
                SampledMetricDefinition.Register("PerformanceTestsMetrics", "Performance.SampledMetrics.Methods", "TotalFraction",
                                                 SamplingType.TotalFraction, null, "Total Fraction",
                                                 "Unit test sampled metric using the Total Fraction calculation routine.  Rare, but rounds us out.");

            SampledMetricDefinition rawCountDefinition =
                SampledMetricDefinition.Register("PerformanceTestsMetrics", "Performance.SampledMetrics.Methods", "RawCount",
                                                 SamplingType.RawCount, null, "Raw Count",
                                                 "Unit test sampled metric using the Raw Count calculation routine, which we will then average to create sample intervals.");

            SampledMetricDefinition rawFractionDefinition =
                SampledMetricDefinition.Register("PerformanceTestsMetrics", "Performance.SampledMetrics.Methods", "RawFraction",
                                                 SamplingType.RawFraction, null, "Raw Fraction",
                                                 "Unit test sampled metric using the Raw Fraction calculation routine.  Fraction types aren't common.");

            //we should have added six new metric definitions
            //Assert.AreEqual(curMetricDefinitionCount + 6, Log.MetricDefinitions.Count, "The number of registered metric definitions hasn't increased by the right amount, tending to mean that one or more metrics didn't register.");

            // These should never be null, but let's check to confirm.
            Assert.IsNotNull(incrementalCountDefinition);
            Assert.IsNotNull(incrementalFractionDefinition);
            Assert.IsNotNull(totalCountDefinition);
            Assert.IsNotNull(totalFractionDefinition);
            Assert.IsNotNull(rawCountDefinition);
            Assert.IsNotNull(rawFractionDefinition);

            Trace.TraceInformation("Sampled metric definitions registered by methods.");

            // These should never be null, but let's check to confirm.
            Assert.IsNotNull(incrementalCountDefinition);
            Assert.IsNotNull(incrementalFractionDefinition);
            Assert.IsNotNull(totalCountDefinition);
            Assert.IsNotNull(totalFractionDefinition);
            Assert.IsNotNull(rawCountDefinition);
            Assert.IsNotNull(rawFractionDefinition);

            //and lets go ahead and create new metrics for each definition
            Log.TraceVerbose("Obtaining default metric instances from each definition");

            SampledMetric incrementalCountMetric = SampledMetric.Register(incrementalCountDefinition, null);
            SampledMetric incrementalFractionMetric = SampledMetric.Register(incrementalFractionDefinition, null);
            SampledMetric totalCountMetric = SampledMetric.Register(totalCountDefinition, null);
            SampledMetric totalFractionMetric = SampledMetric.Register(totalFractionDefinition, null);
            SampledMetric rawCountMetric = SampledMetric.Register(rawCountDefinition, null);
            SampledMetric rawFractionMetric = SampledMetric.Register(rawFractionDefinition, null);

            // These should never be null, either, but let's check to confirm.
            Assert.IsNotNull(incrementalCountMetric);
            Assert.IsNotNull(incrementalFractionMetric);
            Assert.IsNotNull(totalCountMetric);
            Assert.IsNotNull(totalFractionMetric);
            Assert.IsNotNull(rawCountMetric);
            Assert.IsNotNull(rawFractionMetric);

            // Now, lets get everything to flush so we have our best initial state.
            Log.Information(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.Performance", "Preparing for Test", "Flushing queue");

            //now that we know it's flushed everything, lets do our timed loop.
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            for (int curMessage = 0; curMessage < LoopsPerSampledTest; curMessage++)
            {
                //We're putting in fairly bogus data, but it will produce a consistent output.
                incrementalCountMetric.WriteSample(20);
                incrementalFractionMetric.WriteSample(20, 30);
                totalCountMetric.WriteSample(20);
                totalFractionMetric.WriteSample(20, 30);
                rawCountMetric.WriteSample(20);
                rawFractionMetric.WriteSample(20, 30);
            }
            DateTimeOffset messageEndTime = DateTimeOffset.UtcNow;

            //one wait for commit message to force the buffer to flush.
            Log.Information(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.Performance", "Waiting for Samples to Commit", null);

            //and store off our time
            DateTimeOffset endTime = DateTimeOffset.UtcNow;

            TimeSpan testDuration = endTime - startTime;
            TimeSpan loopDuration = messageEndTime - startTime;
            const int messagesPerTest = LoopsPerSampledTest * MessagesPerSampledLoop;

            Trace.TraceInformation("Sampled Metrics by Methods Test committed {0:N0} samples in {1:F3} ms (average {2:F4} ms per message).  Average loop time {3:F4} ms ({4} samples per loop) and final flush time {5:F3} ms.",
                                   messagesPerTest, testDuration.TotalMilliseconds, (testDuration.TotalMilliseconds / messagesPerTest),
                                   (loopDuration.TotalMilliseconds / LoopsPerSampledTest), MessagesPerSampledLoop,
                                   (endTime - messageEndTime).TotalMilliseconds);
        }

        [Test]
        public void SampledMetricsByAttributesPerformanceTest()
        {
            SampledMetric.Register(typeof(UserPerformanceObject));
            Trace.TraceInformation("Sampled metrics registered by attributes.");

            UserPerformanceObject sampledObject = new UserPerformanceObject("AttributesPerformanceTest", 25, 100);

            //first, lets get everything to flush so we have our best initial state.
            Log.Information(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.Performance", "Preparing for Test", "Flushing queue");

            //now that we know it's flushed everything, lets do our timed loop.
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            for (int curMessage = 0; curMessage < LoopsPerSampledTest; curMessage++)
            {
                SampledMetricDefinition.Write(sampledObject);
            }
            DateTimeOffset messageEndTime = DateTimeOffset.UtcNow;

            //one wait for commit message to force the buffer to flush.
            Log.Information(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.Performance", "Waiting for Samples to Commit", null);

            //and store off our time
            DateTimeOffset endTime = DateTimeOffset.UtcNow;

            TimeSpan testDuration = endTime - startTime;
            TimeSpan loopDuration = messageEndTime - startTime;
            const int messagesPerTest = LoopsPerSampledTest * MessagesPerSampledLoop;
            
            Trace.TraceInformation("Sampled Metrics by Attributes Test committed {0:N0} samples in {1:F3} ms (average {2:F4} ms per message).  Average loop time {3:F4} ms ({4} samples per loop) and final flush time {5:F3} ms.",
                                   messagesPerTest, testDuration.TotalMilliseconds, (testDuration.TotalMilliseconds / messagesPerTest),
                                   (loopDuration.TotalMilliseconds / LoopsPerSampledTest), MessagesPerSampledLoop,
                                   (endTime - messageEndTime).TotalMilliseconds);
        }

        [Test]
        public void EventMetricsByMethodsPerformanceTest()
        {
            EventMetricDefinition eventDefinition;
            if (false == EventMetricDefinition.TryGetValue("PerformanceTestsMetrics", "Performance.EventMetrics.Methods", "UserEvent", out eventDefinition))
            {
                eventDefinition = new EventMetricDefinition("PerformanceTestsMetrics", "Performance.EventMetrics.Methods", "UserEvent");
                eventDefinition.Caption = "User Event";
                eventDefinition.Description = "Unit test event metric with typical data.";
                eventDefinition.AddValue("fileName", typeof(string), SummaryFunction.Count, null, "File name", "The name of the file");
                eventDefinition.AddValue("operation", typeof(UserFileOperation), SummaryFunction.Count, null, "Operation", "The type of file operation being performed.");
                eventDefinition.AddValue("duration", typeof(TimeSpan), SummaryFunction.Average, "ms", "Duration", "The duration for this file operation.");
                EventMetricDefinition.Register(ref eventDefinition, "duration");
            }

            Assert.IsNotNull(eventDefinition);
            Assert.IsTrue(eventDefinition.IsReadOnly);

            Trace.TraceInformation("Event metric definition registered by methods.");

            EventMetric eventMetric = EventMetric.Register(eventDefinition, "MethodsPerformanceTest");

            Assert.IsNotNull(eventMetric);

            string fileName = @"C:\Dummy\File\Name.txt";
            DateTimeOffset operationStart = DateTimeOffset.UtcNow;
            DateTimeOffset operationEnd = operationStart.AddMilliseconds(1234);

            //first, lets get everything to flush so we have our best initial state.
            Log.Information(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.Performance", "Preparing for Test", "Flushing queue");

            //now that we know it's flushed everything, lets do our timed loop.
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            for (int curMessage = 0; curMessage < LoopsPerEventTest; curMessage++)
            {
                EventMetricSample eventSample = eventMetric.CreateSample();
                eventSample.SetValue("fileName", fileName);
                eventSample.SetValue("operation", UserFileOperation.Write);
                eventSample.SetValue("duration", operationEnd - operationStart);
                eventSample.Write();
            }
            DateTimeOffset messageEndTime = DateTimeOffset.UtcNow;

            //one wait for commit message to force the buffer to flush.
            Log.Information(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.Performance", "Waiting for Samples to Commit", null);

            //and store off our time
            DateTimeOffset endTime = DateTimeOffset.UtcNow;

            TimeSpan testDuration = endTime - startTime;
            TimeSpan loopDuration = messageEndTime - startTime;
            const int messagesPerTest = LoopsPerEventTest * MessagesPerEventLoop;
            
            Trace.TraceInformation("Event Metrics by Methods Test committed {0:N0} events in {1:F3} ms (average {2:F4} ms per message).  Average loop time {3:F4} ms ({4} values per message) and final flush time {5:F3} ms.",
                                   messagesPerTest, testDuration.TotalMilliseconds, (testDuration.TotalMilliseconds / messagesPerTest),
                                   (loopDuration.TotalMilliseconds / LoopsPerEventTest), ValuesPerEventMessage,
                                   (endTime - messageEndTime).TotalMilliseconds);

        }

        [Test]
        public void EventMetricsByAttributesPerformanceTest()
        {
            EventMetric.Register(typeof(UserPerformanceObject));
            Trace.TraceInformation("Event metrics registered by attributes.");

            UserPerformanceObject eventObject = new UserPerformanceObject("AttributesPerformanceTest");
            DateTimeOffset operationStart = DateTimeOffset.UtcNow;
            DateTimeOffset operationEnd = operationStart.AddMilliseconds(1234);
            eventObject.SetEventData(@"C:\Dummy\File\Name.txt", UserFileOperation.Write, operationStart, operationEnd);

            //first, lets get everything to flush so we have our best initial state.
            Log.Information(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.Performance", "Preparing for Test", "Flushing queue");

            //now that we know it's flushed everything, lets do our timed loop.
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            for (int curMessage = 0; curMessage < LoopsPerEventTest; curMessage++)
            {
                EventMetricDefinition.Write(eventObject);
            }
            DateTimeOffset messageEndTime = DateTimeOffset.UtcNow;

            //one wait for commit message to force the buffer to flush.
            Log.Information(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.Performance", "Waiting for Samples to Commit", null);

            //and store off our time
            DateTimeOffset endTime = DateTimeOffset.UtcNow;

            TimeSpan testDuration = endTime - startTime;
            TimeSpan loopDuration = messageEndTime - startTime;
            const int messagesPerTest = LoopsPerEventTest * MessagesPerEventLoop;
            
            Trace.TraceInformation("Event Metrics by Attributes Test committed {0:N0} events in {1:F3} ms (average {2:F4} ms per message).  Average loop time {3:F4} ms ({4} values per message) and final flush time {5:F3} ms.",
                                   messagesPerTest, testDuration.TotalMilliseconds, (testDuration.TotalMilliseconds / messagesPerTest),
                                   (loopDuration.TotalMilliseconds / LoopsPerEventTest), ValuesPerEventMessage,
                                   (endTime - messageEndTime).TotalMilliseconds);
        }
    }
}
