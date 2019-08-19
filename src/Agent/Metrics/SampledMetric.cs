using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Loupe.Metrics;

namespace Gibraltar.Agent.Metrics
{
    /// <summary>
    /// A single instance of a metric that is recorded by sampling its value on a
    /// periodic basis.
    /// </summary>
    /// <remarks>
    /// 	<para>Sampled Metrics are designed to record a single, summarized value on a
    ///     periodic basis. Whenever a sample is recorded, the value is presumed to remain
    ///     constant until the next sample is recorded. This works best when the data for each
    ///     individual event is either unavailable or would be to inefficient to collect. To do
    ///     this, the application has to do some degree of calculation or aggregation on its
    ///     own and the useful trends have to be determined during development.</para>
    /// 	<para>Windows Performance Counters are collected as Sampled metrics. Processor
    ///     Utilization represents a good example of a sampled metric - It would be infeasible
    ///     to record each time the processor was used for a task, and the underlying data
    ///     isn't available.</para>
    /// 	<para>For more information on how to take advantage of Sampled Metrics, see
    ///     <a href="Metrics_SampledMetricDesign.html">Developer's Reference - Metrics -
    ///     Designing Sampled Metrics</a>.</para>
    /// 	<para><strong>Event Metrics</strong></para>
    /// 	<para>
    ///         An alternative to Sampled Metrics are Event Metrics. These offer more analysis
    ///         options and can be much easier to record, particularly in multithreaded or
    ///         stateless scenarios. For more information on the difference between Sampled and
    ///         Event Metrics, see <a href="Metrics_SampledEventMetrics.html">Developer's
    ///         Reference - Metrics - Sampled and Event Metrics</a>. For more information on
    ///         Event Metrics, see <see cref="EventMetric">EventMetric Class</see>.
    ///     </para>
    /// 	<para><strong>Viewing Metrics</strong></para>
    /// 	<para>Metrics are visible in the <a href="Viewer_Session_Introduction.html">Session
    ///     Viewer</a> of <a href="Viewer_Introduction.html">Loupe Desktop</a>. Metrics
    ///     are not displayed in the Loupe Live Viewer.</para>
    /// </remarks>
    /// <seealso cref="!:Metrics_SampledMetricDesign.html" cat="Developer's Reference">Metrics - Designing Sampled Metrics</seealso>
    /// <seealso cref="!:Metrics_SampledEventMetrics.html" cat="Developer's Reference">Metrics - Sampled and Event Metrics</seealso>
    /// <example>
    /// 	<code lang="CS" description="In this example we define a sampled metric entirely in code with each stage broken out. Note that the API for sampled metrics is designed to be multithread safe.">
    /// 		<![CDATA[
    /// SampledMetricDefinition pageMetricDefinition;
    ///  
    /// //since sampled metrics have only one value per metric, we have to create multiple metrics (one for every value)
    /// if (SampledMetricDefinition.TryGetValue("GibraltarSample", "Database.Engine", "Cache Pages", out pageMetricDefinition) == false)
    /// {
    ///     //doesn't exist yet - add it in all of its glory.  This call is MT safe - we get back the object in cache even if registered on another thread.
    ///     pageMetricDefinition = SampledMetricDefinition.Register("GibraltarSample", "Database.Engine", "cachePages", 
    ///             SamplingType.RawCount, "Pages", "Cache Pages", "The number of pages in the cache");
    /// }
    ///  
    /// //now that we know we have the definitions, make sure we've defined the metric instances.
    /// SampledMetric pageMetric = SampledMetric.Register(pageMetricDefinition, null);
    ///  
    /// //now go ahead and write those samples....
    /// pageMetric.WriteSample(pagesLoaded);]]>
    /// 	</code>
    /// 	<code lang="CS" description="Alternately, we can do it with very little code by taking advantage of the return values from each step in line. This method is functionally equivalent, and relies on the behavior of the Register call which will ignore duplicate registrations and return the already-registered instance.">
    /// 		<![CDATA[
    /// //Alternately, it can be done in a single line of code each, although somewhat less readable.  
    /// //Note the WriteSample call after the Register call.
    /// SampledMetric.Register("GibraltarSample", "Database.Engine", "cachePages", SamplingType.RawCount, 
    ///     "Pages", "Cache Pages", "The number of pages in the cache", null).WriteSample(pagesLoaded);]]>
    /// 	</code>
    /// 	<code lang="CS" title="Declarative Metrics" description="You can also declare metrics by decorating a data object and then sampling that object. This can create very compact, readable metric code and integrates well.">
    /// 		<![CDATA[
    /// //by using an object with the appropriate attributes we can do it in 
    /// //one line - even though it writes multiple values.
    /// SampledMetric.Write(new CacheSampledMetric(pagesLoaded));
    ///  
    /// //To use the above line, you have to define the following class:
    ///  
    /// /// <summary>
    /// /// Log sampled metrics using a single object
    /// /// </summary>
    /// [SampledMetric("GibraltarSample", "Database.Engine")]
    /// public class CacheSampledMetric
    /// {
    ///     public CacheSampledMetric(int pagesLoaded)
    ///     {
    ///         Pages = pagesLoaded;
    ///         Size = pagesLoaded * 8192;
    ///     }
    ///  
    ///     [SampledMetricValue("pages", SamplingType.RawCount, "Pages", Caption = "Pages in Cache", 
    ///         Description = "Total number of pages in cache")]
    ///     public int Pages { get; private set; }
    ///  
    ///     [SampledMetricValue("size", SamplingType.RawCount, "Bytes", Caption = "Cache Size", 
    ///         Description = "Total number of bytes used by pages in cache")]
    ///     public int Size { get; private set; }
    /// }]]>
    /// 	</code>
    /// </example>
    public sealed class SampledMetric : IComparable<SampledMetric>, IEquatable<SampledMetric>
    {
        private readonly Monitor.CustomSampledMetric m_WrappedMetric;
        private readonly SampledMetricDefinition m_MetricDefinition;

