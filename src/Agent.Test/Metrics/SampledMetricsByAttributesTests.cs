using System;
using System.Diagnostics;
using System.Threading;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;
using NUnit.Framework;

namespace Loupe.Agent.Test.Metrics
{
    [TestFixture]
    public class SampledMetricsByAttributesTests
    {
        private const int MessagesPerTest = 10000;

        #region Private example class with attributes
        /// <summary>
        /// A simple example of sampled metric attribute usage.
        /// </summary>
        /// <remarks><para>The sampled metric counters defined here will track accumulated value and "temperature".</para>
        /// <para>Unlike the EventMetric attribute, we only specify metricsSystem and categoryName in the SampledMetric
        /// attribute.  Then multiple sampled metric counters can be defined on this class by specifying the remaining
        /// necessary parameters in SampledMetricValue attributes.  All sampled metric counters defined by attributes
        /// in this class will share this metricsSystem and categoryName.  If mixing sampled metrics with different
        /// category names is really needed, it can be done with advanced usage tricks like defining metrics on interfaces,
        /// much like with defining multiple event metrics on the same object.</para></remarks>
        [SampledMetric("Gibraltar Sampled Metrics", "Example.SampledMetric.Attributes")]
        private class UserSampledAttributedClass : IDisposable
        {
            private readonly string m_InstanceName;

            // We're dealing with plain numbers, no units, so we just use null for the required unitCaption parameter.
            // Caption and Description are optional, but *strongly* recommended for understandable analysis results.
            // SamplingType.RawCount means that this value directly specifies the data to graph for this counter.
            [SampledMetricValue("accumulator", SamplingType.RawCount, null, Caption="Accumulator",
                Description="The accumulated total of increments and decrements")]
            private int m_Accumulator; // It's even legal to use private field members as a sampled metric counter.

            private int m_MagnitudesAccumulator;
            private int m_MagnitudesCount;
            private int m_OperationCount;

            private const float RoomTemperature = 20; // 20 C (68 F), our air-conditioned "room temperature".
            private float m_TemperatureDelta; // How far above "room temperature" the device temperature was after last operation.
            private DateTime m_LastOperation; // When the most recent opperation occurred, for temperature simulation.
            private bool m_EndThread; // A flag to tell our polling thread when to stop.
            private Thread m_PollingThread;

            public UserSampledAttributedClass(string instanceName)
            {
                m_InstanceName = instanceName;
                m_Accumulator = 0;
                m_TemperatureDelta = 0;
                m_LastOperation = DateTime.Now;
                m_MagnitudesAccumulator = 0;
                m_MagnitudesCount = 0;
                m_OperationCount = 0;
                m_PollingThread = null;
            }

            public void StartPolling()
            {
                if (IsPolling)
                    return; // Already polling, don't make another one.

                // Now we'll create a background thread to do our sample polling.  This is just an example approach for
                // our isolated example, but in real usage it may make more sense to have one external polling thread
                // handling all of your sampled metric sampling, rather than create a thread for each instance!
                m_PollingThread = new Thread(SamplePollingThreadStart);
                m_PollingThread.Name = string.Format("Example polling thread ({0})", m_InstanceName ?? "null");
                m_PollingThread.IsBackground = true;

                m_EndThread = false;
                m_PollingThread.Start(); // Start it up!
            }

            public bool IsPolling { get { return m_PollingThread != null; } }

            /// <summary>
            /// An optional member to be automatically queried for the instance name to use for this event metric on this data object instance.
            /// </summary>
            /// <remarks>The name of this property isn't important, but for this simple example the obvious name works well.
            /// Fields (member variables) and zero-argument methods are other options, but only one member in the class
            /// should be marked with the [EventMetricInstanceName] attribute.  It can also be left out entirely and either
            /// be specified in code or default to null (the "default instance").  This attribute is handy when different
            /// instances of your class will record to separate metric instances, as allowed for in this example.  Any other
            /// type will be converted to a string (ToString()) unless it is a null (a numeric zero is not considered a null).</remarks>
            [SampledMetricInstanceName]
            public string InstanceName { get { return m_InstanceName; } }

            /// <summary>
            /// The temperature of the device, as degrees Celsius.
            /// </summary>
            /// <remarks><para>We're using this to simulate a temperature as if it were perhaps read from some measurement
            /// device.  But we'll fake it with math.</para>
            /// <para>We're doing this math without side-effects, so we'll use a property to read it.  And we're measuring
            /// temperature in degrees Celsius, so we'll use that for the units caption.
            ///</para></remarks>
            [SampledMetricValue("temperature", SamplingType.RawCount, "Degrees C", Caption = "Device Temperature",
                Description = "The temperature measured for the device.")]
            public float Temperature
            {
                get
                {
                    // Cools towards room temperature over time.  We're greatly accelerating how fast it could actually cool.
                    TimeSpan coolingTime = DateTime.Now - m_LastOperation;
                    double coolingFactor = Math.Exp(-coolingTime.TotalSeconds);
                    float currentTemperatureDelta = (float)(m_TemperatureDelta * coolingFactor);

                    return RoomTemperature + currentTemperatureDelta;
                }
            }

