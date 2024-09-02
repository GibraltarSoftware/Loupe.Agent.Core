using Gibraltar.Data;
using Gibraltar.Monitor;
using Loupe.Agent.PerformanceCounters;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Loupe.PerformanceCounters.Test
{
    [TestFixture]
    public class When_Loading_Session_Data
    {
        [Test]
        public void Can_Load_Perf_Counter_Data()
        {
            using (var session = LoadSampleSession())
            {
                var perfCounterMetrics = session.MetricDefinitions.Where(d => d is PerfCounterMetricDefinition).ToList();

                Assert.NotZero(perfCounterMetrics.Count);
            }
        }

        [Test]
        public void Can_Calculate_Perf_Counter_Samples()
        {
            using (var session = LoadSampleSession())
            {
                var procTimeCounterDefinition = session.MetricDefinitions.FirstOrDefault(d => d.Name == "PerfCounter~Processor~% Processor Time") as PerfCounterMetricDefinition;
                Assert.IsNotNull(procTimeCounterDefinition);

                var procTimeCounter = procTimeCounterDefinition.Metrics.FirstOrDefault() as PerfCounterMetric;
                Assert.IsNotNull(procTimeCounter);

                var samples = procTimeCounter.CalculateValues(Extensibility.Data.MetricSampleInterval.Second, 5);
                Assert.IsNotNull(samples);
                Assert.AreEqual(13, samples.Count);
            }
        }

        private Session LoadSampleSession()
        {
            var file = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Content", "NoisySession.glf");

            var session = new Session(new GLFReader(File.OpenRead(file)));

            return session;
        }
    }
}
