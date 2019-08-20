using System;
using System.Collections.Generic;
using System.Globalization;
using Loupe.Core.Metrics;
using Loupe.Core.Monitor;
using Loupe.Extensibility.Data;
using Loupe.Metrics;
using NUnit.Framework;

namespace Loupe.Core.Test.Core
{
    [TestFixture]
    public class GeneralMetricCollectionTests
    {
        private static MetricDefinition GetTestMetricDefinition()
        {
            return (MetricDefinition)Log.Metrics["GeneralMetricCollectionTests", "Gibraltar.Monitor.Test", "Manual"];
        }

        private static Metric GetTestMetric()
        {
            return (Metric)((MetricCollection)GetTestMetricDefinition().Metrics)[null];
        }

        /// <summary>
        /// Create an event metric for some specific tests we run that test looking for a metric.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            IMetricDefinition newMetricDefinition;
            EventMetricDefinition newEventMetricDefinition;

            // See if we already created the event metric we need
            if (Log.Metrics.TryGetValue("GeneralMetricCollectionTests", "Gibraltar.Monitor.Test", "Manual", out newMetricDefinition) == false)
            {
                // Didn't find it, so define an event metric manually (the hard way)
                newEventMetricDefinition = new EventMetricDefinition("GeneralMetricCollectionTests", "Gibraltar.Monitor.Test", "Manual");
                newMetricDefinition = newEventMetricDefinition; // cast it as the base type, too

                // we now have a minimal definition, but we probably want to add a few attributes to make it useful
                // NOTE:  This is designed to exactly match UserDataObject for convenience in analzing results.
                EventMetricValueDefinitionCollection valueDefinitions = (EventMetricValueDefinitionCollection)newEventMetricDefinition.Values;
                valueDefinitions.Add("short_average", typeof(short), "Short Average", "Data of type Short").DefaultTrend = SummaryFunction.Average;
                valueDefinitions.Add("short_sum", typeof(short), "Short Sum", "Data of type Short").DefaultTrend = SummaryFunction.Sum;
                valueDefinitions.Add("ushort_average", typeof(ushort), "UShort Average", "Data of type UShort").DefaultTrend = SummaryFunction.Average;
                valueDefinitions.Add("ushort_sum", typeof(ushort), "UShort Sum", "Data of type UShort").DefaultTrend = SummaryFunction.Sum;
                valueDefinitions.Add("int_average", typeof(int), "Int Average", "Data of type Int").DefaultTrend = SummaryFunction.Average;
                valueDefinitions.Add("int_sum", typeof(int), "Int Sum", "Data of type Int").DefaultTrend = SummaryFunction.Sum;
                valueDefinitions.Add("uint_average", typeof(uint), "UInt Average", "Data of type UInt").DefaultTrend = SummaryFunction.Average;
                valueDefinitions.Add("uint_sum", typeof(uint), "UInt Sum", "Data of type UInt").DefaultTrend = SummaryFunction.Sum;
                valueDefinitions.Add("long_average", typeof(long), "Long Average", "Data of type Long").DefaultTrend = SummaryFunction.Average;
                valueDefinitions.Add("long_sum", typeof(long), "Long Sum", "Data of type Long").DefaultTrend = SummaryFunction.Sum;
                valueDefinitions.Add("ulong_average", typeof(ulong), "ULong Average", "Data of type ULong").DefaultTrend = SummaryFunction.Average;
                valueDefinitions.Add("ulong_sum", typeof(ulong), "ULong Sum", "Data of type ULong").DefaultTrend = SummaryFunction.Sum;
                valueDefinitions.Add("decimal_average", typeof(decimal), "Decimal Average", "Data of type Decimal").DefaultTrend = SummaryFunction.Average;
                valueDefinitions.Add("decimal_sum", typeof(decimal), "Decimal Sum", "Data of type Decimal").DefaultTrend = SummaryFunction.Sum;
                valueDefinitions.Add("double_average", typeof(double), "Double Average", "Data of type Double").DefaultTrend = SummaryFunction.Average;
                valueDefinitions.Add("double_sum", typeof(double), "Double Sum", "Data of type Double").DefaultTrend = SummaryFunction.Sum;
                valueDefinitions.Add("float_average", typeof(float), "Float Average", "Data of type Float").DefaultTrend = SummaryFunction.Average;
                valueDefinitions.Add("float_sum", typeof(float), "Float Sum", "Data of type Float").DefaultTrend = SummaryFunction.Sum;
                valueDefinitions.Add("string", typeof(string), "String", "Data of type String");
                valueDefinitions.Add("system.enum", typeof(System.Enum), "System.Enum", "Data of type System.Enum");

                newEventMetricDefinition.DefaultValue = newEventMetricDefinition.Values["int_average"];
                newEventMetricDefinition = newEventMetricDefinition.Register(); // Register it with the collection.
            }
            else
            {
                // Found one, try to cast it to the expected EventMetricDefinition type (raise exception if fails to match)
                newEventMetricDefinition = (EventMetricDefinition)newMetricDefinition;
            }

