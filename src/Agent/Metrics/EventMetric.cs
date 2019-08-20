using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Loupe.Core;


namespace Loupe.Agent.Metrics
{
    /// <summary>
    /// A single event metric instance object, representing one instance of an event
    /// metric definition.
    /// </summary>
    /// <remarks>
    /// 	<para>
    ///         An EventMetric instance can only be created for a specific <see cref="EventMetricDefinition">EventMetricDefinition</see> which has been previously
    ///         registered.
    ///     </para>
    /// 	<para>
    ///         To protect against duplicates with the same instance name, EventMetric does not
    ///         have public constructors. Instead, use the static <see cref="Register(Loupe.Agent.Metrics.EventMetricDefinition, string)">EventMetric.Register</see>
    ///         method with appropriate arguments to identify the specific event metric
    ///         definition under which to create an event metric instance with a designated
    ///         instance name.
    ///     </para>
    /// 	<para>Once created, you can record samples for an event metric by:</para>
    /// 	<list type="number">
    /// 		<item>
    ///             Calling <see cref="CreateSample()">CreateSample</see> to start a new metric
    ///             sample.
    ///         </item>
    /// 		<item>
    ///             Populating the data by calling <see cref="EventMetricSample.SetValue(string, object)">SetValue</see> for each value
    ///             column you wish to record.
    ///         </item>
    /// 		<item>
    ///             Call <see cref="EventMetricSample.Write">Write</see> on the sample to have
    ///             it be locked and written to the session file.
    ///         </item>
    /// 	</list>
    /// 	<para>
    ///         Alternatively, for event metrics defined via attributes (see <see cref="EventMetricAttribute">EventMetricAttribute</see> and <see cref="EventMetricValueAttribute">EventMetricValueAttibute</see>), the event metric
    ///         can be sampled directly from a user data object of the corresponding type which
    ///         contained the attributes by simply calling <see cref="EventMetricDefinition.WriteSample(object)">WriteSample</see> from the event
    ///         metric definition or from the specific event metric instance. In this case all
    ///         data is read and then written to the session file automatically.
    ///     </para>
    /// 	<para>For more information on how to take advantage of Event Metrics, see <a href="Metrics_EventMetricDesign.html">Developer's Reference - Metrics - Designing Event
    ///     Metrics</a>.</para>
    /// 	<para><strong>Sampled Metrics</strong></para>
    /// 	<para>
    ///         An alternative to Event Metrics are Sampled Metrics. Sampled Metrics are
    ///         designed to record a single, summarized value on a periodic basis. For more
    ///         information on the difference between Sampled and Event Metrics, see <a href="Metrics_SampledEventMetrics.html">Developer's Reference - Metrics - Sampled
    ///         and Event Metrics</a>. For more information on Sampled Metrics, see <see cref="SampledMetric">SampledMetric Class</see>.
    ///     </para>
    /// 	<para><strong>Viewing Metrics</strong></para>
    /// 	<para>Metrics are visible in the <a href="Viewer_Session_Introduction.html">Session
    ///     Viewer</a> of <a href="Viewer_Introduction.html">Loupe Desktop</a>. Metrics
    ///     are not displayed in the Loupe Live Viewer.</para>
    /// </remarks>
    /// <seealso cref="EventMetricSample" cat="Related Classes">EventMetricSample Class</seealso>
    /// <seealso cref="EventMetricDefinition" cat="Related Classes">EventMetricDefinition Class</seealso>
    /// <seealso cref="EventMetricAttribute" cat="Related Classes">EventMetricAttribute Class</seealso>
    /// <seealso cref="EventMetricValueAttribute" cat="Related Classes">EventMetricValueAttribute Class</seealso>
    /// <seealso cref="SampledMetric" cat="Related Classes">SampledMetric Class</seealso>
    /// <example>
    /// 	<code lang="CS" description="The following example creates an event metric entirely through code and then writes a sample.">
    /// 		<![CDATA[
    /// public static void RecordCacheMetric(int pagesLoaded)
    /// {
    ///     EventMetricDefinition cacheMetric;
    ///  
    ///     //so we can be called multiple times we want to see if the definition already exists.
    ///     if (EventMetricDefinition.TryGetValue("LoupeSample", "Database.Engine", "Cache", out cacheMetric) == false)
    ///     {
    ///         cacheMetric = new EventMetricDefinition("LoupeSample", "Database.Engine", "Cache");
    ///  
    ///         //add the values (that are part of the definition)
    ///         cacheMetric.AddValue("pages", typeof(int), SummaryFunction.Average, "Pages", "Pages in Cache", "Total number of pages in cache");
    ///         cacheMetric.AddValue("size", typeof(int), SummaryFunction.Average, "Bytes", "Cache Size", "Total number of bytes used by pages in cache");
    ///  
    ///         //and now that we're done, we need to register this definition.  This locks the definition
    ///         //and makes it go live.  This is passed by ref because if another thread registered the same metric, we'll get the
    ///         //true registered object (whoever won the race), not necessarily the one we've just created to pass in.
    ///         EventMetricDefinition.Register(ref cacheMetric);
    ///     }
    ///  
    ///     //Now we can get the specific metric we want to record samples under (this is an instance of the definition)
    ///     EventMetric cacheEventMetric = EventMetric.Register(cacheMetric, null);
    ///  
    ///     //now go ahead and write that sample.
    ///     EventMetricSample newSample = cacheEventMetric.CreateSample();
    ///     newSample.SetValue("pages", pagesLoaded);
    ///     newSample.SetValue("size", pagesLoaded * 8196);
    ///     newSample.Write();
    /// }]]>
    /// 	</code>
    /// 	<code lang="CS" description="The same results can be achieved with a more declarative approach by defining a metric data object decorated with attributes to indicate what properties to store as part of the event metric. You can decorate an existing object if that's convenient. The values of the decorated properties are read during the call to Write so the same object can be repeatedly sampled safely.">
    /// 		<![CDATA[
    /// /// <summary>
    /// /// Record an event metric using an object
    /// /// </summary>
    /// /// <param name="pagesLoaded"></param>
    /// public static void RecordCacheMetricByObject(int pagesLoaded)
    /// {
    ///     CacheEventMetric sample = new CacheEventMetric(pagesLoaded);
    ///     EventMetric.Write(sample);
    /// }
    ///  
    /// //The above code relies on the following class being defined.
    ///  
    /// /// <summary>
    /// /// Log event metrics using a single object
    /// /// </summary>
    /// [EventMetric("LoupeSample", "Database.Engine", "Cache - Declarative", Caption = "Simple Cache", Description = "Performance metrics for the database engine")]
    /// public class CacheEventMetric
    /// {
    ///     public CacheEventMetric(int pagesLoaded)
    ///     {
    ///         Pages = pagesLoaded;
    ///         Size = pagesLoaded * 8192;
    ///     }
    ///  
    ///     [EventMetricValue("pages", SummaryFunction.Average, "Pages", Caption = "Pages in Cache", Description = "Total number of pages in cache")]
    ///     public int Pages { get; private set; }
    ///  
    ///     [EventMetricValue("size", SummaryFunction.Average, "Bytes", Caption = "Cache Size", Description = "Total number of bytes used by pages in cache")]
    ///     public int Size { get; private set; }
    /// }]]>
    /// 	</code>
    /// </example>
    /// <seealso cref="!:Metrics_Introduction.html" cat="Developer's Reference">Metrics - Introduction</seealso>
    /// <seealso cref="!:Metrics_SampledEventMetrics.html" cat="Developer's Reference">Metrics - Sampled and Event Metrics</seealso>
    public sealed class EventMetric : IComparable<EventMetric>, IEquatable<EventMetric>
    {
        private readonly Core.Monitor.EventMetric m_WrappedMetric;
        private readonly EventMetricDefinition m_MetricDefinition;