            /// <summary>
            /// Perform an increment or decrement operation by a specified (positive or negative) delta.
            /// </summary>
            /// <param name="delta">The amount to increment (positive) or decrement (negative) by.</param>
            public void ApplyDelta(int delta)
            {
                m_Accumulator += delta;
                m_TemperatureDelta = Temperature + 0.1f; // We're greatly exaggerating the temperature increase from one operation.
                m_LastOperation = DateTime.Now;

                m_MagnitudesAccumulator += (delta < 0) ? -delta : delta; // Add the magnitude of change.
                m_MagnitudesCount++;
                // This seems redundant, but it gets read separately from the MagnitudesCount,
                // so we have to track it as a separate value.
                m_OperationCount++;
            }

            /// <summary>
            /// The numerator portion of the average magnitude (absolute value) of operations.
            /// </summary>
            /// <remarks><para>This value tracks the total magnitude of operations applied since the previous sample.
            /// When divided by the count of such operations, this produces an average as the value tracked by this
            /// counter.  To accomplish this, we use the SamplingType.IncrementalFraction to designate that we're tracking
            /// the incremental value since the previous sample (then reset to 0) and that we also need a second value
            /// as the divisor portion, in order to complete actual metric counter.  See the MagnitudeDivisor() method
            /// for this other value.</para>
            /// <para>Because we're just dealing with raw numbers here, we'll use null for the required units caption
            /// parameter.  And because this resets the value, we're using a method rather than a property to discourage
            /// extraneous reads (such as in the debugger).</para></remarks>
            /// <returns>The total of the absolute values of deltas applied since the previous sample.</returns>
            [SampledMetricValue("averageMagnitude", SamplingType.IncrementalFraction, null, Caption="Average Magnitude",
                Description="This tracks the average absolute value of operations applied.")]
            public int AverageMagnitude()
            {
                int magnitudes = m_MagnitudesAccumulator;
                m_MagnitudesAccumulator = 0; // Reset average for next sample interval.
                return magnitudes;
            }

            /// <summary>
            /// The divisor portion of the average magnitude (absolute value) of operations.
            /// </summary>
            /// <remarks><para>This is the second part of the averageMagnitude counter, which we designate with the
            /// SampledMetricDivisor attribute.  The counter name specified in this attribute must match the one we
            /// use in the SampledMetricValue attribute for the counter that this goes with.</para>
            /// <para>Because the SampledMetricValue attribute specifies SamplingType.IncrementalFraction, both data
            /// values for this counter must behave as "Incremental", meaning they track the "increment" since the
            /// previous sample (then reset to 0).  In this case both values happen to be integers, but all sampled
            /// metrics convert data values to double (double-precision floating point), and the two members read for
            /// a "Fraction" type counter do not need to have the same numeric type.  As with the numerator portion,
            /// we're using a method because it has the side-effect of resetting the count, but in general the two members
            /// used for a "Fraction" type do not need to be the same member type or the same access level.</para></remarks>
            /// <returns>The number of operations applied since the previous sample.</returns>
            [SampledMetricDivisor("averageMagnitude")]
            public int MagnitudeDivisor()
            {
                int divisor = m_MagnitudesCount;
                m_MagnitudesCount = 0; // Reset count for next sample interval.
                return divisor;
            }

            /// <summary>
            /// The number of operations applied.
            /// </summary>
            /// <remarks>This counter tracks the number of operations in a sampling interval.  Notice that we actually
            /// never reset the value, however, so our sampling type is SamplingType.TotalCount.  This means that we
            /// return the total count, but the analysis will calculate the delta for the start and end of a sampling
            /// interval when displaying and thus graph the "rate" of operations.  This also saves us from having to reset
            /// the value upon sampling, which can be less risky than the "Incremental" types.  This could be interesting
            /// to compare against the "temperature" graph to see how temperature varies with operation rate.</remarks>
            [SampledMetricValue("operationCount", SamplingType.TotalCount, null, Caption = "Operation Count",
                Description = "This tracks the number of operations performed in a given interval.")]
            public int OperationCount { get { return m_OperationCount; } }

            #region IDisposable Members

            /// <summary>
            /// Dispose of this instance and end the polling thread.
            /// </summary>
            /// <remarks>This is the Microsoft-recommended pattern for IDisposable.  The finalizer (done for us by
            /// default) will call Dispose(false), and our Dispose() will call Dispose(true).  This puts the disposal
            /// logic in one place to handle both cases and allows for all inheritance levels to cleanup as well.</remarks>
            public void Dispose()
            {
                Dispose(true);
            }

            protected virtual void Dispose(bool disposing) // Or override, if we had a base class implementing it also.
            {
                //base.Dispose(disposing); // But we have no base, so this doesn't exist.

                if (disposing)
                {
                    SampledMetric.Write(this); // Write one last sample when we're disposed.
                }

                m_EndThread = true; // This needs to be done by the finalizer as well as when disposed.
            }