        /*
        /// <summary>Creates a new custom sampled metric object from the metric definition looked up with the provided key information.</summary>
        /// <remarks>The metric definition must already exist or an exception will be raised.</remarks>
        /// <param name="definitions">The definitions dictionary this definition is a part of</param>
        /// <param name="metricsSystem">The metrics capture system label.</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection.</param>
        internal SampledMetric(MetricDefinitionCollection definitions, string metricsSystem, string categoryName, string counterName, string instanceName)
            : this((SampledMetricDefinition)definitions[metricsSystem, categoryName, counterName], instanceName)
        {
        }
        */

        /// <summary>
        /// Create a new custom sampled metric object from the provided metric definition
        /// </summary>
        /// <remarks>The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The metric definition for the metric instance</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection.</param>
        internal SampledMetric(SampledMetricDefinition definition, string instanceName)
            : this(definition, new Monitor.CustomSampledMetric(definition.WrappedDefinition, instanceName))
        {
            definition.Metrics.Internalize(this); // ToDo: Is this needed?
        }

        /// <summary>
        /// Create a new API custom sampled metric object from the provided API custom sampled metric definition and internal custom sampled metric.
        /// </summary>
        /// <remarks>The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The API custom sampled metric definition for the metric instance.</param>
        /// <param name="metric">The internal custom sampled metric to wrap.</param>
        internal SampledMetric(SampledMetricDefinition definition, Monitor.CustomSampledMetric metric)
        {
            m_MetricDefinition = definition;
            m_WrappedMetric = metric;
        }

        #region Public Properties and Methods

