using Gibraltar.Agent.Metrics;
using Loupe.Metrics;

namespace Loupe.Agent.Test.Metrics
{
    [SampledMetric("SimpleMetricUsage", "Temperature")]
    public class UserInstancedObject
    {
        private static int m_BaseTemperature = 10;
        private readonly int m_InstanceNumber;

        public UserInstancedObject(int instanceNumber)
        {
            m_InstanceNumber = instanceNumber;
        }

        public static void SetTemperature(int baseTemperature)
        {
            m_BaseTemperature = baseTemperature;
        }

        [SampledMetricInstanceName]
        public string GetMetricInstanceName()
        {
            return string.Format("Experiment {0}", m_InstanceNumber);
        }

        [SampledMetricValue("Experiment Temperature", SamplingType.RawCount, null,
                            Description="This tracks the temperature of the various experiments.")]
        public int Temperature { get { return m_BaseTemperature + m_InstanceNumber; } }
    }
}