            #endregion

            private void SamplePollingThreadStart()
            {
                Trace.TraceInformation("Example polling thread ({0}) started", m_InstanceName ?? "null");

                while (m_EndThread == false)
                {
                    SampledMetric.Write(this); // Write a sample of all sampled metrics defined by attributes on this object.

                    Thread.Sleep(100); // Sleep for 0.1 seconds before sampling again.
                }

                Trace.TraceInformation("Example polling thread ({0}) ending", m_InstanceName ?? "null");
                m_PollingThread = null; // Exiting thread, mark us as no longer polling.
            }
        }
        #endregion

        /// <summary>
        /// Several alternatives for example client code to utilize sampled metrics through reflection with attributes.
        /// </summary>
        [Test]
        public void SampledAttributeReflectionExample()
        {
            //
            // General example usage.
            //

            // Optional registration in advance (time-consuming reflection walk to find attributes the first time).

            // Either of these two approaches will dig into base types and all interfaces seen at the top level.
            SampledMetric.Register(typeof(UserSampledAttributedClass)); // Can be registered from the Type itself.
            // Or...
            UserSampledAttributedClass anyUserSampledAttributedInstance = new UserSampledAttributedClass("any-prototype");
            SampledMetric.Register(anyUserSampledAttributedInstance); // Can register from a live instance (gets its Type automatically).

            // Elsewhere...

            UserSampledAttributedClass userSampledAttributedInstance = new UserSampledAttributedClass("automatic-instance-name"); // then...

            // To sample all valid sampled metrics defined by attributes (at all inheritance levels) for a data object instance:

            // This will determine the instance name automatically from the member marked with the EventMetricInstanceName attribute.
            // Null will be used as the instance name (the "default instance") if the attribute is not found for a particular metric.
            SampledMetric.Write(userSampledAttributedInstance); // The recommended typical usage.

            // Or... To specify a different fallback instance name if an EventMetricInstanceName attribute isn't found:
            SampledMetric.Write(userSampledAttributedInstance, "fallback-instance-if-not-assigned");



            //
            // Specific example usage for example class above.
            // Generate some meaningful data in the log to look at.
            //

            int[] testDataArray = new[] { 1, 5, 3, -4, 2, 7, -3, -2, 9, 4, -5, -1, 3, -7, 2, 4, -2, 8, 10, -4, 2 };

            // Using the "default" instance here.  This will Dispose it for us when it exits the block.
            using (UserSampledAttributedClass realUsageInstance = new UserSampledAttributedClass(null))
            {
                SampledMetric.Register(realUsageInstance); // Registering from the live object also registers the metric instance.
                realUsageInstance.StartPolling(); // Start polling thread for this one.
                
                foreach (int dataValue in testDataArray)
                {
                    realUsageInstance.ApplyDelta(dataValue); // This method also fires off an event metric sample for us.

                    Thread.Sleep(50 + (5 * dataValue)); // Sleep for a little while to space out the data, not entirely evenly for this example.
                }
            }

            Thread.Sleep(1000); // Give it some time to complete.
        }

        [Test]
        public void TestEventAttributeReflection()
        {
            SampledMetric.Register(typeof(UserSampledObject));

            UserSampledObject sampledObject = new UserSampledObject(25, 100);

            SampledMetricDefinition.Write(sampledObject);

            SampledMetricDefinition sampledMetricDefinition;
            Assert.IsTrue(SampledMetricDefinition.TryGetValue(typeof(UserSampledObject), "IncrementalCount", out sampledMetricDefinition));

            sampledObject.SetValue(35, 90);
            sampledMetricDefinition.WriteSample(sampledObject);
        }

        [Test]
        public void PerformanceTest()
        {
            //first, lets get everything to flush so we have our best initial state.
            Log.EndFile("Preparing for Performance Test");
            Log.Information(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.Performance", "Preparing for Test", "Flushing queue");

            SampledMetric.Register(typeof(UserSampledObject));

            UserSampledObject sampledObject = new UserSampledObject(25, 100);

            //now that we know it's flushed everything, lets do our timed loop.
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            for (int curMessage = 0; curMessage < MessagesPerTest; curMessage++)
            {
                SampledMetricDefinition.Write(sampledObject);
            }
            DateTimeOffset messageEndTime = DateTimeOffset.UtcNow;

            //one wait for commit message to force the buffer to flush.
            Log.Information(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.Performance", "Waiting for Samples to Commit", null);

            //and store off our time
            DateTimeOffset endTime = DateTimeOffset.UtcNow;

            TimeSpan duration = endTime - startTime;

            Trace.TraceInformation("Sampled Metrics by Attribute Test Completed in {0}ms .  {1} messages were written at an average duration of {2}ms per message.  The flush took {3}ms.",
                                   duration.TotalMilliseconds, MessagesPerTest, (duration.TotalMilliseconds) / MessagesPerTest, (endTime - messageEndTime).TotalMilliseconds);
                       
        }
    }
}
