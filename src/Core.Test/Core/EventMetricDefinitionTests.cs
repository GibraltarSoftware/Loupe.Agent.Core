﻿using System;
using Gibraltar.Monitor;
using Loupe.Extensibility.Data;
using Loupe.Metrics;
using NUnit.Framework;

namespace Loupe.Core.Test.Core
{
    [TestFixture]
    public class EventMetricDefinitionTests
    {
        /// <summary>
        /// Register metric definitions the long hand way to prove the underlying API works.
        /// </summary>
        /// <remarks>This test is coupled with other tests in this batch, so don't go changing the strings willy nilly</remarks>
        [Test]
        public void RegisterEventMetrics()
        {
            //Define an event metric manually (the hard way)
            EventMetricDefinition newEventMetric =
                new EventMetricDefinition("EventMetricTests", "Gibraltar.Monitor.Test", "Manual");

            //we now have a minimal definition, but we probably want to add a few attributes to make it useful
            //NOTE:  This is designed to exactly match UserDataObject for convenience in analyzing results.
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
            newEventMetric = newEventMetric.Register(); // Register it with the collection.

            //Create this instance of that definition
            newEventMetric.Metrics.Add("This Experiment");
        }        
    }
}
