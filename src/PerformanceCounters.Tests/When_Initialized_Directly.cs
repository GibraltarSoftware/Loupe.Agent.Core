using System.Diagnostics;
using System.Threading;
using Loupe.Agent.PerformanceCounters;
using NUnit.Framework;

namespace Loupe.PerformanceCounters.Tests
{
    [TestFixture]
    public class When_Initialized_Directly
    {
        private PerformanceMonitor _PerformanceMonitor;

        [SetUp]
        public void SetUp()
        {
            _PerformanceMonitor = new PerformanceMonitor();

            var configuration = new PerformanceConfiguration();

            _PerformanceMonitor.Initialize(null, configuration);
        }

        [Test]
        public void Can_Poll_Performance_Counters()
        {
            _PerformanceMonitor.Poll();

            //wait a bit..
            Thread.Sleep(5000);


            _PerformanceMonitor.Poll();
        }


        [Test]
        public void Can_Poll_Performance_Counters_Quickly()
        {
            _PerformanceMonitor.Poll();

            var testRunTime = Stopwatch.StartNew();
            var polls = 0;
            do
            {
                _PerformanceMonitor.Poll();
                polls++;
            } while (testRunTime.Elapsed.TotalSeconds < 20);


            Assert.That(polls > 200, "Unable to poll perf counters in under 100ms on average");
        }
    }
}
