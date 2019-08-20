using System;
using System.Diagnostics;
using System.Globalization;
using Loupe.Core.Monitor;
using Loupe.Extensibility.Data;
using Loupe.Logging;
using Loupe.Metrics;
using NUnit.Framework;

namespace Loupe.Core.Test.Core
{
    [TestFixture]
    public class EventMetricTests
    {
        /// <summary>
        /// Ensures each of the metrics we test with are actually defined.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            IMetricDefinition newMetricDefinition;

            //Define an event metric manually (the long way)
            if (Log.Metrics.TryGetValue("EventMetricTests", "Gibraltar.Monitor.Test", "Manual", out newMetricDefinition) == false)
            {
                //Define an event metric manually (the hard way)
                EventMetricDefinition newEventMetric = new EventMetricDefinition("EventMetricTests", "Gibraltar.Monitor.Test", "Manual");

                //we now have a minimal definition, but we probably want to add a few attributes to make it useful
                //NOTE:  This is designed to exactly match UserDataObject for convenience in analzing results.
                EventMetricValueDefinitionCollection valueDefinition = (EventMetricValueDefinitionCollection)newEventMetric.Values;
                valueDefinition.Add("short_average", typeof(short), "Short Average", "Data of type Short").DefaultTrend = SummaryFunction.Average;
                valueDefinition.Add("short_sum", typeof(short), "Short Sum", "Data of type Short").DefaultTrend = SummaryFunction.Sum;
                valueDefinition.Add("short_runningaverage", typeof(short), "Short Running Average", "Data of type Short").DefaultTrend = SummaryFunction.RunningAverage;
                valueDefinition.Add("short_runningsum", typeof(short), "Short Running Sum", "Data of type Short").DefaultTrend = SummaryFunction.RunningSum;
                valueDefinition.Add("ushort_average", typeof(ushort), "UShort Average", "Data of type UShort").DefaultTrend = SummaryFunction.Average;
                valueDefinition.Add("ushort_sum", typeof(ushort), "UShort Sum", "Data of type UShort").DefaultTrend = SummaryFunction.Sum;
                valueDefinition.Add("int_average", typeof(int), "Int Average", "Data of type Int").DefaultTrend = SummaryFunction.Average;
                valueDefinition.Add("int_sum", typeof(int), "Int Sum", "Data of type Int").DefaultTrend = SummaryFunction.Sum;
                valueDefinition.Add("uint_average", typeof(uint), "UInt Average", "Data of type UInt").DefaultTrend = SummaryFunction.Average;
                valueDefinition.Add("uint_sum", typeof(uint), "UInt Sum", "Data of type UInt").DefaultTrend = SummaryFunction.Sum;
                valueDefinition.Add("long_average", typeof(long), "Long Average", "Data of type Long").DefaultTrend = SummaryFunction.Average;
                valueDefinition.Add("long_sum", typeof(long), "Long Sum", "Data of type Long").DefaultTrend = SummaryFunction.Sum;
                valueDefinition.Add("ulong_average", typeof(ulong), "ULong Average", "Data of type ULong").DefaultTrend = SummaryFunction.Average;
                valueDefinition.Add("ulong_sum", typeof(ulong), "ULong Sum", "Data of type ULong").DefaultTrend = SummaryFunction.Sum;
                valueDefinition.Add("decimal_average", typeof(decimal), "Decimal Average", "Data of type Decimal").DefaultTrend = SummaryFunction.Average;
                valueDefinition.Add("decimal_sum", typeof(decimal), "Decimal Sum", "Data of type Decimal").DefaultTrend = SummaryFunction.Sum;
                valueDefinition.Add("double_average", typeof(double), "Double Average", "Data of type Double").DefaultTrend = SummaryFunction.Average;
                valueDefinition.Add("double_sum", typeof(double), "Double Sum", "Data of type Double").DefaultTrend = SummaryFunction.Sum;
                valueDefinition.Add("float_average", typeof(float), "Float Average", "Data of type Float").DefaultTrend = SummaryFunction.Average;
                valueDefinition.Add("float_sum", typeof(float), "Float Sum", "Data of type Float").DefaultTrend = SummaryFunction.Sum;
                valueDefinition.Add("timespan_average", typeof(TimeSpan), "TimeSpan Average", "Data of type TimeSpan").DefaultTrend = SummaryFunction.Average;
                valueDefinition.Add("timespan_sum", typeof(TimeSpan), "TimeSpan Sum", "Data of type TimeSpan").DefaultTrend = SummaryFunction.Sum;
                valueDefinition.Add("timespan_runningaverage", typeof(TimeSpan), "TimeSpan Running Average", "Data of type TimeSpan represented as a running average.").DefaultTrend = SummaryFunction.RunningAverage;
                valueDefinition.Add("timespan_runningsum", typeof(TimeSpan), "TimeSpan Running Sum", "Data of type TimeSpan represented as a running sum.").DefaultTrend = SummaryFunction.RunningSum;
                valueDefinition.Add("string", typeof(string), "String", "Data of type String");
                valueDefinition.Add("system.enum", typeof(UserDataEnumeration), "System.Enum", "Data of type System.Enum");

                newEventMetric.DefaultValue = newEventMetric.Values["int_average"];
                newEventMetric.Register(); // Register it with the collection.
            }

