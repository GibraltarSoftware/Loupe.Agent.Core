using System.Globalization;
using System.Threading;
using Gibraltar.Monitor;
using NUnit.Framework;

namespace Loupe.Core.Test.Core
{
    [TestFixture]
    public class CustomSampledMetricTests
    {
        [Test]
        public void GenerateMetricData()
        {
            Log.Trace( "Defining new metric definitions" );
            //go ahead and register a few metrics
            int curMetricDefinitionCount = Log.Metrics.Count;

            CustomSampledMetricDefinition newMetric;
            newMetric = new CustomSampledMetricDefinition(Log.Metrics, "GenerateMetricData", "Unit Test Data", "IncrementalCount",
                                              MetricSampleType.IncrementalCount);
            newMetric.Description = "Unit test sampled metric using the incremental count calculation routine";

            newMetric = new CustomSampledMetricDefinition(Log.Metrics, "GenerateMetricData", "Unit Test Data", "IncrementalFraction",
                                              MetricSampleType.IncrementalFraction);
            newMetric.Description = "Unit test sampled metric using the incremental fraction calculation routine.  Rare, but fun.";

            newMetric = new CustomSampledMetricDefinition(Log.Metrics, "GenerateMetricData", "Unit Test Data", "TotalCount",
                                              MetricSampleType.TotalCount);
            newMetric.Description = "Unit test sampled metric using the Total Count calculation routine.  Very common.";

            newMetric = new CustomSampledMetricDefinition(Log.Metrics, "GenerateMetricData", "Unit Test Data", "TotalFraction",
                                              MetricSampleType.TotalFraction);
            newMetric.Description = "Unit test sampled metric using the Total Fraction calculation routine.  Rare, but rounds us out.";

            newMetric = new CustomSampledMetricDefinition(Log.Metrics, "GenerateMetricData", "Unit Test Data", "RawCount",
                                              MetricSampleType.RawCount);
            newMetric.Description = "Unit test sampled metric using the Raw Count calculation routine, which we will then average to create sample intervals.";


            newMetric = new CustomSampledMetricDefinition(Log.Metrics, "GenerateMetricData", "Unit Test Data", "RawFraction",
                                              MetricSampleType.RawFraction);
            newMetric.Description = "Unit test sampled metric using the Raw Fraction calculation routine.  Fraction types aren't common.";

            //we should have added six new metric definitions
            Assert.AreEqual(curMetricDefinitionCount + 6, Log.Metrics.Count, "The number of registered metric definitions hasn't increased by the right amount, tending to mean that one or more metrics didn't register.");

            //and lets go ahead and create new metrics for each definition
            Log.Trace("Defining new metrics");
            new CustomSampledMetric(Log.Metrics, "GenerateMetricData", "Unit Test Data", "IncrementalCount", null);
            new CustomSampledMetric(Log.Metrics, "GenerateMetricData", "Unit Test Data", "IncrementalFraction", null);
            new CustomSampledMetric(Log.Metrics, "GenerateMetricData", "Unit Test Data", "TotalCount", null);
            new CustomSampledMetric(Log.Metrics, "GenerateMetricData", "Unit Test Data", "TotalFraction", null);
            new CustomSampledMetric(Log.Metrics, "GenerateMetricData", "Unit Test Data", "RawCount", null);
            new CustomSampledMetric(Log.Metrics, "GenerateMetricData", "Unit Test Data", "RawFraction", null);

            //and finally, lets go log some data!
            Log.Trace("And now lets log data");
            CustomSampledMetric incrementalCountMetric = (CustomSampledMetric)
                                                         Log.Metrics["GenerateMetricData", "Unit Test Data", "IncrementalCount"].Metrics[0];
            CustomSampledMetric incrementalFractionMetric = (CustomSampledMetric)
                                                            Log.Metrics["GenerateMetricData", "Unit Test Data", "IncrementalFraction"].Metrics[0];
            CustomSampledMetric deltaCountMetric = (CustomSampledMetric)
                                                   Log.Metrics["GenerateMetricData", "Unit Test Data", "TotalCount"].Metrics[0];
            CustomSampledMetric deltaFractionMetric = (CustomSampledMetric)
                                                      Log.Metrics["GenerateMetricData", "Unit Test Data", "TotalFraction"].Metrics[0];
            CustomSampledMetric rawCountMetric = (CustomSampledMetric)
                                                 Log.Metrics["GenerateMetricData", "Unit Test Data", "RawCount"].Metrics[0];
            CustomSampledMetric rawFractionMetric = (CustomSampledMetric)
                                                    Log.Metrics["GenerateMetricData", "Unit Test Data", "RawFraction"].Metrics[0];

            //lets add 10 values, a few milliseconds apart
            int curSamplePass = 0;
            while(curSamplePass < 10)
            {
                //We're putting in fairly bogus data, but it will produce a consistent output.
                incrementalCountMetric.WriteSample(curSamplePass * 20);
                incrementalFractionMetric.WriteSample(curSamplePass * 20, curSamplePass * 30);
                deltaCountMetric.WriteSample(curSamplePass * 20);
                deltaFractionMetric.WriteSample(curSamplePass * 20, curSamplePass * 30);
                rawCountMetric.WriteSample(curSamplePass);
                rawFractionMetric.WriteSample(curSamplePass / 10.0);

                curSamplePass++;
                Thread.Sleep(100);
            }

            Log.Trace( "Completed logging metric samples." );
        }

