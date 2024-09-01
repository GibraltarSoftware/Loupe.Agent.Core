using Gibraltar.Data;
using Gibraltar.Monitor;
using Loupe.Agent.PerformanceCounters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Loupe.Core.Test.Data
{
    [TestFixture]
    public class When_Loading_Session_Data
    {
        [Test]
        public void Can_Load_Perf_Counter_Data()
        {
            var file = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Content", "NoisySession.glf");

            using (var stream = File.OpenRead(file))
            using (var glfReader = new GLFReader(stream))
            using (var session = new Session(glfReader))
            {
                var perfCounterMetrics = session.MetricDefinitions.Where(d => d is PerfCounterMetricDefinition).ToList();

                Assert.NotZero(perfCounterMetrics.Count);
            }
        }
    }
}
