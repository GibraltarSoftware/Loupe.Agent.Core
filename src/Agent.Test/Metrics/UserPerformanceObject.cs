using System;
using Gibraltar.Agent.Metrics;

namespace Loupe.Agent.Test.Metrics
{
    public enum UserFileOperation
    {
        None,
        Open,
        Close,
        Read,
        Write,
        Append,
        Flush,
    }

    [SampledMetric("PerformanceTestsMetrics", "Performance.SampledMetrics.Attributes")]
    [EventMetric("PerformanceTestsMetrics", "Performance.EventMetrics.Attributes", "UserEvent", Caption="User Event",
        Description="Unit test event metric with typical data.")]
    public class UserPerformanceObject
    {
        private int m_PrimaryValue;
        private int m_SecondaryValue;
        private string m_InstanceName;

        [EventMetricValue("operation", SummaryFunction.Count, null, Caption="Operation",
            Description="The type of file operation being performed.")]
        private UserFileOperation m_Operation; // We can even pull the value directly from a private field.

        private string m_FileName;
        private DateTimeOffset m_StartTime;
        private DateTimeOffset m_EndTime;

        public UserPerformanceObject()
        {
            m_PrimaryValue = 0;
            m_SecondaryValue = 1;
            m_InstanceName = "Dummy instance";
        }

        public UserPerformanceObject(int primaryValue)
        {
            m_PrimaryValue = primaryValue;
            m_SecondaryValue = 1;
            m_InstanceName = "Dummy instance";
        }

        public UserPerformanceObject(int primaryValue, int secondaryValue)
        {
            m_PrimaryValue = primaryValue;
            m_SecondaryValue = secondaryValue;
            m_InstanceName = "Dummy instance";
        }

        public UserPerformanceObject(string instanceName)
        {
            m_PrimaryValue = 0;
            m_SecondaryValue = 1;
            m_InstanceName = instanceName;
        }

        public UserPerformanceObject(string instanceName, int primaryValue)
        {
            m_PrimaryValue = primaryValue;
            m_SecondaryValue = 1;
            m_InstanceName = instanceName;
        }

        public UserPerformanceObject(string instanceName, int primaryValue, int secondaryValue)
        {
            m_PrimaryValue = primaryValue;
            m_SecondaryValue = secondaryValue;
            m_InstanceName = instanceName;
        }

        public void SetValue(int primaryValue)
        {
            m_PrimaryValue = primaryValue;
            m_SecondaryValue = 1;
        }

        public void SetValue(int primaryValue, int secondaryValue)
        {
            m_PrimaryValue = primaryValue;
            m_SecondaryValue = secondaryValue;
        }

        public void SetInstanceName(string instanceName)
        {
            m_InstanceName = instanceName;
        }

        public void SetEventData(string fileName, UserFileOperation operation, DateTimeOffset start, DateTimeOffset end)
        {
            m_FileName = fileName;
            m_Operation = operation;
            m_StartTime = start;
            m_EndTime = end;
        }

        [EventMetricValue("fileName", SummaryFunction.Count, null, Caption="File name",
            Description="The name of the file.")]
        public string FileName { get { return m_FileName; } }

        // TimeSpan is sampled as its Ticks value but ultimately displayed in milliseconds.  We'll call the units "ms".
        // Also, this is the value to graph by default for this metric, and we'll recommend averaging it.
        [EventMetricValue("duration", SummaryFunction.Average, "ms", IsDefaultValue=true, Caption="Duration",
            Description="The duration for this file operation.")]
        public TimeSpan GetDuration()
        {
            return m_EndTime - m_StartTime; // Compute duration from our start and end timestamps.
        }

        [SampledMetricValue("IncrementalCount", SamplingType.IncrementalCount, null,
                       Description = "Unit test sampled metric using the incremental count calculation routine")]
        [SampledMetricValue("IncrementalFraction", SamplingType.IncrementalFraction, null,
                       Description = "Unit test sampled metric using the incremental fraction calculation routine.  Rare, but fun.")]
        [SampledMetricValue("TotalCount", SamplingType.TotalCount, null,
                       Description = "Unit test sampled metric using the Total Count calculation routine.  Very common.")]
        [SampledMetricValue("TotalFraction", SamplingType.TotalFraction, null,
                       Description = "Unit test sampled metric using the Total Fraction calculation routine.  Rare, but rounds us out.")]
        [SampledMetricValue("RawCount", SamplingType.RawCount, null,
                       Description = "Unit test sampled metric using the Raw Count calculation routine, which we will then average to create sample intervals.")]
        [SampledMetricValue("RawFraction", SamplingType.RawFraction, null,
                       Description = "Unit test sampled metric using the Raw Fraction calculation routine.  Fraction types aren't common.")]
        public int PrimaryValue { get { return m_PrimaryValue; } set { m_PrimaryValue = value; } }

        [SampledMetricDivisor("IncrementalFraction")]
        [SampledMetricDivisor("TotalFraction")]
        [SampledMetricDivisor("RawFraction")]
        public int SecondaryValue { get { return m_SecondaryValue; } set { m_SecondaryValue = value; } }

        [SampledMetricInstanceName]
        [EventMetricInstanceName]
        public string GetInstanceName()
        {
            return m_InstanceName;
        }
    }
}
