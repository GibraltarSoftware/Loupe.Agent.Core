using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Gibraltar.Agent;
using Gibraltar.Agent.Metrics;
using Loupe.Logging;
using Loupe.Metrics;
using NUnit.Framework;

namespace Loupe.Agent.Test.Metrics
{
    [TestFixture]
    public class SampledMetricsByMethodsTests
    {
        private const int MessagesPerTest = 10000;

        [Test]
        public void RegistrationConsistency()
        {
            Assert.IsTrue(true);
            bool question = SampledMetricDefinition.IsValidDataType(typeof(int));
            Assert.IsTrue(question);
            question = SampledMetricDefinition.IsValidDataType(GetType());
            Assert.IsFalse(question);

            // Create a sampled metric definition to work with.
            SampledMetricDefinition createdDefinitionOne = SampledMetricDefinition.Register("RegistrationConsistency",
                "Methods.Unit Test Data.Consistency", "Metric One", SamplingType.RawCount, null, "First metric",
                "The first sampled metric definition for this test.");
            Assert.IsNotNull(createdDefinitionOne);

            // Get the same definition with different caption and description.
            SampledMetricDefinition obtainedDefinitionOne = SampledMetricDefinition.Register("RegistrationConsistency",
                "Methods.Unit Test Data.Consistency", "Metric One", SamplingType.RawCount, null, "The first metric",
                "This is the first sampled metric definition for this test.");
            Assert.IsNotNull(obtainedDefinitionOne);

            Assert.AreSame(createdDefinitionOne, obtainedDefinitionOne,
                "Second call to SampledMetricDefinition.Register() did not get the same definition object as the first. \"{0}\" vs \"{1}\".",
                createdDefinitionOne.Caption, obtainedDefinitionOne.Caption);
            Assert.AreEqual("First metric", createdDefinitionOne.Caption);
            Assert.AreEqual("The first sampled metric definition for this test.", createdDefinitionOne.Description);

            // Get an instance from the definition.
            SampledMetric createdInstanceOneA = SampledMetric.Register(createdDefinitionOne, "Instance One A");
            Assert.IsNotNull(createdInstanceOneA);

            Assert.AreSame(createdDefinitionOne, createdInstanceOneA.Definition,
                "Created instance does not point to the same definition object used to create it. \"{0}\" vs \"{1}\".",
                createdDefinitionOne.Caption, createdInstanceOneA.Definition.Caption);
            Assert.AreEqual("Instance One A", createdInstanceOneA.InstanceName);

            // Get the same instance the same way again.
            SampledMetric obtainedInstanceOneA = SampledMetric.Register(createdDefinitionOne, "Instance One A");
            Assert.IsNotNull(obtainedInstanceOneA);

            Assert.AreSame(createdInstanceOneA, obtainedInstanceOneA,
                "Second call to SampledMetric.Register() did not get the same metric instance object as the first. \"{0}\" vs \"{1}\".",
                createdInstanceOneA.InstanceName, obtainedInstanceOneA.InstanceName);

            // Get the same instance directly.
            obtainedInstanceOneA = SampledMetric.Register("RegistrationConsistency",
                "Methods.Unit Test Data.Consistency", "Metric One", SamplingType.RawCount, null, "Metric #1",
                "This is metric definition #1 for this test.", "Instance One A");
            Assert.IsNotNull(obtainedInstanceOneA);
            Assert.IsNotNull(obtainedInstanceOneA.Definition);

            Assert.AreSame(createdInstanceOneA, obtainedInstanceOneA,
                "Third call to SampledMetric.Register() did not get the same metric instance object as the first. \"{0}\" vs \"{1}\".",
                createdInstanceOneA.InstanceName, obtainedInstanceOneA.InstanceName);

            // Get a second instance from the definition.
            SampledMetric createdInstanceOneB = SampledMetric.Register(createdDefinitionOne, "Instance One B");
            Assert.IsNotNull(createdInstanceOneB);
            Assert.IsNotNull(createdInstanceOneB.Definition);

            Assert.AreSame(createdDefinitionOne, createdInstanceOneB.Definition,
                "Created instance does not point to the same definition object used to create it. \"{0}\" vs \"{1}\".",
                createdDefinitionOne.Caption, createdInstanceOneB.Definition.Caption);
            Assert.AreEqual("Instance One B", createdInstanceOneB.InstanceName);

            Assert.AreNotSame(createdInstanceOneA, createdInstanceOneB,
                "Different metric instances should never be the same object.");
            Assert.AreNotEqual(createdInstanceOneA, createdInstanceOneB,
                "Different metric instances should not test as being equal. \"{0}\" vs \"{1}\".",
                createdInstanceOneA.InstanceName, createdInstanceOneB.InstanceName);

            Assert.IsTrue(createdInstanceOneA.Key.StartsWith(createdDefinitionOne.Key),
                "Instance Key does not start with definition Key. \"{0}\" vs \"{1}\".",
                createdInstanceOneA.Key, createdDefinitionOne.Key);
            Assert.IsTrue(createdInstanceOneA.Key.EndsWith(createdInstanceOneA.InstanceName),
                "Instance Key does not end with instance name. \"{0}\" vs \"{1}\".",
                createdInstanceOneA.Key, createdInstanceOneA.InstanceName);
            Assert.IsTrue(createdInstanceOneB.Key.StartsWith(createdDefinitionOne.Key),
                "Instance Key does not start with definition Key. \"{0}\" vs \"{1}\".",
                createdInstanceOneB.Key, createdDefinitionOne.Key);
            Assert.IsTrue(createdInstanceOneB.Key.EndsWith(createdInstanceOneB.InstanceName),
                "Instance Key does not end with instance name. \"{0}\" vs \"{1}\".",
                createdInstanceOneB.Key, createdInstanceOneB.InstanceName);



            // Try a different definition, directly to an instance first.
            SampledMetric createdInstanceTwoA = SampledMetric.Register("RegistrationConsistency",
                "Methods.Unit Test Data.Consistency", "Metric Two", SamplingType.RawCount, null, "Second metric",
                "The second sampled metric definition for this test.", "Instance Two A");
            Assert.IsNotNull(createdInstanceTwoA);

            // Check it's definition, created automatically.
            SampledMetricDefinition createdDefinitionTwo = createdInstanceTwoA.Definition;
            Assert.IsNotNull(createdDefinitionTwo);

            // Get the same instance, with a different caption and description.
            SampledMetric obtainedInstanceTwoA = SampledMetric.Register("RegistrationConsistency",
                "Methods.Unit Test Data.Consistency", "Metric Two", SamplingType.RawCount, null, "The second metric",
                "This is the second sampled metric definition for this test.", "Instance Two A");
            Assert.IsNotNull(obtainedInstanceTwoA);
            Assert.AreSame(createdDefinitionTwo, obtainedInstanceTwoA.Definition,
                "Instances of the same metric do not point to the same definition object. \"{0}\" vs \"{1}\".",
                createdDefinitionTwo.Caption, obtainedInstanceTwoA.Definition.Caption);

            // Get the same definition another way.
            SampledMetricDefinition obtainedDefinitionTwo = SampledMetricDefinition.Register("RegistrationConsistency",
                "Methods.Unit Test Data.Consistency", "Metric Two", SamplingType.RawCount, null, "Metric #2",
                "This is metric definition #2 for this test.");
            Assert.IsNotNull(obtainedDefinitionTwo);

            Assert.AreSame(createdDefinitionTwo, obtainedDefinitionTwo,
                "Call to SampledMetricDefinition.Register() did not get the same definition as the direct instances. \"{0}\" vs \"{1}\".",
                createdDefinitionTwo.Caption, obtainedDefinitionTwo.Caption);
            Assert.AreEqual("Second metric", obtainedDefinitionTwo.Caption);
            Assert.AreEqual("The second sampled metric definition for this test.", obtainedDefinitionTwo.Description);

            Assert.IsTrue(createdInstanceTwoA.Key.StartsWith(createdDefinitionTwo.Key),
                "Instance Key does not start with definition Key. \"{0}\" vs \"{1}\".",
                createdInstanceTwoA.Key, createdDefinitionTwo.Key);
            Assert.IsTrue(createdInstanceTwoA.Key.EndsWith(createdInstanceTwoA.InstanceName),
                "Instance Key does not end with instance name. \"{0}\" vs \"{1}\".",
                createdInstanceTwoA.Key, createdInstanceTwoA.InstanceName);

            Assert.AreNotSame(createdDefinitionOne, createdDefinitionTwo,
                "Different metric definitions should never be the same object.");
            Assert.AreNotEqual(createdDefinitionOne, createdDefinitionTwo,
                "Different metric definitions should not test as being equal. \"{0}\" vs \"{1}\".",
                createdDefinitionOne.Key, createdDefinitionTwo.Key);



            // Create instance from null, then from empty string.
            SampledMetric instanceOneNull = SampledMetric.Register(createdDefinitionOne, null);
            SampledMetric instanceOneEmpty = SampledMetric.Register(createdDefinitionOne, string.Empty);
            Assert.IsNotNull(instanceOneNull);
            Assert.IsNotNull(instanceOneEmpty);
            Assert.AreSame(instanceOneNull, instanceOneEmpty,
                "Null instance and empty instance are not the same metric object. {0} vs {1}.",
                (instanceOneNull.InstanceName == null) ? "(null)" : "\"" + instanceOneNull.InstanceName + "\"",
                (instanceOneEmpty.InstanceName == null) ? "(null)" : "\"" + instanceOneEmpty.InstanceName + "\"");

            // Create instance from empty string, then from null.
            SampledMetric instanceTwoEmpty = SampledMetric.Register(createdDefinitionTwo, string.Empty);
            SampledMetric instanceTwoNull = SampledMetric.Register(createdDefinitionTwo, null);
            Assert.IsNotNull(instanceTwoEmpty);
            Assert.IsNotNull(instanceTwoNull);
            Assert.AreSame(instanceTwoEmpty, instanceTwoNull,
                "Empty instance and null instance are not the same metric object. {0} vs {1}.",
                (instanceTwoEmpty.InstanceName == null) ? "(null)" : "\"" + instanceTwoEmpty.InstanceName + "\"",
                (instanceTwoNull.InstanceName == null) ? "(null)" : "\"" + instanceTwoNull.InstanceName + "\"");

            Assert.IsTrue(instanceOneNull.IsDefault);
            Assert.IsTrue(instanceOneEmpty.IsDefault);
            Assert.IsTrue(instanceTwoEmpty.IsDefault);
            Assert.IsTrue(instanceTwoNull.IsDefault);

            Assert.IsNull(instanceOneNull.InstanceName);
            Assert.IsNull(instanceOneEmpty.InstanceName);
            Assert.IsNull(instanceTwoEmpty.InstanceName);
            Assert.IsNull(instanceTwoNull.InstanceName);



            SampledMetricDefinition createdDefinitionThree = SampledMetricDefinition.Register("RegistrationConsistency", 
                "Methods.Unit Test Data.Consistency", "Metric Three", SamplingType.IncrementalCount, null, "Third metric",
                "An IncrementalCount metric.");
            Assert.IsNotNull(createdDefinitionThree);
            SampledMetricDefinition obtainedDefinitionThree;
            try
            {
                obtainedDefinitionThree = SampledMetricDefinition.Register("RegistrationConsistency",
                    "Methods.Unit Test Data.Consistency", "Metric Three", SamplingType.RawCount, null, "Third metric",
                    "A RawCount metric.");
                Assert.IsNotNull(obtainedDefinitionThree); // This should never actually be executed.
            }
            catch (ArgumentException ex)
            {
                Trace.TraceInformation("SampledMetricDefinition.Register() with inconsistent sampling type threw expected exception.", ex);
                obtainedDefinitionThree = null;
            }
            Assert.IsNull(obtainedDefinitionThree); // Confirm we went through the catch.

            SampledMetric createdInstanceThreeA;
            try
            {
                createdInstanceThreeA = SampledMetric.Register("RegistrationConsistency",
                    "Methods.Unit Test Data.Consistency", "Metric Three", SamplingType.TotalCount, null, "Third metric",
                    "A TotalCount metric.", "Instance Three A");
                Assert.IsNotNull(createdInstanceThreeA); // This should never actually be executed.
            }
            catch (ArgumentException ex)
            {
                Trace.TraceInformation("SampledMetric.Register() with inconsistent sampling type threw expected exception.", ex);
                createdInstanceThreeA = null;
            }
            Assert.IsNull(createdInstanceThreeA); // Confirm we went through the catch.

            obtainedDefinitionThree = SampledMetricDefinition.Register("RegistrationConsistency",
                "Methods.Unit Test Data.Consistency", "Metric Three", SamplingType.IncrementalCount, "seconds", "Third metric",
                "An IncrementalCount of seconds metric.");
            Assert.IsNotNull(obtainedDefinitionThree);
            //Assert.IsNull(obtainedDefinitionThree.UnitCaption); // Bug: This is getting "Count of items" instead of null?

            SampledMetric createdInstanceFour = SampledMetric.Register("RegistrationConsistency",
                "Methods.Unit Test Data.Consistency", "Metric Four", SamplingType.IncrementalFraction, "hits per minute",
                "Third metric", "An IncrementalCount of seconds metric.", null);
            Assert.IsNotNull(createdInstanceFour);
            Assert.AreEqual("hits per minute", createdInstanceFour.UnitCaption);

            SampledMetricDefinition obtainedDefinitionFour = SampledMetricDefinition.Register("RegistrationConsistency",
                "Methods.Unit Test Data.Consistency", "Metric Four", SamplingType.IncrementalFraction, "hits per hour",
                "Third metric", "An IncrementalCount of seconds metric.");
            Assert.IsNotNull(obtainedDefinitionFour);
            Assert.AreEqual("hits per minute", obtainedDefinitionFour.UnitCaption);
        }