        /// <summary>Registers all sampled metric definitions defined by attributes on the provided object or Type, and registers
        /// metric instances where SampledMetricInstanceName attribute is also found or a non-null fall-back is specified.</summary>
        /// <remarks><para>This call ensures that the time-consuming reflection scan of all members looking for attributes
        /// across the entire inheritance of an object instance or Type has been done (e.g. outside of a critical path)
        /// so that the first call to Write(userDataObject) will not have to do that work within a critical path.  Results
        /// are cached internally, so redundant calls to this method will not repeat the scan for types already scanned
        /// (including as part of a different top-level type).</para>
        /// <para>If a live object is given (not just a Type) then the member(s) bound as [SampledMetricInstanceName] will be
        /// queried and used to also register a sampled metric instance with the returned name, to save that step as well,
        /// although this step is much quicker.  If a Type is given instead of a live object, it can not be queried for
        /// instance name(s) and will only register the sampled metric definitions.  Metric instances will still be created
        /// as needed when sampling a userDataObject, automatically.</para>
        /// <para>If fallbackInstanceName is null, only instances which specify an instance name in the live object will
        /// be registered (and returned).  With a valid string for fall-back instance name (including string.Empty for the
        /// "default instance"), a sampled metric will be registered and returned (barring errors) for each definition
        /// found.  The instance indicated by the binding in the object will always be used by preference over the
        /// fall-back instance name parameter, even if the instance name member returns a null.</para></remarks>
        /// <param name="userDataObject">An object or Type defining sampled metrics via attributes on itself or on its base types or interfaces.</param>
        /// <param name="fallbackInstanceName">The instance name to fall back on if a definition does not specify an instance name binding (may be null).</param>
        /// <returns>And array of all sampled metric instances found or created (one per definition) based on the instance
        /// name binding and optional fallbackInstanceName.</returns>
        /// <exception cref="ArgumentNullException">The provided userDataObject was null.</exception>
        internal static SampledMetric[] RegisterAll(object userDataObject, string fallbackInstanceName)
        {
            //we need a live object, not a null object or we'll fail
            if (userDataObject == null)
            {
                throw new ArgumentNullException(nameof(userDataObject));
            }

            // Register all of the event metric definitions it contains, object or Type:
            SampledMetricDefinition[] definitions = SampledMetricDefinition.RegisterAll(userDataObject);

            List<SampledMetric> metricsList = new List<SampledMetric>();

            if ((userDataObject is Type) == false)
            {
                // They gave us a live object, not just a Type, so see if there are metric instances we can register.

                // We'll cache the instance name for efficiency when multiple definitions in a row have the same BoundType.
                Type boundType = null;
                string instanceName = null;

                foreach (SampledMetricDefinition definition in definitions)
                {
                    if (definition.IsBound && definition.NameBound)
                    {
                        // We are bound, so BoundType won't be null.  Initial null value won't match, so we'll look it up.
                        if (definition.BoundType != boundType)
                        {
                            // New bound type, we need to look up the instance name bound for sampled metrics on that Type.
                            instanceName = definition.InvokeInstanceNameBinding(userDataObject) ?? fallbackInstanceName;
                            // A null return means it didn't have an instance name binding or couldn't read it, so we'll
                            // use the specified fallbackInstanceName instead.  If the instance name member returned null,
                            // this call will actually give us string.Empty, so we won't override it.  If this call and
                            // fallbackInstanceName are both null, we won't register the instance.
                            boundType = definition.BoundType;
                        }

                        if (instanceName != null) // null means it didn't find one, so we won't register an instance.
                        {
                            // In case there's an error in registration of one metric, we don't want to stop the rest.
                            try
                            {
                                // An empty string (meaning the found value was null or empty) will be registered (same as null).
                                metricsList.Add(Register(definition, instanceName));
                            }
                            // ReSharper disable EmptyGeneralCatchClause
                            catch
                            // ReSharper restore EmptyGeneralCatchClause
                            {
                            }
                        }
                    }
                }
            }

            return metricsList.ToArray();
        }