            //And define a metric using reflection (not the same metric as above)
            //UserDataObject myDataObject = new UserDataObject(null);
            //EventMetric.AddOrGet(myDataObject);
        }

        [Test]
        public void RecordEventMetric()
        {
            //Internally we want to make this comparable to the reflection test, just varying the part that use reflection.
            EventMetric thisExperimentMetric = EventMetric.AddOrGet("EventMetricTests", "Gibraltar.Monitor.Test", "Manual", "RecordEventMetric");

            //write out one sample
            EventMetricSample newSample = thisExperimentMetric.CreateSample();
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
            newSample.Write(); //only now does it get written because we had to wait until you populated the metrics
        }

        /*
        [Test]
        [Obsolete("Internal use of event metrics must define programatically rather than by reflection.", true)]
        public void RecordEventMetricReflection()
        {
            UserDataObject myDataObject = new UserDataObject(null);

            //you still have to figure out what metric you want to record
            EventMetric existingObjectMetric = EventMetric.AddOrGet(myDataObject);

            //but now you write it with a single call
            existingObjectMetric.WriteSample(myDataObject);
        }
        */

        [Test]
        public void RecordEventMetricPerformanceTest()
        {
            //Internally we want to make this comparable to the reflection test, just varying the part that use reflection.
            EventMetric thisExperimentMetric =
                EventMetric.AddOrGet("EventMetricTests", "Gibraltar.Monitor.Test", "Manual", "RecordEventMetricPerformanceTest");

            //and we're going to write out a BUNCH of samples
            Trace.TraceInformation("Starting performance test");
            DateTime curTime = DateTime.Now; //for timing how fast we are
            for (int curSample = 0; curSample < 32000; curSample++)
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
            Trace.TraceInformation("Completed performance test in {0} milliseconds for 32,000 samples", duration.TotalMilliseconds);

            Log.Write(LogMessageSeverity.Verbose, LogWriteMode.WaitForCommit, null, "Unit Tests", "Event Metrics performance test flush", null);
        }

        /*
        [Test]
        [Obsolete("Internal use of event metrics must define programatically rather than by reflection.", true)]
        public void RecordEventMetricReflectionPerformanceTest()
        {
            UserDataObject myDataObject = new UserDataObject("Performance Test");

            //warm up the object just to get rid of first hit performance
            EventMetric existingObjectMetric = EventMetric.AddOrGet(myDataObject);
            existingObjectMetric.WriteSample(myDataObject);

            //and we're going to write out a BUNCH of samples
            Trace.TraceInformation("Starting reflection performance test");
            DateTime curTime = DateTime.Now;

            //We have to limit ourselves to 32000 samples to stay within short.
            for (short curSample = 0; curSample < 32000; curSample++)
            {
                //we have a LOT of numbers we need to set to increment this object.
                myDataObject.SetValues(curSample);  //sets all of the numerics

                //and write it out again just for kicks
                existingObjectMetric.WriteSample(myDataObject);
            }
            TimeSpan duration = DateTime.Now - curTime;
            Trace.TraceInformation("Completed reflection performance test in {0} milliseconds for 32,000 samples", duration.TotalMilliseconds);

            Log.Write(LogMessageSeverity.Verbose, LogWriteMode.WaitForCommit, null, "Event Metrics performance test flush", null);
        }

        [Test]
        [Obsolete("Internal use of event metrics must define programatically rather than by reflection.", true)]
        public void RecordEventMetricReflectionDataRangeTest()
        {
            UserDataObject myDataObject = new UserDataObject("Data Range Test");

            //warm up the object just to get rid of first hit performance
            EventMetric existingObjectMetric = EventMetric.AddOrGet(myDataObject);
            existingObjectMetric.WriteSample(myDataObject);

            //and we're going to write out a BUNCH of samples
            Trace.TraceInformation("Starting reflection data range test");
            DateTime curTime = DateTime.Now;

            //We have to limit ourselves to 32000 samples to stay within short.
            for (short curSample = 0; curSample < 32000; curSample++)
            {
                //we have a LOT of numbers we need to set to increment this object.
                myDataObject.SetValues(curSample, 32000);  //sets all of the numerics

                //and write it out again just for kicks
                existingObjectMetric.WriteSample(myDataObject);
            }
            TimeSpan duration = DateTime.Now - curTime;
            Trace.TraceInformation("Completed reflection data range test in {0} milliseconds for 32,000 samples", duration.TotalMilliseconds);

            Log.Write(LogMessageSeverity.Verbose, LogWriteMode.WaitForCommit, null, "Event Metrics performance test flush", null);
        }

        /// <summary>
        /// Create a set of data points distributed over a reasonable range of time to show how events over time display.
        /// </summary>
        [Test]
        [Obsolete("Internal use of event metrics must define programatically rather than by reflection.", true)]
        public void PrettySampleDataOverTimeTest()
        {
            UserDataObject myDataObject = new UserDataObject("Pretty Data");
            EventMetric existingObjectMetric = EventMetric.AddOrGet(myDataObject);

            //do a set of 20 samples with a gap between each.
            for(short curSample = 0; curSample < 20; curSample++)
            {
                myDataObject.SetValues(curSample);
                existingObjectMetric.WriteSample(myDataObject);

                //now sleep for a little to make a nice gap.  This has to be >> 16 ms to show a reasonable gap.
                Thread.Sleep(200);
            }
        }
        */
    }

}