        [Test]
        public void BasicMetricUsage()
        {
            Log.TraceVerbose("Registering new sampled metric definitions");
            //go ahead and register a few metrics
            //int curMetricDefinitionCount = Log.MetricDefinitions.Count;

            SampledMetricDefinition incrementalCountDefinition = SampledMetricDefinition.Register("BasicMetricUsage",
                "Methods.Unit Test Data.Long", "IncrementalCount", SamplingType.IncrementalCount, null, "Incremental Count",
                "Unit test sampled metric using the incremental count calculation routine.");

            SampledMetricDefinition incrementalFractionDefinition = SampledMetricDefinition.Register("BasicMetricUsage",
                "Methods.Unit Test Data.Long", "IncrementalFraction", SamplingType.IncrementalFraction, null, "Incremental Fraction",
                "Unit test sampled metric using the incremental fraction calculation routine.  Rare, but fun.");

            SampledMetricDefinition totalCountDefinition = SampledMetricDefinition.Register("BasicMetricUsage",
                "Methods.Unit Test Data.Long", "TotalCount", SamplingType.TotalCount, null, "Total Count",
                "Unit test sampled metric using the Total Count calculation routine.  Very common.");

            SampledMetricDefinition totalFractionDefinition = SampledMetricDefinition.Register("BasicMetricUsage",
                "Methods.Unit Test Data.Long", "TotalFraction", SamplingType.TotalFraction, null, "Total Fraction",
                "Unit test sampled metric using the Total Fraction calculation routine.  Rare, but rounds us out.");

            SampledMetricDefinition rawCountDefinition = SampledMetricDefinition.Register("BasicMetricUsage",
                "Methods.Unit Test Data.Long", "RawCount", SamplingType.RawCount, null, "Raw Count",
                "Unit test sampled metric using the Raw Count calculation routine, which we will then average to create sample intervals.");

            SampledMetricDefinition rawFractionDefinition = SampledMetricDefinition.Register("BasicMetricUsage",
                "Methods.Unit Test Data.Long", "RawFraction", SamplingType.RawFraction, null, "Raw Fraction",
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

            //and finally, lets go log some data!
            Log.TraceVerbose("And now lets log data");
            
            //lets add 10 values, a few milliseconds apart
            int curSamplePass = 0;
            while (curSamplePass < 10)
            {
                //We're putting in fairly bogus data, but it will produce a consistent output.
                incrementalCountMetric.WriteSample(curSamplePass * 20);
                incrementalFractionMetric.WriteSample(curSamplePass * 20, curSamplePass * 30);
                totalCountMetric.WriteSample(curSamplePass * 20);
                totalFractionMetric.WriteSample(curSamplePass * 20, curSamplePass * 30);
                rawCountMetric.WriteSample(curSamplePass);
                rawFractionMetric.WriteSample(curSamplePass, 10.0);

                curSamplePass++;
                Thread.Sleep(100);
            }

            Log.TraceVerbose("Completed logging metric samples.");
        }

        [Test]
        public void SimpleMetricUsage()
        {
            Log.TraceVerbose("Registering new sampled metric definition");
            //create one sampled metric definition using the "make a definition for the current log set" override
            SampledMetricDefinition temperatureTracking = SampledMetricDefinition.Register("SimpleMetricUsage", "Methods.Temperature", "Experiment Temperature", SamplingType.RawCount, null, "Temperature", "This is an example from iControl where we want to track the temperature of a reaction or some such thing.");

            //create a set of METRICS (definition + metric) using the static add metric capability
            Log.TraceVerbose("Registering metric instances directly");

            SampledMetric incrementalCountMetric = SampledMetric.Register("SimpleMetricUsage", "Methods.Unit Test Data.Direct",
                "IncrementalCount", SamplingType.IncrementalCount, null, "Incremental Count",
                "Unit test sampled metric using the incremental count calculation routine.", null);

            SampledMetric incrementalFractionMetric = SampledMetric.Register("SimpleMetricUsage", "Methods.Unit Test Data.Direct",
                "IncrementalFraction", SamplingType.IncrementalFraction, null, "Incremental Fraction",
                "Unit test sampled metric using the incremental fraction calculation routine.  Rare, but fun.", null);

            SampledMetric totalCountMetric = SampledMetric.Register("SimpleMetricUsage", "Methods.Unit Test Data.Direct",
                "TotalCount", SamplingType.TotalCount, null, "Total Count",
                "Unit test sampled metric using the Total Count calculation routine.  Very common.", null);

            SampledMetric totalFractionMetric = SampledMetric.Register("SimpleMetricUsage", "Methods.Unit Test Data.Direct",
                "TotalFraction", SamplingType.TotalFraction, null, "Total Fraction",
                "Unit test sampled metric using the Total Fraction calculation routine.  Rare, but rounds us out.", null);

            SampledMetric rawCountMetric = SampledMetric.Register("SimpleMetricUsage", "Methods.Unit Test Data.Direct",
                "RawCount", SamplingType.RawCount, null, "Raw Count",
                "Unit test sampled metric using the Raw Count calculation routine, which we will then average to create sample intervals.", null);

            SampledMetric rawFractionMetric = SampledMetric.Register("SimpleMetricUsage", "Methods.Unit Test Data.Direct",
                "RawFraction", SamplingType.RawFraction, null, "Raw Fraction",
                "Unit test sampled metric using the Raw Fraction calculation routine.  Fraction types aren't common.", null);

            // These should never be null, but let's check to confirm.
            Assert.IsNotNull(incrementalCountMetric);
            Assert.IsNotNull(incrementalFractionMetric);
            Assert.IsNotNull(totalCountMetric);
            Assert.IsNotNull(totalFractionMetric);
            Assert.IsNotNull(rawCountMetric);
            Assert.IsNotNull(rawFractionMetric);

            Log.TraceVerbose("And now lets log data");

            //lets add 10 values, a few milliseconds apart
            int curSamplePass = 0;
            while (curSamplePass < 10)
            {
                //the temperature tracking one is set up so we can write to instances directly from a definition.
                temperatureTracking.WriteSample(string.Format(CultureInfo.CurrentCulture, "Experiment {0}", 1), curSamplePass);
                temperatureTracking.WriteSample(string.Format(CultureInfo.CurrentCulture, "Experiment {0}", 2), curSamplePass);
                temperatureTracking.WriteSample(string.Format(CultureInfo.CurrentCulture, "Experiment {0}", 3), curSamplePass);
                temperatureTracking.WriteSample(string.Format(CultureInfo.CurrentCulture, "Experiment {0}", 4), curSamplePass);
                temperatureTracking.WriteSample(string.Format(CultureInfo.CurrentCulture, "Experiment {0}", 5), curSamplePass);
                temperatureTracking.WriteSample(string.Format(CultureInfo.CurrentCulture, "Experiment {0}", 6), curSamplePass);

                incrementalCountMetric.WriteSample(curSamplePass * 20);
                incrementalFractionMetric.WriteSample(curSamplePass * 20, curSamplePass * 30);
                totalCountMetric.WriteSample(curSamplePass * 20);
                totalFractionMetric.WriteSample(curSamplePass * 20, curSamplePass * 30);
                rawCountMetric.WriteSample(curSamplePass);
                rawFractionMetric.WriteSample(curSamplePass, 10.0);

                curSamplePass++;
                Thread.Sleep(100);
            }
            Log.TraceVerbose("Completed logging metric samples.");
        }


        [Test]
        public void PerformanceTest()
        {
            // Start a new session file so it won't do maintenance in the middle of our test.
            Log.EndFile("Preparing for Performance Test");

            Log.TraceVerbose("Registering new sampled metric definitions");
            //go ahead and register a few metrics
            //int curMetricDefinitionCount = Log.MetricDefinitions.Count;

            SampledMetricDefinition incrementalCountDefinition = SampledMetricDefinition.Register("BasicMetricUsage",
                "Methods.Unit Test Data.Long", "IncrementalCount", SamplingType.IncrementalCount, null, "Incremental Count",
                "Unit test sampled metric using the incremental count calculation routine.");

            SampledMetricDefinition incrementalFractionDefinition = SampledMetricDefinition.Register("BasicMetricUsage",
                "Methods.Unit Test Data.Long", "IncrementalFraction", SamplingType.IncrementalFraction, null, "Incremental Fraction",
                "Unit test sampled metric using the incremental fraction calculation routine.  Rare, but fun.");

            SampledMetricDefinition totalCountDefinition = SampledMetricDefinition.Register("BasicMetricUsage",
                "Methods.Unit Test Data.Long", "TotalCount", SamplingType.TotalCount, null, "Total Count",
                "Unit test sampled metric using the Total Count calculation routine.  Very common.");

            SampledMetricDefinition totalFractionDefinition = SampledMetricDefinition.Register("BasicMetricUsage",
                "Methods.Unit Test Data.Long", "TotalFraction", SamplingType.TotalFraction, null, "Total Fraction",
                "Unit test sampled metric using the Total Fraction calculation routine.  Rare, but rounds us out.");

            SampledMetricDefinition rawCountDefinition = SampledMetricDefinition.Register("BasicMetricUsage",
                "Methods.Unit Test Data.Long", "RawCount", SamplingType.RawCount, null, "Raw Count",
                "Unit test sampled metric using the Raw Count calculation routine, which we will then average to create sample intervals.");

            SampledMetricDefinition rawFractionDefinition = SampledMetricDefinition.Register("BasicMetricUsage",
                "Methods.Unit Test Data.Long", "RawFraction", SamplingType.RawFraction, null, "Raw Fraction",
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
            for (int curMessage = 0; curMessage < MessagesPerTest; curMessage++)
            {
                //We're putting in fairly bogus data, but it will produce a consistent output.
                incrementalCountMetric.WriteSample(20);
                incrementalFractionMetric.WriteSample(20, 30);
                totalCountMetric.WriteSample(20);
                totalFractionMetric.WriteSample(20,  30);
                rawCountMetric.WriteSample(20);
                rawFractionMetric.WriteSample(10.0);
            }
            DateTimeOffset messageEndTime = DateTimeOffset.UtcNow;

            //one wait for commit message to force the buffer to flush.
            Log.Information(LogWriteMode.WaitForCommit, "Test.Agent.Metrics.Performance", "Waiting for Samples to Commit", null);

            //and store off our time
            DateTimeOffset endTime = DateTimeOffset.UtcNow;

            TimeSpan testDuration = endTime - startTime;
            TimeSpan loopDuration = messageEndTime - startTime;

            Trace.TraceInformation("Sampled Metrics by Method Test committed {0} samples in {1} ms (average {2} ms per message).  Average loop time {3} ms and final flush time {4} ms.",
                                   MessagesPerTest, testDuration.TotalMilliseconds, (testDuration.TotalMilliseconds) / MessagesPerTest,
                                   loopDuration.TotalMilliseconds / MessagesPerTest, (endTime - messageEndTime).TotalMilliseconds);

        }
    }
}
