#pragma warning disable 420 //volatile warn

using System;
using System.Diagnostics;
using System.Threading;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;
using Loupe.Logging;
using Loupe.Metrics;
using NUnit.Framework;

namespace Loupe.Agent.Test.Metrics
{
    [TestFixture]
    public class EventMetricsByAttributesTests
    {
        #region Private enum and example class with attributes
        private enum OperationType
        {
            None = 0,
            Plus = 1,
            Minus = 2,
        }

        /// <summary>
        /// A simple example of event metric attribute usage.
        /// </summary>
        /// <remarks>The event metric defined here will track increments and decrements.</remarks>
        [EventMetric("Gibraltar Event Metrics", "Example.EventMetric.Attributes", "UserEventClass",
            Caption="Example event metric", Description="An example of defining an event metric via attributes.")]
        private class UserEventAttributedClass
        {
            private readonly string m_InstanceName;

            // We're dealing with plain numbers, no units, so we just use null for the required unitCaption parameter.
            // Caption and Description are optional, but *strongly* recommended for understandable analysis results.
            // And this will be the primary data column to graph for this event metric, so we mark it as the default.
            [EventMetricValue("delta", SummaryFunction.RunningSum, null, IsDefaultValue=true, Caption="Delta",
                Description="The positive or negative (or zero) effect of the operation.")]
            private int m_Delta; // It's even legal to use private field members as a value column.

            public UserEventAttributedClass(string instanceName)
            {
                m_InstanceName = instanceName;
                m_Delta = 0;
            }

            /// <summary>
            /// An optional member to be automatically queried for the instance name to use for this event metric on this data object instance.
            /// </summary>
            /// <remarks>The name of this property isn't important, but for this simple example the obvious name works well.
            /// Fields (member variables) and zero-argument methods are other options, but only one member in the class
            /// should be marked with the [EventMetricInstanceName] attribute.  It can also be left out entirely and either
            /// be specified in code or default to null (the "default instance").  This attribute is handy when different
            /// instances of your class will record to separate metric instances, as allowed for in this example.  Any other
            /// type will be converted to a string (ToString()) unless it is a null (a numeric zero is not considered a null).</remarks>
            [EventMetricInstanceName]
            public string InstanceName { get { return m_InstanceName; } }

            /// <summary>
            /// The OperationType enum value for the most recent operation.
            /// </summary>
            /// <remarks>Because this is not a recognized numeric type, the value collected will automatically be converted
            /// to a string in each event metric sample.  (Can cast an enum to an int to keep it as a numeric value, but
            /// the enum label would then be lost.)  We must specify a summary function, but since this is a non-numeric
            /// data type, SummaryFunction.Count is the only meaningful choice.  Units are also meaningless for non-numeric
            /// data types, so we use null for the required unitCaption parameter.</remarks>
            [EventMetricValue("operation", SummaryFunction.Count, null, Caption="Operation",
                Description="The operation performed.")]
            public OperationType Operation
            {
                get
                {
                    OperationType operation;
                    if (m_Delta < 0)
                    {
                        operation = OperationType.Minus;
                    }
                    else if (m_Delta > 0)
                    {
                        operation = OperationType.Plus;
                    }
                    else
                    {
                        operation = OperationType.None;
                    }

                    return operation;
                }
            }

            /// <summary>
            /// Perform an increment or decrement operation by a specified (positive or negative) delta.
            /// </summary>
            /// <param name="delta">The amount to increment (positive) or decrement (negative) by.</param>
            public void ApplyDelta(int delta)
            {
                m_Delta = delta;

                EventMetric.Write(this);
            }

            /// <summary>
            /// Read the magnitude (absolute value) of the delta for the latest operation.
            /// </summary>
            /// <remarks>This is an example of a zero-argument method, which can also be marked with an EventMetricValue
            /// attribute to be included as another value column.  Only methods with no arguments are supported, and it
            /// must, of course, return a value (not void).</remarks>
            /// <returns>The amount of increment, or the negative of the amount of decrement.</returns>
            [EventMetricValue("magnitude", SummaryFunction.Average, null, Caption="Magnitude",
                Description="The magnitude (absolute value) of the delta submitted for the operation.")]
            public int MagnitudeOfChange()
            {
                return (m_Delta < 0) ? -m_Delta : m_Delta;
            }
        }
        #endregion