            IMetric newMetric;
            if (newMetricDefinition.Metrics.TryGetValue(null, out newMetric) == false)
            {
                // go ahead and add this new metric
                newMetric = new EventMetric(newEventMetricDefinition, (string)null); //add the default metric.
            }

            Assert.IsNotNull(newMetricDefinition);
            Assert.IsNotNull(newEventMetricDefinition);
            Assert.IsNotNull(newMetric);
        }


        /// <summary>
        /// Make sure we can look up definitions by the range of ways it's possible
        /// </summary>
        [Test]
        public void MetricDefinitionObjectLookup()
        {
            MetricDefinition lookupMetricDefinition = GetTestMetricDefinition();

            //look it up by GUID
            Assert.AreSame(lookupMetricDefinition, Log.Metrics[lookupMetricDefinition.Id], "Failed to find same object when looking by Id");
            Assert.AreSame(lookupMetricDefinition, Log.Metrics[lookupMetricDefinition.Name], "Failed to find same object when looking by Name");
            Assert.AreSame(lookupMetricDefinition, Log.Metrics[lookupMetricDefinition.MetricTypeName, lookupMetricDefinition.CategoryName, lookupMetricDefinition.CounterName], "Failed to find same object when looking by Key Components");
        }

        /// <summary>
        /// Make sure we can look up metrics by the range of ways that's possible.
        /// </summary>
        [Test]
        public void MetricObjectLookup()
        {
            MetricDefinition lookupMetricDefinition = GetTestMetricDefinition();
            Metric lookupMetric = GetTestMetric();

            Assert.AreSame(lookupMetric, Log.Metrics.Metric(lookupMetric.Id), "Failed to find metric in Log.Metrics Metric cache");
            Assert.AreSame(lookupMetric, lookupMetricDefinition.Metrics[lookupMetric.Id], "Failed to find same object when looking by Id");
            Assert.AreSame(lookupMetric, lookupMetricDefinition.Metrics[lookupMetric.InstanceName], "Failed to find same object when looking by Instance Name");
            Assert.AreSame(lookupMetric, lookupMetricDefinition.Metrics[lookupMetric.Name], "Failed to find same object when looking by Full Name");
        }