        /// <summary>
        /// Pre-registers all sampled metric definitions defined by attributes on the
        /// provided object or Type, and registers metric instances where SampledMetricInstanceName
        /// attribute is also found.
        /// </summary>
        /// <remarks>
        /// 	<para>
        ///         This call ensures that the reflection scan of all members looking for
        ///         attributes across the entire inheritance of an object instance or Type has been
        ///         done (e.g. outside of a critical path) so that the first call to <see cref="Write(object)">Write</see> can be as fast as possible. Results are cached
        ///         internally, so redundant calls to this method will not repeat the scan for
        ///         types already scanned (including as part of a different top-level type).
        ///     </para>
        /// 	<para>
        ///         If a live object is given (not just a Type) then the member(s) decorated with a
        ///         <see cref="SampledMetricInstanceNameAttribute">SampledMetricInstanceNameAttribute
        ///         Class</see> will be queried and used to also register a sampled metric instance
        ///         with the returned name.
        ///     </para>
        /// 	<para>If a Type is given instead of a live object, it can't be queried for instance
        ///     name(s) and will only register the sampled metric definitions. Metric instances
        ///     will still be automatically created as needed when writing a
        ///     metricDataObject.</para>
        /// </remarks>
        /// <example>
        ///     For examples, see the <see cref="SampledMetric">Sampled Metric</see> class
        ///     overview.
        /// </example>
        /// <param name="metricData">An object or Type defining sampled metrics via attributes on itself or on its base types or interfaces.</param>
        /// <exception cref="ArgumentNullException">The provided metricData object was null.</exception>
        /// <exception cref="ArgumentException">The specified Type does not have a SampledMetric attribute, so it can't be used to define sampled metrics.<br />
        /// - or -<br />
        /// The specified Type does not have a usable SampledMetric attribute, so it can't be used to define sampled metrics.<br />
        /// - or -<br />
        /// The specified Type's SampledMetric attribute has an empty metric namespace which is not allowed, so no metrics can be defined under it.<br />
        /// - or -<br />
        /// The specified Type's SampledMetric attribute has an empty metric category name which is not allowed, so no metrics can be defined under it.</exception>
        public static void Register(object metricData)
        {
            RegisterAll(metricData, null); // Register all definitions, but with no fall-back for unspecified instances.
        }

        /// <summary>Creates a new metric instance from the provided definition information, or returns any existing instance if found.</summary>
        /// <remarks>
        /// 	<para>This call is designed to be safe in multithreaded environments. If two
        ///     threads attempt to register the same metric at the same time, the first will
        ///     register the metric and the second (and all subsequent calls to Register with the
        ///     same three part key) will return the same object.</para>
        /// 	<para>If the Metric Definition doesn't exist, it will be created. If the Sampled
        ///     Metric doesn't exist, it will be created.</para>
        /// 	<para>If a metric definition does exist with the same 3-part Key but is not a
        ///     sampled metric an exception will be thrown. This is one of the only times that an
        ///     exception can be thrown by the Loupe Agent.</para>
        /// </remarks>
        /// <example>
        ///     For examples, see the <see cref="SampledMetric">Sampled Metric</see> class
        ///     overview.
        /// </example>
        /// <param name="metricsSystem">The metrics capture system label.</param>
        /// <param name="categoryName">The name of the category with which this metric is associated.</param>
        /// <param name="counterName">The name of the metric definition within the category.</param>
        /// <param name="samplingType">The sampling type of this sampled metric counter.</param>
        /// <param name="unitCaption">A displayable caption for the units this metric samples, or null for unit-less values.</param>
        /// <param name="metricCaption">A displayable caption for this sampled metric counter.</param>
        /// <param name="description">An extended end-user description of this sampled metric counter.</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection (may be null).</param>
        public static SampledMetric Register(string metricsSystem, string categoryName, string counterName,
                                             SamplingType samplingType, string unitCaption, string metricCaption,
                                             string description, string instanceName)
        {
            SampledMetricDefinition metricDefinition = SampledMetricDefinition.Register(metricsSystem, categoryName, counterName, samplingType, unitCaption, metricCaption, description);

            // Then just forward into our call that requires the definition to be specified
            return Register(metricDefinition, instanceName);
        }

