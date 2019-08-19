using Gibraltar.Agent.Metrics;
using Loupe.Metrics;
using NUnit.Framework;

namespace Loupe.Agent.Test.Metrics
{
    [TestFixture]
    public class Examples
    {
        [Test]
        public void RecordSampledMetric()
        {
            SampledMetricExample.RecordCacheMetric(1);
            SampledMetricExample.RecordCacheMetric(2);
            SampledMetricExample.RecordCacheMetric(3);
            SampledMetricExample.RecordCacheMetric(4);
        }

        [Test]
        public void RecordSampledMetricShortestCode()
        {
            SampledMetricExample.RecordCacheMetricShortestCode(1);
            SampledMetricExample.RecordCacheMetricShortestCode(2);
            SampledMetricExample.RecordCacheMetricShortestCode(3);
            SampledMetricExample.RecordCacheMetricShortestCode(4);
        }

        [Test]
        public void RecordSampledMetricByObject()
        {
            SampledMetricExample.RecordCacheMetricByObject(1);
            SampledMetricExample.RecordCacheMetricByObject(2);
            SampledMetricExample.RecordCacheMetricByObject(3);
            SampledMetricExample.RecordCacheMetricByObject(4);
        }

        [Test]
        public void RecordEventMetric()
        {
            EventMetricExample.RecordCacheMetric(1);
            EventMetricExample.RecordCacheMetric(2);
            EventMetricExample.RecordCacheMetric(3);
            EventMetricExample.RecordCacheMetric(4);
        }

        [Test]
        public void RecordEventMetricByObject()
        {
            EventMetricExample.RecordCacheMetricByObject(1);
            EventMetricExample.RecordCacheMetricByObject(2);
            EventMetricExample.RecordCacheMetricByObject(3);
            EventMetricExample.RecordCacheMetricByObject(4);
        }
    }

    /// <summary>
    /// Sampled metric example
    /// </summary>
    public static class SampledMetricExample
    {
        /// <summary>
        /// Snapshot cache metrics
        /// </summary>
        /// <param name="pagesLoaded"></param>
        public static void RecordCacheMetric(int pagesLoaded)
        {
            SampledMetricDefinition pageMetricDefinition;

            //since sampled metrics have only one value per metric, we have to create multiple metrics (one for every value)
            if (SampledMetricDefinition.TryGetValue("GibraltarSample", "Database.Engine", "Cache Pages", out pageMetricDefinition) == false)
            {
                //doesn't exist yet - add it in all of its glory.  This call is MT safe - we get back the object in cache even if registered on another thread.
                pageMetricDefinition = SampledMetricDefinition.Register("GibraltarSample", "Database.Engine", "cachePages", SamplingType.RawCount, "Pages", "Cache Pages", "The number of pages in the cache");
            }

            //now that we know we have the definitions, make sure we've defined the metric instances.
            SampledMetric pageMetric = SampledMetric.Register(pageMetricDefinition, null);

            //now go ahead and write those samples....
            pageMetric.WriteSample(pagesLoaded);

            //Continue for our second metric.
            SampledMetricDefinition sizeMetricDefinition;
            if (SampledMetricDefinition.TryGetValue("GibraltarSample", "Database.Engine", "Cache Size", out sizeMetricDefinition) == false)
            {
                //doesn't exist yet - add it in all of its glory  This call is MT safe - we get back the object in cache even if registered on another thread.
                sizeMetricDefinition = SampledMetricDefinition.Register("GibraltarSample", "Database.Engine", "cacheSize", SamplingType.RawCount, "Bytes", "Cache Size", "The number of bytes used by pages in the cache");
            }

            SampledMetric sizeMetric = SampledMetric.Register(sizeMetricDefinition, null);
            sizeMetric.WriteSample(pagesLoaded * 8196);
        }

        /// <summary>
        /// Snapshot cache metrics using fewest lines of code
        /// </summary>
        /// <param name="pagesLoaded"></param>
        public static void RecordCacheMetricShortestCode(int pagesLoaded)
        {
            //Alternately, it can be done in a single line of code each, although somewhat less readable.  Note the WriteSample call after the Register call.
            SampledMetric.Register("GibraltarSample", "Database.Engine", "cachePages", SamplingType.RawCount, "Pages", "Cache Pages", "The number of pages in the cache", null).WriteSample(pagesLoaded);
            SampledMetric.Register("GibraltarSample", "Database.Engine", "cacheSize", SamplingType.RawCount, "Bytes", "Cache Size", "The number of bytes used by pages in the cache", null).WriteSample(pagesLoaded * 8196);
        }