        private readonly object m_SyncLock = new object();
        private volatile int m_ThreadCounter;
        private volatile bool m_ThreadFailed;

        /// <summary>
        /// Several alternatives for example client code to utilize event metrics through reflection with attributes.
        /// </summary>
        [Test]
        public void EventAttributeReflectionExample()
        {            
            //
            // General example usage.
            //

            // Optional registration in advance (time-consuming reflection walk to find attributes the first time).

            // Either of these two approaches will dig into base types and all interfaces seen at the top level.
            EventMetric.Register(typeof(UserEventAttributedClass)); // Can be registered from the Type itself.
            // Or...
            UserEventAttributedClass anyUserEventAttributedInstance = new UserEventAttributedClass("any-prototype");
            EventMetric.Register(anyUserEventAttributedInstance); // Can register from a live instance (gets its Type automatically).

            // Elsewhere...

            UserEventAttributedClass userEventAttributedInstance = new UserEventAttributedClass("automatic-instance-name"); // then...
            
            // To sample all valid event metrics defined by attributes (at all inheritance levels) for a data object instance:

            // This will determine the instance name automatically from the member marked with the EventMetricInstanceName attribute.
            // Null will be used as the instance name (the "default instance") if the attribute is not found for a particular metric.
            EventMetric.Write(userEventAttributedInstance); // The recommended typical usage.

            // Or... To specify a different fallback instance name if an EventMetricInstanceName attribute isn't found:
            EventMetric.Write(userEventAttributedInstance, "fallback-instance-if-not-assigned");



            //
            // Specific example usage for example class above.
            // Generate some meaningful data in the log to look at.
            //

            UserEventAttributedClass realUsageInstance = new UserEventAttributedClass(null); // Using the "default" instance here.
            EventMetric.Register(realUsageInstance); // Registering from the live object also registers the metric instance.

            int[] testDataArray = new [] { 1, 5, 3, -4, 2, 7, -3, -2, 9, 4, -5, -1, 3, -7, 2, 4, -2, 8, 10, -4, 2 };

            foreach (int dataValue in testDataArray)
            {
                realUsageInstance.ApplyDelta(dataValue); // This method also fires off an event metric sample for us.

                Thread.Sleep(100 + (10 * dataValue)); // Sleep for a little while to space out the data, not entirely evenly for this example.
            }

            UserEventAttributedClass reverseInstance = new UserEventAttributedClass("Reverse example"); // Spaces are legal, too.
            // We've already registered the event metric definition above in several places,
            // and we'll just let this metric instance be registered when we first sample it.

            for (int i=testDataArray.Length - 1; i >= 0; i--)
            {
                // We're just running through the example data in reverse order for a second example.
                reverseInstance.ApplyDelta(testDataArray[i]);

                Thread.Sleep(100 + (10 * testDataArray[i])); // Space out the data over time a bit.
            }
        }

        [Test]
        public void RecordEventMetricReflection()
        {
            UserEventObject myDataObject = new UserEventObject(null);
            EventMetric.Register(myDataObject);

            EventMetricDefinition metricDefinition;
            Assert.IsTrue(EventMetricDefinition.TryGetValue(typeof(UserEventObject), out metricDefinition));

            EventMetricDefinition.Write(myDataObject);

            // Now try it with inheritance and interfaces in the mix.

            UserMultipleEventObject bigDataObject = new UserMultipleEventObject(null);
            EventMetric.Register(bigDataObject);
            // There's no event at the top level, so this lookup should fail.
            Assert.IsFalse(EventMetricDefinition.TryGetValue(typeof(UserMultipleEventObject), out metricDefinition));
            // Now check for interfaces...
            Assert.IsTrue(EventMetricDefinition.TryGetValue(typeof(IEventMetricOne), out metricDefinition));
            Assert.IsTrue(EventMetricDefinition.TryGetValue(typeof(IEventMetricTwo), out metricDefinition));
            Assert.IsTrue(EventMetricDefinition.TryGetValue(typeof(IEventMetricThree), out metricDefinition));
            Assert.IsTrue(EventMetricDefinition.TryGetValue(typeof(IEventMetricFour), out metricDefinition));

            // And sample all of them on the big object with a single call...

            EventMetric.Write(bigDataObject);
        }

