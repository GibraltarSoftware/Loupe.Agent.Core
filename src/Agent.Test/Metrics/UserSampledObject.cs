using Gibraltar.Agent.Metrics;
using Loupe.Metrics;

namespace Loupe.Agent.Test.Metrics
{
    [SampledMetric("UserSampledObject", "Attributes.Unit Test Data")]
    public class UserSampledObject
    {
        private int m_PrimaryValue;
        private int m_SecondaryValue;
        private string m_InstanceName;

        public UserSampledObject()
        {
            m_PrimaryValue = 0;
            m_SecondaryValue = 1;
            m_InstanceName = "Dummy instance";
        }

        public UserSampledObject(int primaryValue)
        {
            m_PrimaryValue = primaryValue;
            m_SecondaryValue = 1;
            m_InstanceName = "Dummy instance";
        }

        public UserSampledObject(int primaryValue, int secondaryValue)
        {
            m_PrimaryValue = primaryValue;
            m_SecondaryValue = secondaryValue;
            m_InstanceName = "Dummy instance";
        }

        public UserSampledObject(string instanceName)
        {
            m_PrimaryValue = 0;
            m_SecondaryValue = 1;
            m_InstanceName = instanceName;
        }

        public UserSampledObject(string instanceName, int primaryValue)
        {
            m_PrimaryValue = primaryValue;
            m_SecondaryValue = 1;
            m_InstanceName = instanceName;
        }

        public UserSampledObject(string instanceName, int primaryValue, int secondaryValue)
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

        [SampledMetricValue("IncrementalCount", SamplingType.IncrementalCount, null,
                       Description="Unit test sampled metric using the incremental count calculation routine")]
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
        public string GetInstanceName()
        {
            return m_InstanceName;
        }
    }
}
