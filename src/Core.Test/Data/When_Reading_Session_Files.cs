using Gibraltar.Monitor;
using NUnit.Framework;
using System.IO;
using System.Reflection;
using Gibraltar.Data;
using System.Linq;

namespace Loupe.Core.Test.Data
{
    [TestFixture]
    public class When_Reading_Session_Files
    {
        [Test]
        public void Can_Get_Index_Of_Event_Metric_Values()
        {
            using (var session = LoadSampleSession())
            {
                var metricsChecked = 0;
                foreach (var definition in session.MetricDefinitions.Where(d => d as EventMetricDefinition != null).Cast<EventMetricDefinition>())
                {
                    metricsChecked++;

                    foreach (var valueDef in definition.Values)
                    {
                        Assert.That(valueDef.Index, Is.GreaterThanOrEqualTo(0));
                    }
                }

                Assert.That(metricsChecked, Is.GreaterThan(0));
            }
        }

        private Session LoadSampleSession()
        {
            var file = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Content", "SampleSession.glf");

            var session = new Session(new GLFReader(File.OpenRead(file)));

            return session;
        }
    }
}