        /// <summary>
        /// Create a new event metric object from the provided event metric definition
        /// </summary>
        /// <remarks>The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The metric definition for the metric instance</param>
        /// <param name="instanceName">The unique name of this instance within the metric's collection.</param>
        internal EventMetric(EventMetricDefinition definition, string instanceName)
            : this(definition, new Core.Monitor.EventMetric(definition.WrappedDefinition, instanceName))
        {
            // Let our other constructor handle the rest.
        }

        /// <summary>
        /// Create a new metric with the provided API event metric definition and an internal event metric object to wrap.
        /// </summary>
        /// <remarks>The new metric will automatically be added to the metric definition's metrics collection.</remarks>
        /// <param name="definition">The API event metric definition that defines this metric</param>
        /// <param name="metric">The internal event metric</param>
        internal EventMetric(EventMetricDefinition definition, Core.Monitor.EventMetric metric)
        {
            m_MetricDefinition = definition;
            m_WrappedMetric = metric;
        }


        #region Public Properties and Methods


        /// <summary>Registers all event metric definitions defined by attributes on the provided object or Type,
        /// and registers metric instances where EventMetricInstanceName attribute is also found (with a live object).</summary>
        /// <param name="metricData">An object or Type defining event metrics via attributes on itself or on its base types or interfaces.</param>
        /// <remarks>
        /// 	<para>
        ///         This call ensures that the reflection scan of all members looking for
        ///         attributes across the entire inheritance of an object instance or Type has been
        ///         done (e.g. outside of a critical path) so that the first call to <see cref="Write(object)">Write</see> will not have to do that work within a critical path.
        ///         Results are cached internally, so redundant calls to this method will not
        ///         repeat the scan for types already scanned (including as part of a different
        ///         top-level type).
        ///     </para>
        /// 	<para>
        ///         If a live object is given (not just a Type) then the member(s) decorated with
        ///         an <see cref="EventMetricInstanceNameAttribute">EventMetricInstanceNameAttribute Class</see>
        ///         will be queried and used to also register an event metric instance with the
        ///         returned name
        ///     </para>
        /// 	<para>If a Type is given instead of a live object, it can't be queried for instance
        ///     name(s) and will only register the event metric definitions. Metric instances will
        ///     still be created automatically as needed when Write is called.</para>
        /// </remarks>
        /// <seealso cref="Write(object)">Write Method</seealso>
        /// <exception caption="" cref="ArgumentNullException">Thrown if metricData is null.</exception>
        /// <exception caption="" cref="ArgumentException">The specified metricDataObjectType does not have an EventMetric attribute &lt;br /&gt;
        /// &lt;br /&gt;
        /// -or- &lt;br /&gt;
        /// &lt;br /&gt;
        /// The specified Type does not have a usable EventMetric attribute, so it can't be used to define an event metric.&lt;br /&gt;
        /// &lt;br /&gt;
        /// -or- &lt;br /&gt;
        /// &lt;br /&gt;
        /// The specified Type's EventMetric has an empty metric namespace which is not allowed, so no metric can be defined.&lt;br /&gt;
        /// &lt;br /&gt;
        /// -or- &lt;br /&gt;
        /// &lt;br /&gt;
        /// The specified Type's EventMetric has an empty metric category name which is not allowed, so no metric can be defined.&lt;br /&gt;
        /// &lt;br /&gt;
        /// -or- &lt;br /&gt;
        /// &lt;br /&gt;
        /// The specified Type's EventMetric has an empty metric counter name which is not allowed, so no metric can be defined.&lt;br /&gt;
        /// &lt;br /&gt;
        /// -or- &lt;br /&gt;
        /// &lt;br /&gt;
        /// The specified Type's EventMetric attribute's 3-part Key is already used for a metric definition which is not an event metric.</exception>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// <code title="" description="" lang="neutral"></code></example>
        public static void Register(object metricData)
        {
            //we need a live object, not a null object or we'll fail
            if (metricData == null)
            {
                throw new ArgumentNullException(nameof(metricData));
            }

            // Register all of the event metric definitions it contains, object or Type:
            EventMetricDefinition[] definitions = EventMetricDefinition.RegisterAll(metricData);

            if ((metricData is Type) == false)
            {
                // They gave us a live object, not just a Type, so see if there are metric instances we can register.
                foreach (EventMetricDefinition definition in definitions)
                {
                    if (definition.IsBound && definition.NameBound)
                    {
                        string instanceName = definition.InvokeInstanceNameBinding(metricData);

                        if (instanceName != null) // null means it didn't find one, so we won't register an instance.
                        {
                            // An empty string (meaning the found value was null or empty) will be registered (same as null).
                            Register(definition, instanceName);
                        }
                    }
                }
            }
        }