        /// <summary>
        /// Record a snapshot cache metric using an object
        /// </summary>
        /// <param name="pagesLoaded"></param>
        public static void RecordCacheMetricByObject(int pagesLoaded)
        {
            //by using an object with the appropriate attributes we can do it in one line - even though it writes multiple values.
            SampledMetric.Write(new CacheSampledMetric(pagesLoaded));
        }
    }

    /// <summary>
    /// Log sampled metrics using a single object
    /// </summary>
    [SampledMetric("GibraltarSample", "Database.Engine")]
    public class CacheSampledMetric
    {
        public CacheSampledMetric(int pagesLoaded)
        {
            Pages = pagesLoaded;
            Size = pagesLoaded * 8192; //VistaDB.Engine.Core.IO.DiskPage.PageSize;
        }

        private int m_Pages;

        [SampledMetricValue("pages", SamplingType.RawCount, "Pages", Caption = "Pages in Cache", Description = "Total number of pages in cache")]
        public int Pages { get { return m_Pages; } private set { m_Pages = value; } }

        private int m_Size;

        [SampledMetricValue("size", SamplingType.RawCount, "Bytes", Caption = "Cache Size", Description = "Total number of bytes used by pages in cache")]
        public int Size { get { return m_Size; } private set { m_Size = value; } }
    }

    /// <summary>
    /// Write event metrics using different approaches
    /// </summary>
    public static class EventMetricExample
    {
        /// <summary>
        /// Record an event metric using a programmatic declaration
        /// </summary>
        /// <param name="pagesLoaded"></param>
        public static void RecordCacheMetric(int pagesLoaded)
        {
            EventMetricDefinition cacheMetric;

            //so we can be called multiple times we want to see if the definition already exists.
            if (EventMetricDefinition.TryGetValue("GibraltarSample", "Database.Engine", "Cache", out cacheMetric) == false)
            {
                cacheMetric = new EventMetricDefinition("GibraltarSample", "Database.Engine", "Cache");

                //add the values (that are part of the definition)
                cacheMetric.AddValue("pages", typeof(int), SummaryFunction.Average, "Pages", "Pages in Cache", "Total number of pages in cache");
                cacheMetric.AddValue("size", typeof(int), SummaryFunction.Average, "Bytes", "Cache Size", "Total number of bytes used by pages in cache");

                //and now that we're done, we need to register this definition.  This locks the definition
                //and makes it go live.  Note that it's based by ref because if another thread registered the same metric, we'll get the
                //registered object (whoever one the race), not necessarily the one we've just created to pass in.
                EventMetricDefinition.Register(ref cacheMetric);
            }

            //Now we can get the specific metric we want to record samples under (this is an instance of the definition)
            EventMetric cacheEventMetric = EventMetric.Register(cacheMetric, null);

            //now go ahead and write that sample.
            EventMetricSample newSample = cacheEventMetric.CreateSample();
            newSample.SetValue("pages", pagesLoaded);
            newSample.SetValue("size", pagesLoaded * 8196);
            newSample.Write();
        }

        /// <summary>
        /// Record an event metric using an object
        /// </summary>
        /// <param name="pagesLoaded"></param>
        public static void RecordCacheMetricByObject(int pagesLoaded)
        {
            CacheEventMetric sample = new CacheEventMetric(pagesLoaded);
            EventMetric.Write(sample);
        }
    }

    /// <summary>
    /// Log event metrics using a single object
    /// </summary>
    [EventMetric("GibraltarSample", "Database.Engine", "Cache - Declarative", Caption = "Simple Cache", Description = "Performance metrics for the database engine")]
    public class CacheEventMetric
    {
        public CacheEventMetric(int pagesLoaded)
        {
            Pages = pagesLoaded;
            Size = pagesLoaded * 8192;
        }

        private int m_Pages;

        [EventMetricValue("pages", SummaryFunction.Average, "Pages", Caption = "Pages in Cache", Description = "Total number of pages in cache")]
        public int Pages { get { return m_Pages; } private set { m_Pages = value; } }

        private int m_Size;

        [EventMetricValue("size", SummaryFunction.Average, "Bytes", Caption = "Cache Size", Description = "Total number of bytes used by pages in cache")]
        public int Size { get { return m_Size; } private set { m_Size = value; } }
    }
}