        /// <summary>
        /// Creates a new metric instance from the provided definition information, or
        /// returns any existing instance if found.
        /// </summary>
        /// <remarks>
        /// 	<para>If the Sampled Metric doesn't exist, it will be created.</para>
        /// 	<para>This call is designed to be safe in multithreaded environments. If two
        ///     threads attempt to register the same metric at the same time, the first will
        ///     register the metric and the second (and all subsequent calls to Register with the
        ///     same three part key) will return the same object.</para>
        /// </remarks>
        /// <returns>The event metric object for the specified event metric instance.</returns>
        /// <example>
        ///     For examples, see the <see cref="SampledMetric">Sampled Metric</see> class
        ///     overview.
        /// </example>
        /// <param name="definition">The metric definition for the metric instance.</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection (may be null).</param>
        public static SampledMetric Register(SampledMetricDefinition definition, string instanceName)
        {
            if (definition == null)
            {
                // Uh-oh. AddOrGet() gave us a non-CustomSampledMetricDefinition?
                return null;
            }

            SampledMetric metric;
            lock (definition.Metrics.Lock)
            {
                if (definition.Metrics.TryGetValue(instanceName, out metric) == false)
                {
                    metric = definition.Metrics.Add(instanceName);
                }
            }
            return metric;
        }

        /// <summary>
        /// Write a metric sample with the provided data immediately, for non-fraction sampling types.
        /// </summary>
        /// <remarks>Sampled metrics using any fraction sampling type should instead use an overload providing both values.</remarks>
        /// <example>
        ///     For examples, see the <see cref="SampledMetric">Sampled Metric</see> class
        ///     overview.
        /// </example>
        /// <param name="rawValue">The raw data value.</param>
        public void WriteSample(double rawValue)
        {
            //Create a new custom sampled metric and write it out to the log
            m_WrappedMetric.CreateSample(rawValue).Write();
        }

        /// <summary>
        /// Write a metric sample with the provided data immediately, for non-fraction sampling types.
        /// </summary>
        /// <remarks>Sampled metrics using any fraction sampling type should instead use an overload providing both values.</remarks>
        /// <example>
        ///     For examples, see the <see cref="SampledMetric">Sampled Metric</see> class
        ///     overview.
        /// </example>
        /// <param name="rawValue">The raw data value.</param>
        /// <param name="rawTimestamp">The exact date and time the raw value was determined.</param>
        public void WriteSample(double rawValue, DateTimeOffset rawTimestamp)
        {
            //Create a new custom sampled metric and write it out to the log
            m_WrappedMetric.CreateSample(rawValue, rawTimestamp).Write();
        }

        /// <summary>
        /// Write a metric sample with the provided data immediately, for fraction sampling types.
        /// </summary>
        /// <remarks>Sampled metrics using a non-fraction sampling type should instead use an overload taking a single data values.</remarks>
        /// <example>
        ///     For examples, see the <see cref="SampledMetric">Sampled Metric</see> class
        ///     overview.
        /// </example>
        /// <param name="rawValue">The raw data value.</param>
        /// <param name="baseValue">The divisor entry of this sample.</param>
        public void WriteSample(double rawValue, double baseValue)
        {
            //Create a new custom sampled metric and write it out to the log
            m_WrappedMetric.CreateSample(rawValue, baseValue).Write();
        }

        /// <summary>
        /// Write a metric sample with the provided data immediately, for fraction sampling types.
        /// </summary>
        /// <remarks>Sampled metrics using a non-fraction sampling type should instead use an overload taking a single data values.</remarks>
        /// <example>
        ///     For examples, see the <see cref="SampledMetric">Sampled Metric</see> class
        ///     overview.
        /// </example>
        /// <param name="rawValue">The raw data value.</param>
        /// <param name="baseValue">The divisor entry of this sample.</param>
        /// <param name="rawTimestamp">The exact date and time the raw value was determined.</param>
        public void WriteSample(double rawValue, double baseValue, DateTimeOffset rawTimestamp)
        {
            //Create a new custom sampled metric and write it out to the log
            m_WrappedMetric.CreateSample(rawValue, baseValue, rawTimestamp).Write();
        }