        [Test]
        public void ReadMetricData()
        {
            
        }

        [Test]
        public void SimpleMetricUsage()
        {
            Log.Trace("Defining new metric definitions");
            //create one sampled metric definition using the "make a definition for the current log set" override
            CustomSampledMetricDefinition temperatureTracking = new CustomSampledMetricDefinition( "SimpleMetricUsage", "Temperature", "Experiment Temperature", MetricSampleType.RawCount );
            temperatureTracking.Description = "This is an example from iControl where we want to track the temperature of a reaction or some such thing.";

            //create a set of METRICS (definition + metric) using the static add metric capability
            Log.Trace( "defining new metrics" );
            CustomSampledMetric incrementalCountMetric = CustomSampledMetric.AddOrGet("SimpleMetricUsage", "Unit Test Data", "IncrementalCount", MetricSampleType.IncrementalCount, null);
            CustomSampledMetric incrementalFractionMetric = CustomSampledMetric.AddOrGet("SimpleMetricUsage", "Unit Test Data", "IncrementalFraction", MetricSampleType.IncrementalFraction, null);
            CustomSampledMetric deltaCountMetric = CustomSampledMetric.AddOrGet("SimpleMetricUsage", "Unit Test Data", "TotalCount", MetricSampleType.TotalCount, null);
            CustomSampledMetric deltaFractionMetric = CustomSampledMetric.AddOrGet("SimpleMetricUsage", "Unit Test Data", "TotalFraction", MetricSampleType.TotalFraction, null);
            CustomSampledMetric rawCountMetric = CustomSampledMetric.AddOrGet("SimpleMetricUsage", "Unit Test Data", "RawCount", MetricSampleType.RawCount, null);
            CustomSampledMetric rawFractionMetric = CustomSampledMetric.AddOrGet("SimpleMetricUsage", "Unit Test Data", "RawFraction", MetricSampleType.RawFraction, null);            

            //lets add 10 values, a few milliseconds apart
            Log.Trace("And now lets log data");
            int curSamplePass = 0;
            while(curSamplePass < 10)
            {
                //the temperature tracking one is set up so we can write to instances directly from a definition.
                temperatureTracking.WriteSample(string.Format(CultureInfo.CurrentCulture, "Experiment {0}", 1), curSamplePass);
                temperatureTracking.WriteSample(string.Format(CultureInfo.CurrentCulture, "Experiment {0}", 2), curSamplePass);
                temperatureTracking.WriteSample(string.Format(CultureInfo.CurrentCulture, "Experiment {0}", 3), curSamplePass);
                temperatureTracking.WriteSample(string.Format(CultureInfo.CurrentCulture, "Experiment {0}", 4), curSamplePass);
                temperatureTracking.WriteSample(string.Format(CultureInfo.CurrentCulture, "Experiment {0}", 5), curSamplePass);
                temperatureTracking.WriteSample(string.Format(CultureInfo.CurrentCulture, "Experiment {0}", 6), curSamplePass);

                incrementalCountMetric.WriteSample(curSamplePass * 20);
                incrementalFractionMetric.WriteSample(curSamplePass * 20, curSamplePass * 30);
                deltaCountMetric.WriteSample(curSamplePass * 20);
                deltaFractionMetric.WriteSample(curSamplePass * 20, curSamplePass * 30);
                rawCountMetric.WriteSample(curSamplePass);
                rawFractionMetric.WriteSample(curSamplePass / 10.0);

                curSamplePass++;
                Thread.Sleep(100);
            }
            Log.Trace("Completed logging metric samples.");
        }
    }
}