        [Test]
        public void MetricDefinitionCollectionUnderrun()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                IMetricDefinition testMetricDefinition = Log.Metrics[-1];
            });
        }

        [Test]
        public void MetricDefinitionCollectionOverrun()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                IMetricDefinition testMetricDefinition = Log.Metrics[Log.Metrics.Count];
            });
        }

        [Test]
        public void MetricCollectionUnderrun()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                MetricDefinition testMetricDefinition = GetTestMetricDefinition();
                IMetric testMetric = testMetricDefinition.Metrics[-1];
            });
        }

        [Test]
        public void MetricCollectionOverrun()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                MetricDefinition testMetricDefinition = GetTestMetricDefinition();
                IMetric testMetric = testMetricDefinition.Metrics[-1];
            });
        }

        [Test]
        public void MetricCollectionStringKeyMiss()
        {
            Assert.Throws<KeyNotFoundException>(() =>
            {
                MetricDefinition testMetricDefinition = GetTestMetricDefinition();
                IMetric testMetric = testMetricDefinition.Metrics["ThisKeyShouldNeverBeHere"];
            });
        }

        [Test]
        public void MetricCollectionGuidKeyMiss()
        {
            Assert.Throws<KeyNotFoundException>(() =>
            {
                MetricDefinition testMetricDefinition = GetTestMetricDefinition();
                IMetric testMetric = testMetricDefinition.Metrics[Guid.NewGuid()];
            });
        }


        [Test]
        public void MetricCollectionFindByIndex()
        {
            MetricDefinition testMetricDefinition = GetTestMetricDefinition();
            IMetric testMetric = testMetricDefinition.Metrics[0];
            Assert.IsNotNull(testMetric); 
        }

        [Test]
        public void MetricCollectionFindByStringFullName()
        {
            MetricDefinition testMetricDefinition = GetTestMetricDefinition();
            IMetric lookupMetric = testMetricDefinition.Metrics[0];  //this test we already passed, so now we use it to do the rest of our tests.
            IMetric testMetric = testMetricDefinition.Metrics[lookupMetric.Name];
            Assert.IsNotNull(testMetric);
        }

        [Test]
        public void MetricCollectionFindByStringInstanceName()
        {
            MetricDefinition testMetricDefinition = GetTestMetricDefinition();
            IMetric lookupMetric = testMetricDefinition.Metrics[0];  //this test we already passed, so now we use it to do the rest of our tests.
            IMetric testMetric = testMetricDefinition.Metrics[lookupMetric.InstanceName];
            Assert.IsNotNull(testMetric);
        }

        [Test]
        public void MetricCollectionFindDefault()
        {
            MetricDefinition testMetricDefinition = GetTestMetricDefinition();
            IMetric testMetric = testMetricDefinition.Metrics[null];
            Assert.IsNotNull(testMetric);

            //an empty string should also be treated as null
            testMetric = testMetricDefinition.Metrics[string.Empty];
            Assert.IsNotNull(testMetric);

            //a string variable with nothing but whitespace should be treated as null
            string instanceName = "   ";
            IMetric trimTestMetric = testMetricDefinition.Metrics[instanceName];
            Assert.IsNotNull(trimTestMetric);
            Assert.AreSame(testMetric, trimTestMetric); //we should have gotten the same object despite our nefarious input
        }

        [Test]
        public void MetricCollectionFindByGuidKey()
        {
            MetricDefinition testMetricDefinition = GetTestMetricDefinition();
            IMetric lookupMetric = testMetricDefinition.Metrics[0];  //this test we already passed, so now we use it to do the rest of our tests.
            IMetric testMetric = testMetricDefinition.Metrics[lookupMetric.Id];
            Assert.IsNotNull(testMetric);
        }

        /// <summary>
        /// Verify that we can be lazy and have leading & trailing white space on key elements without it causing a problem.
        /// </summary>
        [Test]
        public void MetricDefinitionStringKeyTrimming()
        {
            MetricDefinition lookupMetricDefinition = GetTestMetricDefinition();

            //Now try to get it using each key element with extra white space.
            IMetricDefinition testMetricDefinition = Log.Metrics[string.Format(CultureInfo.InvariantCulture, "  {0}  ", lookupMetricDefinition.Name)];
            Assert.IsNotNull(testMetricDefinition);

            testMetricDefinition = Log.Metrics[string.Format(CultureInfo.InvariantCulture, "  {0}  ", lookupMetricDefinition.MetricTypeName),
                                               string.Format(CultureInfo.InvariantCulture, "  {0}  ", lookupMetricDefinition.CategoryName),
                                               string.Format(CultureInfo.InvariantCulture, "  {0}  ", lookupMetricDefinition.CounterName)];
            Assert.IsNotNull(testMetricDefinition);
        }

        /// <summary>
        /// Verify that we can be lazy and have leading & trailing white space on key elements without it causing a problem.
        /// </summary>
        [Test]
        public void MetricStringKeyTrimming()
        {
            MetricDefinition testMetricDefinition = GetTestMetricDefinition();
            Metric lookupMetric = EventMetric.AddOrGet((EventMetricDefinition)testMetricDefinition, "MetricStringKeyTrimming");  //this test we already passed, so now we use it to do the rest of our tests.

            //Now try to get it using each key element with extra white space.
            IMetric testMetric = testMetricDefinition.Metrics[string.Format(CultureInfo.InvariantCulture, "  {0}  ", lookupMetric.InstanceName)];
            Assert.IsNotNull(testMetric);
            Assert.AreSame(lookupMetric, testMetric);

            testMetric = testMetricDefinition.Metrics[string.Format(CultureInfo.InvariantCulture, "  {0}  ", lookupMetric.Name)];
            Assert.IsNotNull(testMetric);
            Assert.AreSame(lookupMetric, testMetric);
        }

    }
}