        /// <summary>
        /// Write a sampled metric sample for this sampled metric instance using the provided data object.
        /// </summary>
        /// <remarks>The provided user data object must be assignable to the bound type which defined this sampled metric
        /// via attributes.</remarks>
        /// <example>
        ///     For examples, see the <see cref="SampledMetric">Sampled Metric</see> class
        ///     overview.
        /// </example>
        /// <param name="metricData">The object to retrieve metric values from.</param>
        /// <exception cref="ArgumentNullException">The provided metricData object was null.</exception>
        /// <exception cref="ArgumentException">This sampled metric's definition is not bound to sample automatically from a user data object.  WriteSample(...) must be given the data values directly.<br />-or-<br />
        /// The provided user data object type ({0}) is not assignable to this sampled metric's bound type ({1}) and can not be sampled automatically for this metric instance."></exception>
        public void WriteSample(object metricData)
        {
            if (metricData == null)
            {
                throw new ArgumentNullException(nameof(metricData));
            }

            if (Definition.IsBound == false)
            {
                throw new ArgumentException("This sampled metric's definition is not bound to sample automatically from a user data object.  WriteSample(...) must be given the data values directly.");
            }

            Type userDataType = metricData.GetType();
            if (Definition.BoundType.IsAssignableFrom(userDataType) == false)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The provided user data object type ({0}) is not assignable to this sampled metric's bound type ({1}) and can not be sampled automatically for this metric instance.",
                                                          userDataType, Definition.BoundType));
            }

            try
            {
                // Get the numerator value...
                NameValuePair<MemberTypes> dataBinding = Definition.DataBinding;
                BindingFlags dataBindingFlags = m_MetricDefinition.GetBindingFlags(dataBinding);

                // This should throw an exception if dataBinding isn't valid, so we'll bail the whole thing.
                object rawNumerator = userDataType.InvokeMember(dataBinding.Name, dataBindingFlags, null, metricData, null, CultureInfo.InvariantCulture);
                double numerator = Convert.ToDouble(rawNumerator, CultureInfo.CurrentCulture);

                if (SampledMetricDefinition.RequiresDivisor(SamplingType))
                {
                    NameValuePair<MemberTypes> divisorBinding = Definition.DivisorBinding;
                    BindingFlags divisorBindingFlags = m_MetricDefinition.GetBindingFlags(divisorBinding);

                    // This should throw an exception if divisorBinding isn't valid, so we'll bail the whole thing.
                    object rawDivisor = userDataType.InvokeMember(divisorBinding.Name, divisorBindingFlags, null, metricData, null, CultureInfo.InvariantCulture);
                    double divisor = Convert.ToDouble(rawDivisor, CultureInfo.CurrentCulture);

                    WriteSample(numerator, divisor); // Write the pair of values.
                }
                else
                {
                    WriteSample(numerator); // Write the single data value.
                }
            }
            catch
            {
#if DEBUG
                if (Debugger.IsAttached)
                    Debugger.Break();
#endif

                // We can't write this sample if we got an error reading the data.
            }
        }

        /// <summary>
        /// Write sampled metric samples for all sampled metrics defined on the provided data object by attributes.
        /// </summary>
        /// <remarks>The provided user data object must be assignable to the bound type which defined this sampled metric
        /// via attributes.</remarks>
        /// <example>
        ///     For examples, see the <see cref="SampledMetric">Sampled Metric</see> class
        ///     overview.
        /// </example>
        /// <param name="metricData">The object to retrieve both metric values and definition from</param>
        /// <param name="fallbackInstanceName">The instance name to fall back on if a definition does not specify an instance name binding (may be null).</param>
        /// <exception cref="ArgumentNullException">The provided metricData object was null.</exception>
        /// <exception cref="ArgumentException">The specified Type does not have a SampledMetric attribute, so it can't be used to define sampled metrics.<br />
        /// - or -<br />
        /// The specified Type does not have a usable SampledMetric attribute, so it can't be used to define sampled metrics.<br />
        /// - or -<br />
        /// The specified Type's SampledMetric attribute has an empty metric namespace which is not allowed, so no metrics can be defined under it.<br />
        /// - or -<br />
        /// The specified Type's SampledMetric attribute has an empty metric category name which is not allowed, so no metrics can be defined under it.</exception>
        public static void Write(object metricData, string fallbackInstanceName)
        {
            // We have to force a null fall-back instance name into a valid string to force all metrics to be sampled.
            SampledMetric[] allMetrics = RegisterAll(metricData, fallbackInstanceName ?? string.Empty);

            foreach (SampledMetric metric in allMetrics)
            {
                try
                {
                    metric.WriteSample(metricData);
                }
                // ReSharper disable EmptyGeneralCatchClause
                catch
                // ReSharper restore EmptyGeneralCatchClause
                {
                }
            }

            return;
        }

        /// <summary>
        /// Write sampled metric samples for all sampled metrics defined on the provided data object by attributes.
        /// </summary>
        /// <remarks>The provided user data object must be assignable to the bound type which defined this sampled metric
        /// via attributes.</remarks>
        /// <example>
        ///     For examples, see the <see cref="SampledMetric">Sampled Metric</see> class
        ///     overview.
        /// </example>
        /// <param name="metricData">The object to retrieve both metric values and definition from</param>
        public static void Write(object metricData)
        {
            // The real logic is in SampledMetricDefinition.
            Write(metricData, string.Empty);
        }


        /// <summary>
        /// Compare this sampled metric to another sampled metric.
        /// </summary>
        /// <param name="other">The sampled metric to compare this sampled metric to.</param>
        /// <returns>A value which is less than, equal to, or greater than zero to represent the comparison result.</returns>
        public int CompareTo(SampledMetric other)
        {
            //we let our base object do the compare, we're really just casting things
            return WrappedMetric.CompareTo(other.WrappedMetric);
        }

        /// <summary>
        /// Determines if the provided sampled metric is identical to this sampled metric.
        /// </summary>
        /// <param name="other">The sampled metric to compare this sampled metric to.</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(SampledMetric other)
        {
            if (other == null) return false;

            //We're really just a type cast, refer to our base object
            return WrappedMetric.Equals(other.WrappedMetric);
        }

        /// <summary>
        /// The definition of this sampled metric.
        /// </summary>
        public SampledMetricDefinition Definition { get { return m_MetricDefinition; } }

        /// <summary>
        /// The unique Id of this sampled metric instance.  This can reliably be used as a key to refer to this item, within the same session which created it.
        /// </summary>
        /// <remarks>
        ///     The Id is limited to a specific session, and thus identifies a consistent unchanged
        ///     definition. The Id can <b>not</b> be used to identify a definition across different
        ///     sessions, which could have different actual definitions due to changing user code.
        ///     See the <see cref="Key">Key</see> property to identify a metric definition across
        ///     different sessions.
        /// </remarks>
        public Guid Id { get { return m_WrappedMetric.Id; } }

        /// <summary>
        /// The four-part key of the metric instance being captured, as a single string.  
        /// </summary>
        /// <remarks>The Key is the combination of metrics capture system label, category name, and counter name of
        /// the metric definition, along with the instance name, to uniquely identify a specific metric instance of a
        /// specific metric definition.  It can also identify the same metric instance across different sessions.</remarks>
        public string Key { get { return m_WrappedMetric.Name; } }

        /// <summary>
        /// A short caption of what the metric tracks, suitable for end-user display.
        /// </summary>
        public string Caption
        {
            get { return m_WrappedMetric.Caption; }
        }

        /// <summary>
        /// A description of what is tracked by this metric, suitable for end-user display.
        /// </summary>
        public string Description
        {
            get { return m_WrappedMetric.Description; }
        }

        /// <summary>
        /// The metrics capture system label of this metric definition.
        /// </summary>
        public string MetricsSystem
        {
            get { return m_MetricDefinition.MetricsSystem; }
        }

        /// <summary>
        /// The category of this metric for display purposes. Displayed as a dot (.)
        /// delimited hierarchical display.
        /// </summary>
        /// <remarks>
        /// You can create arbitrarily deep categorization by using periods (.) to separate
        /// category levels. For example, the category "Database.Query.Search" will be parsed to
        /// create a three-level category {Database, Query, Search}. You can have spaces in the
        /// category name.
        /// </remarks>
        public string CategoryName
        {
            get { return m_MetricDefinition.CategoryName; }
        }

        /// <summary>
        /// The display name of this metric (unique within the category name).
        /// </summary>
        public string CounterName
        {
            get { return m_MetricDefinition.CounterName; }
        }

        /// <summary>
        /// The intended method of interpreting the sampled counter value.
        /// </summary>
        /// <remarks>
        /// 	<para>Depending on how your application can conveniently aggregate data, select the
        ///     matching sampling type. For example, consider a metric designed to record disk
        ///     utilization in bytes / second. This can be done by:</para>
        /// 	<list type="number">
        /// 		<item>
        ///             Recording with each sample the total number of bytes written from the start
        ///             of the process to the current point. This would use the Total Count
        ///             <see cref="SamplingType">Sampling Type</see>.
        ///         </item>
        /// 		<item>
        ///             Recording with each sample the number of bytes written since the last
        ///             sample. This would use the IncrementalCount <see cref="SamplingType">Sampling Type</see>.
        ///         </item>
        /// 		<item>
        ///             Recording with each sample the bytes per second since the last sample. This
        ///             would use the RawCount <see cref="SamplingType">Sampling Type</see>.
        ///         </item>
        /// 	</list>
        /// 	<para><strong>Fraction Sampling Formats</strong></para>
        /// 	<para>When you want to record a metric that represents a percentage, such as
        ///     percent utilization, it's often easiest to record the individual metric samples
        ///     with both parts of the fraction used to derive the percentage. For example,
        ///     consider a metric designed to record percent disk utilization (as a percentage of
        ///     working time). This can be done by:</para>
        /// 	<list type="number">
        /// 		<item>
        ///             Recording with each sample the total number of ticks spent writing to disk
        ///             as the value and the total number of ticks spent servicing requests as the
        ///             base value. This would use the TotalFraction <see cref="SamplingType">Sampling Type</see>.
        ///         </item>
        /// 		<item>
        ///             Recording with each sample the number of ticks spent writing to disk since
        ///             the last sample as the value and the number of ticks spent servicing client
        ///             requests since the last sample as the base value. This would use the
        ///             IncrementalFraction <see cref="SamplingType">Sampling Type</see>.
        ///         </item>
        /// 		<item>
        ///             Recording with each sample the number of ticks spent writing per second as
        ///             the value and the number of ticks spent servicing client requests per
        ///             second as the base value. This would use the RawFraction <see cref="SamplingType">Sampling Type</see>.
        ///         </item>
        /// 	</list>
        /// 	<para>The advantage of the fractional sampling types over simply doing the division
        ///     yourself is primarily the additional safety aspects built into Loupe (such as
        ///     division by zero protection) and automatic, accurate extrapolation to different
        ///     sampling intervals (such as when samples are recorded once per second but you want
        ///     to view them on a longer interval)</para>
        /// </remarks>
        /// <seealso cref="SamplingType">SamplingType Enumeration</seealso>
        public SamplingType SamplingType
        {
            get { return m_MetricDefinition.SamplingType; }
        }

        /// <summary>
        /// The display caption for the units this metric's values represent, or null for unit-less values.
        /// </summary>
        /// <remarks>
        /// 	<para>Unit caption is used in the Analyst during charting and graphing to allow
        ///     metrics that share the same units to be displayed on the same axis. Comparison is
        ///     case insensitive, but otherwise done as a normal string compare.</para>
        /// 	<para>Normally unit captions do not include aggregation text, such as Average, Min
        ///     or Max to support the best axis grouping.</para>
        /// </remarks>
        public string UnitCaption
        {
            get { return m_MetricDefinition.UnitCaption; }
        }

        /// <summary>
        /// Gets the instance name for this sampled metric.
        /// </summary>
        public string InstanceName
        {
            get { return m_WrappedMetric.InstanceName; }
        }

        /// <summary>
        /// Indicates whether this is the default metric instance for this metric definition or not.
        /// </summary>
        /// <remarks>The default instance has a null instance name.  This property is provided as a convenience to simplify
        /// client code so you don't have to distinguish empty strings or null.</remarks>
        public bool IsDefault
        {
            get { return m_WrappedMetric.IsDefault; }
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// The internal custom sampled metric we're wrapping. 
        /// </summary>
        internal Monitor.CustomSampledMetric WrappedMetric { get { return m_WrappedMetric; } }

        #endregion
    }
}