        /// <summary>Return a registered event metric instance for the provided event metric definition.</summary>
        /// <remarks><para>If the provided event metric definition is an unregistered raw definition, it will be registered
        /// as a completed definition (or a matching registered event metric definition will be used in place of it), but
        /// an inability to successfully register the definition will result in an ArgumentException, as with calling
        /// the Register() method in EventMetricDefinition.  Using a properly-registered definition is preferred.</para>
        /// <para>If an event metric with that instance name already exists for that registered definition, it will be
        /// returned.  Otherwise, one will be created from that definition and returned.</para></remarks>
        /// <param name="definition">The metric definition for the desired metric instance.</param>
        /// <param name="instanceName">The desired instance name (may be null for the default instance).</param>
        /// <returns>The EventMetric object for the requested event metric instance.</returns>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        public static EventMetric Register(EventMetricDefinition definition, string instanceName)
        {
            if (definition == null)
            {
                // Uh-oh. They gave us a non-EventMetricDefinition?
                return null;
            }

            EventMetricDefinition metricDefinition;
            lock (definition.Lock)
            {
                if (definition.IsReadOnly == false)
                {
                    // Uh-oh.  They gave us a raw event metric definition which wasn't registered.
                    // But they're calling Register(), so they'd expect us to complete registration for them in this call.
                    metricDefinition = definition.Register();
                }
                else
                {
                    // Assume this is a registered definition. ToDo: Make sure they only get IsReadOnly when actually registered.
                    metricDefinition = definition;
                }
            }

            EventMetric eventMetric;
            EventMetricCollection metrics = metricDefinition.Metrics;
            lock (metrics.Lock)
            {
                if (metrics.TryGetValue(instanceName, out eventMetric) == false)
                {
                    eventMetric = metrics.Add(instanceName);
                }
            }
            return eventMetric;
        }