        [Test]
        public void RecordEventMetricReflectionPerformanceTest()
        {
            UserEventObject myDataObject = new UserEventObject("Performance Test");

            //We have to limit ourselves to 32000 samples to stay within short.
            const short sampleCount = 32000;
            short curSample;

            //warm up the object just to get rid of first hit performance
            EventMetric.Register(myDataObject);
            EventMetric.Write(myDataObject);

            //and we're going to write out a BUNCH of samples
            Trace.TraceInformation("Starting reflection performance test");

            DateTime curTime = DateTime.Now;
            for (curSample = 0; curSample < sampleCount; curSample++)
            {
                //we have a LOT of numbers we need to set to increment this object.
                myDataObject.SetValues(curSample);  //sets all of the numerics

                //and write it out again just for kicks
                EventMetric.Write(myDataObject);
            }
            TimeSpan duration = DateTime.Now - curTime;
            Trace.TraceInformation("Completed reflection performance test in {0} milliseconds for {1} samples", duration.TotalMilliseconds, curSample);

            Log.Verbose(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.EventMetric.Attributes", "Event Metrics performance test flush", null);
        }

        [Test]
        public void RecordEventMetricReflectionDataRangeTest()
        {
            UserEventObject myDataObject = new UserEventObject("Data Range Test");

            //warm up the object just to get rid of first hit performance
            EventMetric.Register(myDataObject);
            EventMetric.Write(myDataObject);

            //and we're going to write out a BUNCH of samples
            Trace.TraceInformation("Starting reflection data range test");
            DateTime curTime = DateTime.Now;

            //We have to limit ourselves to 32000 samples to stay within short.
            for (short curSample = 0; curSample < 32000; curSample++)
            {
                //we have a LOT of numbers we need to set to increment this object.
                myDataObject.SetValues(curSample, 32000);  //sets all of the numerics

                //and write it out again just for kicks
                EventMetric.Write(myDataObject);
            }
            TimeSpan duration = DateTime.Now - curTime;
            Trace.TraceInformation("Completed reflection data range test in {0} milliseconds for 32,000 samples", duration.TotalMilliseconds);

            Log.Verbose(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.EventMetric.Attributes", "Event Metrics performance test flush", null);
        }

        /// <summary>
        /// Create a set of data points distributed over a reasonable range of time to show how events over time display.
        /// </summary>
        [Test]
        public void PrettySampleDataOverTimeTest()
        {
            UserEventObject myDataObject = new UserEventObject("Pretty Data");
            EventMetric.Register(myDataObject);

            //do a set of 20 samples with a gap between each.
            for (short curSample = 0; curSample < 20; curSample++)
            {
                myDataObject.SetValues(curSample);
                EventMetric.Write(myDataObject);

                //now sleep for a little to make a nice gap.  This has to be >> 16 ms to show a reasonable gap.
                Thread.Sleep(200);
            }
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
                    newThread.IsBackground = false;
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
            UserEventCollisionClass userObject = new UserEventCollisionClass(name);

            try
            {
                Interlocked.Increment(ref m_ThreadCounter);
                lock (m_SyncLock)
                {
                    // Do nothing, just release it immediately.
                }

                EventMetric.Register(userObject);
                Trace.TraceInformation("{0} completed registration of event metric", name);

                userObject.ApplyDelta(Thread.CurrentThread.ManagedThreadId);
            }
            catch (Exception ex)
            {
                m_ThreadFailed = true;
                Trace.TraceError("{0} got {1}: {2}", name, ex.GetType().Name, ex.Message, ex);
            }

            Interlocked.Decrement(ref m_ThreadCounter);
        }

        #region Private class with attributes for threading collision test

        /// <summary>
        /// A simple example of event metric attribute usage.
        /// </summary>
        /// <remarks>The event metric defined here will track increments and decrements.</remarks>
        [EventMetric("Gibraltar Event Metrics", "Example.EventMetric.Attributes", "ThreadingCollision",
            Caption = "Example event metric", Description = "A practical test case for threading collision of registration.")]
        private class UserEventCollisionClass
        {
            private readonly string m_InstanceName;

            // We're dealing with plain numbers, no units, so we just use null for the required unitCaption parameter.
            // Caption and Description are optional, but *strongly* recommended for understandable analysis results.
            // And this will be the primary data column to graph for this event metric, so we mark it as the default.
            [EventMetricValue("delta", SummaryFunction.RunningSum, null, IsDefaultValue = true, Caption = "Delta",
                Description = "The positive or negative (or zero) effect of the operation.")]
            private int m_Delta; // It's even legal to use private field members as a value column.

            public UserEventCollisionClass(string instanceName)
            {
                m_InstanceName = instanceName;
                m_Delta = 0;
            }

            /// <summary>
            /// An optional member to be automatically queried for the instance name to use for this event metric on this data object instance.
            /// </summary>
            /// <remarks>The name of this property isn't important, but for this simple example the obvious name works well.
            /// Fields (member variables) and zero-argument methods are other options, but only one member in the class
            /// should be marked with the [EventMetricInstanceName] attribute.  It can also be left out entirely and either
            /// be specified in code or default to null (the "default instance").  This attribute is handy when different
            /// instances of your class will record to separate metric instances, as allowed for in this example.  Any other
            /// type will be converted to a string (ToString()) unless it is a null (a numeric zero is not considered a null).</remarks>
            [EventMetricInstanceName]
            public string InstanceName { get { return m_InstanceName; } }

            /// <summary>
            /// The OperationType enum value for the most recent operation.
            /// </summary>
            /// <remarks>Because this is not a recognized numeric type, the value collected will automatically be converted
            /// to a string in each event metric sample.  (Can cast an enum to an int to keep it as a numeric value, but
            /// the enum label would then be lost.)  We must specify a summary function, but since this is a non-numeric
            /// data type, SummaryFunction.Count is the only meaningful choice.  Units are also meaningless for non-numeric
            /// data types, so we use null for the required unitCaption parameter.</remarks>
            [EventMetricValue("operation", SummaryFunction.Count, null, Caption = "Operation",
                Description = "The operation performed.")]
            public OperationType Operation
            {
                get
                {
                    OperationType operation;
                    if (m_Delta < 0)
                    {
                        operation = OperationType.Minus;
                    }
                    else if (m_Delta > 0)
                    {
                        operation = OperationType.Plus;
                    }
                    else
                    {
                        operation = OperationType.None;
                    }

                    return operation;
                }
            }

            /// <summary>
            /// Perform an increment or decrement operation by a specified (positive or negative) delta.
            /// </summary>
            /// <param name="delta">The amount to increment (positive) or decrement (negative) by.</param>
            public void ApplyDelta(int delta)
            {
                m_Delta = delta;

                EventMetric.Write(this);
            }

            /// <summary>
            /// Read the magnitude (absolute value) of the delta for the latest operation.
            /// </summary>
            /// <remarks>This is an example of a zero-argument method, which can also be marked with an EventMetricValue
            /// attribute to be included as another value column.  Only methods with no arguments are supported, and it
            /// must, of course, return a value (not void).</remarks>
            /// <returns>The amount of increment, or the negative of the amount of decrement.</returns>
            [EventMetricValue("magnitude", SummaryFunction.Average, null, Caption = "Magnitude",
                Description = "The magnitude (absolute value) of the delta submitted for the operation.")]
            public int MagnitudeOfChange()
            {
                return (m_Delta < 0) ? -m_Delta : m_Delta;
            }
        }
        #endregion
    }
}