        /// <summary>
        /// Create a new, empty metric sample for this event metric instance, ready to be filled out and written.
        /// </summary>
        /// <remarks><para>This creates an empty sample for the current event metric instance, which needs to be filled out
        /// and written.  Set the value columns by calling newSample.SetValue(...), and write it to the Loupe log by
        /// calling newSample.Write().</para>
        /// <para>To record samples for event metrics defined via attributes, call eventMetricInstance.WriteSample(userDataObject)
        /// or EventMetric.Write(userDataObject).</para></remarks>
        /// <returns>The new metric sample object.</returns>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        public EventMetricSample CreateSample()
        {
            return new EventMetricSample(this, m_WrappedMetric.CreateSample());
        }


        /// <summary>
        /// Create a new sample for this metric and populate it with data from the provided user data object.  The caller must write this sample for it to be recorded.
        /// </summary>
        /// <remarks>
        /// The provided user data object must be compatible with the object type used to initialize this event metric.
        /// </remarks>
        /// <param name="userDataObject">The object to retrieve metric values from</param>
        /// <returns>The new metric sample object</returns>
        internal EventMetricSample CreateSample(object userDataObject)
        {
            if (userDataObject == null)
            {
                throw new ArgumentNullException(nameof(userDataObject));
            }

            if (Definition.IsBound == false)
            {
                throw new ArgumentException("This event metric's definition is not bound to sample automatically from a user data object.  CreateSample() and SetValue() must be used to specify the data values directly.");
            }

            Type userDataType = userDataObject.GetType();
            if (Definition.BoundType.IsAssignableFrom(userDataType) == false)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The provided user data object type ({0}) is not assignable to this event metric's bound type ({1}) and can not be sampled automatically for this metric instance.",
                                                          userDataType, Definition.BoundType));
            }

            EventMetricSample metricSample = CreateSample();

            foreach (EventMetricValueDefinition valueDefinition in Definition.ValueCollection)
            {
                if (valueDefinition.Bound == false)
                    continue; // Can't sample values that aren't bound.

                try
                {
                    // Get the numerator value...
                    // ToDo: Change value definition to use NVP (or a new Binding class).
                    NameValuePair<MemberTypes> binding = new NameValuePair<MemberTypes>(valueDefinition.MemberName, valueDefinition.MemberType);
                    BindingFlags dataBindingFlags = Definition.GetBindingFlags(binding);

                    // This should throw an exception if DataBinding isn't valid, so we'll bail on this column.
                    object rawData = userDataType.InvokeMember(valueDefinition.MemberName, dataBindingFlags, null, userDataObject, null, CultureInfo.InvariantCulture);
                    metricSample.SetValue(valueDefinition, rawData); // This will handle conversion as needed.
                }
                catch
                {
#if DEBUG
                    if (Debugger.IsAttached)
                        Debugger.Break();
#endif
                    // We can't write this column if we got an error reading the data.  Write the sample without it?
                }
            }

            return metricSample;
        }

        /// <summary>
        /// Write an event metric sample for this event metric instance using the provided data object.
        /// </summary>
        /// <remarks>The provided user data object must be assignable to the bound type which defined this event metric
        /// via attributes.</remarks>
        /// <param name="metricData">The object to retrieve metric values from.</param>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        public void WriteSample(object metricData)
        {
            //use our normal create sample method, but write it out immediately!
            CreateSample(metricData).Write();
        }

        /// <summary>
        /// Write event metric samples for all event metrics defined on the provided data object by attributes.
        /// </summary>
        /// <remarks>The provided user data object must be assignable to the bound type which defined this event metric
        /// via attributes.</remarks>
        /// <param name="metricData">The object to retrieve both metric values and definition from</param>
        /// <param name="fallbackInstanceName">The instance name to fall back on if a definition does not specify an instance name binding (may be null).</param>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        public static void Write(object metricData, string fallbackInstanceName)
        {
            // The real logic is in EventMetricDefinition.
            EventMetricDefinition.Write(metricData, fallbackInstanceName);
        }

        /// <summary>
        /// Write event metric samples for all event metrics defined on the provided data object by attributes.
        /// </summary>
        /// <remarks>The provided user data object must be assignable to the bound type which defined this event metric
        /// via attributes.</remarks>
        /// <param name="metricData">The object to retrieve both metric values and definition from</param>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        public static void Write(object metricData)
        {
            // The real logic is in EventMetricDefinition.
            EventMetricDefinition.Write(metricData, null);
        }

        /// <summary>
        /// Write a metric sample to the current process log if it hasn't been written already.
        /// </summary>
        /// <param name="metricSample">The metric sample to write.</param>
        [Obsolete("The non-static metricSample.Write() method should be used instead.", true)]
        public static void Write(EventMetricSample metricSample)
        {
            metricSample.Write(); // If it does happen to call, bypassing the error flag, it can forward and work.
        }

        /// <summary>
        /// Write a metric sample to the current process log if it hasn't been written already.
        /// </summary>
        /// <param name="metricSample">The metric sample to write.</param>
        /// <param name="str">A meaningless string to fit the overload.</param>
        [Obsolete("The non-static metricSample.Write() method should be used instead.", true)]
        public static void Write(EventMetricSample metricSample, string str)
        {
            metricSample.Write(); // If it does happen to call, bypassing the error flag, it can forward and work.
        }

        /// <summary>
        /// This is a bogus overload to prevent incorrect usage of this method when attempting to write a metric sample.
        /// </summary>
        /// <param name="metricSample">The metric sample to be written.</param>
        [Obsolete("The non-static metricSample.Write() method should be used instead.", true)]
        public void WriteSample(EventMetricSample metricSample)
        {
            metricSample.Write(); // If it does happen to call, bypassing the error flag, it can forward and work (maybe).
        }

        /// <summary>
        /// Indicates the relative sort order of this object to another of the same type.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(EventMetric other)
        {
            //we let our internal objects do the compare, we're really just wrapping things
            return WrappedMetric.CompareTo(other.WrappedMetric);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(EventMetric other)
        {
            if (other == null) return false;

            //We're really just a type cast, refer to our base object
            return WrappedMetric.Equals(other.WrappedMetric);
        }


        /// <summary>
        /// The definition of this event metric.
        /// </summary>
        public EventMetricDefinition Definition { get { return m_MetricDefinition; } }

        /// <summary>
        /// The unique Id of this event metric instance.  This can reliably be used as a key to refer to this item, within the same session which created it.
        /// </summary>
        /// <remarks>The Id is limited to a specific session, and thus identifies a consistent unchanged definition. The
        /// Id can <b>not</b> be used to identify a definition across different sessions, which could have different
        /// actual definitions due to changing user code.  See the Key property to identify a metric definition across
        /// different sessions.</remarks>
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
        /// The category of this metric for display purposes.  Category is the top displayed hierarchy.
        /// </summary>
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
        /// Gets the instance name for this event metric.
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
        /// The internal Metric object we're wrapping.
        /// </summary>
        internal Core.Monitor.EventMetric WrappedMetric { get { return m_WrappedMetric; } }

        #endregion
    }
}
