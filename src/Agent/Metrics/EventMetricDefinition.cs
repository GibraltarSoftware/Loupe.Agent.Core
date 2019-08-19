
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Gibraltar.Agent.Metrics.Internal;
using Loupe.Extensibility.Data;
using Loupe.Metrics;
using Microsoft.Extensions.Logging;
using IMetricDefinition = Gibraltar.Agent.Metrics.Internal.IMetricDefinition;



namespace Gibraltar.Agent.Metrics
{
    /// <summary>
    /// The definition of an event metric, which must be registered before any specific event metric instance can be created or sampled.
    /// </summary>
    /// <remarks>
    /// 	<para>Unlike sampled metrics which represent continuous values by sampling at
    ///     periodic intervals, event metrics have meaning only at discrete moments in time
    ///     when some "event" happens and records a sample to describe it.</para>
    /// 	<para>Event metrics can define multiple values to be collected with each sample and
    ///     can include both numeric data types (recorded as their native type) and strings
    ///     (all non-numeric types are converted to strings). Numeric data columns can then be
    ///     processed later to be graphed like Sampled Metrics. Both numeric and string data
    ///     can be analyzed in a variety of ways to produce charts. This makes event metrics a
    ///     powerful instrument for analyzing your application's behavior.</para>
    /// 	<para>For more information Event Metrics, see <a href="Metrics_EventMetricDesign.html">Developer's Reference - Metrics - Designing Event
    ///     Metrics</a>.</para>
    /// 	<para><strong>Defining Event Metrics</strong></para>
    /// 	<para>Event metrics can be defined either programmatically or declaratively with
    ///     attributes.</para>
    /// 	<para>
    ///         To define an event metric with attributes, apply the <see cref="EventMetricAttribute">EventMetric</see> attribute to the source code for any
    ///         class, struct, or interface, and apply the <see cref="EventMetricValueAttribute">EventMetricValue</see> attribute to desired members
    ///         to define the value columns. This approach provides a simple and powerful way
    ///         to design and collect event metrics for your application. See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
    ///     </para>
    /// 	<para>
    ///         To define an event metric programmatically requires more coding, but allows you
    ///         to optimize the performance of recording event metrics and works in
    ///         environments where it isn't feasible to decorate a class with attributes. See
    ///         the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
    ///     </para>
    /// </remarks>
    /// <example>
    /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
    /// </example>
    /// <seealso cref="!:Metrics_EventMetricDesign.html" cat="Developer's Reference">Metrics - Designing Event Metrics</seealso>
    /// <seealso cref="EventMetric" cat="Related Classes">EventMetric Class</seealso>
    public sealed class EventMetricDefinition : IMetricDefinition, IComparable<EventMetricDefinition>, IEquatable<EventMetricDefinition>
    {
        private readonly ILogger m_Logger;
        private readonly Monitor.EventMetricDefinition m_WrappedDefinition;
        private readonly EventMetricCollection m_Metrics;
        private readonly EventMetricValueDefinitionCollection m_MetricValues;

        private static readonly MetricDefinitionCollection s_Definitions = Log.MetricDefinitions;
        private static readonly Dictionary<Type, Type[]> s_InheritanceMap = new Dictionary<Type, Type[]>(); // Array of all inherited types (that have attributes), by type.
        private static readonly Dictionary<Type, EventMetricDefinition> s_DefinitionMap = // LOCKED definition by specific bound type.
            new Dictionary<Type, EventMetricDefinition>();
        private static readonly object s_DictionaryLock = new object(); // Lock for the DefinitionMap dictionary.

        /// <summary>
        /// Create a new (empty) event metric definition, ready to have value columns defined for it (with AddValue()).
        /// </summary>
        /// <remarks><para>In any session there should only be one metric definition with a given combination of metric
        /// type, category, and counter name (the three-part Key).  These values together are used to correlate metrics
        /// between sessions and to distinguish all metrics from each other within a session.  Collisions between
        /// incompatible metrics (different definitions) using the same three-part Key could result in run-time errors.</para>
        /// <para>After creating a new empty event metric definition, some number of value columns must be defined for it
        /// by calling newDefinition.AddValue(...).  Once all desired columns have been added, the definition must be
        /// officially registered by calling newDefinition = newDefinition.Register().  The registration checks again that
        /// the 3-part Key is not already defined, and if it <i>was</i>, it returns the existing registration (or throws
        /// an exception if the existing registration is not compatible), so it is important to use the <i>returned</i>
        /// event metric definition rather than the one passed in.  This is done for thread safety, in case two threads
        /// find the same event metric not yet defined and both try to create the definition; as long as they define the
        /// event metric the same way (for that same Key), they will both get the same completed definition returned,
        /// without errors.  It is not necessary to save the return if the variable is not to be used further (e.g. going
        /// out of scope); the official definition can also be looked up separately later, without going through another
        /// new definition object.</para>
        /// <para>Event metric definitions can also be created by applying EventMetric and EventMetricValue attributes
        /// to a class, struct, or interface and registering that Type (or an object assignable to that type) using
        /// the static Register() method.  The constructor is not used when using attribute-based metric definitions.</para>
        /// </remarks>
        /// <param name="metricsSystem">The metrics capture system label.</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        public EventMetricDefinition(string metricsSystem, string categoryName, string counterName)
            : this(new Monitor.EventMetricDefinition(metricsSystem, categoryName, counterName))
        {
        }

        /// <summary>
        /// Create a new API event metric definition from the provided internal event metric definition.
        /// </summary>
        /// <param name="metricDefinition">The internal metric definition to wrap.</param>
        internal EventMetricDefinition(Monitor.EventMetricDefinition metricDefinition)
        {
            m_WrappedDefinition = metricDefinition;
            m_Metrics = new EventMetricCollection(this);
            m_MetricValues = new EventMetricValueDefinitionCollection(this);

            // The internal definition no longer automatically adds itself to the internal collection of definitions.
            /*
            // Now we have to add this specific API definition to the collection of API definitions.
            // But we can't just Add(this) because that would try to add it internally, too.  So instead,
            // We just want to set up the mapping to externalize the internal definition to point to us.
            s_Definitions.Internalize(this);
            */

            m_Logger = ApplicationLogging.CreateLogger<EventMetricDefinition>();
        }

        #region Public Properties and Methods

        /// <summary>
        /// Create a new value column definition with the supplied name and type.  The name must be unique within this definition.
        /// </summary>
        /// <remarks>Internally, only simple types are supported.  Any non-numeric, non-DateTimeOffset type will be converted to a string
        /// using the default ToString capability when it is recorded.</remarks>
        /// <param name="name">The unique name for this value column definition.</param>
        /// <param name="type">The simple type of this value (e.g. typeof(int) or typeof(string)).</param>
        /// <param name="summaryFunction">The default way that individual samples of this value column can be aggregated
        /// to create a graphable summary. (Use SummaryFunction.Count for non-numeric types.)</param>
        /// <param name="unitCaption">A displayable caption for the units this value represents, or null for unit-less values.</param>
        /// <param name="caption">The end-user display caption for this value column.</param>
        /// <param name="description">The end-user description for this value column.</param>
        /// <returns>The newly created value column definition.</returns>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        public EventMetricValueDefinition AddValue(string name, Type type, SummaryFunction summaryFunction, string unitCaption, string caption, string description)
        {
            // Error checking will be done by Values.Add(...), which is also publicly available.

            //create a new value definition
            EventMetricValueDefinition newDefinition = ValueCollection.Add(name, type, summaryFunction, unitCaption, caption, description);

            return newDefinition;
        }

        /// <summary>
        /// Find or create multiple event metrics definitions (defined via EventMetric attributes) for the provided object or Type.
        /// </summary>
        /// <remarks>The provided Type or the GetType() of the provided object instance will be scanned for EventMetric
        /// attributes on itself and any of its interfaces to identify a list of event metrics defined for instances of
        /// that type, creating them as necessary by scanning its members for EventMetricValue attributes.  Inheritance
        /// will be followed into base types, along with all interfaces inherited to the top level.  This method will not
        /// throw exceptions, so a null argument will return an empty array, as will an argument which does not define any
        /// valid event metrics.  Also see RegisterType(Type) to find or create a single event metric definition for a specific
        /// Type.</remarks>
        /// <param name="metricData">A Type or an instance defining event metrics by attributes on itself and/or its interfaces.</param>
        /// <returns>An array of zero or more event metric definitions found for the provided object or Type.</returns>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        /// <exception cref="ArgumentException">The specified metricDataObjectType does not have an EventMetric attribute 
        /// -or- 
        /// The specified Type does not have a usable EventMetric attribute, so it can't be used to define an event metric.
        /// -or-
        /// The specified Type's EventMetric has an empty metric namespace which is not allowed, so no metric can be defined.
        /// -or-
        /// The specified Type's EventMetric has an empty metric category name which is not allowed, so no metric can be defined.
        /// -or-
        /// The specified Type's EventMetric has an empty metric counter name which is not allowed, so no metric can be defined.
        /// -or-
        /// The specified Type's EventMetric attribute's 3-part Key is already used for a metric definition which is not an event metric.</exception>
        internal static EventMetricDefinition[] RegisterAll(object metricData)
        {
            List<EventMetricDefinition> definitions = new List<EventMetricDefinition>();

            if (metricData != null)
            {
                // Either they gave us a Type, or we need to get the type of the object instance they gave us.
                Type userObjectType = (metricData as Type) ?? metricData.GetType();

                EventMetricDefinition metricDefinition;
                Type[] inheritanceArray;
                bool foundIt;
                lock (s_InheritanceMap) // Apparently Dictionaries aren't internally threadsafe.
                {
                    foundIt = s_InheritanceMap.TryGetValue(userObjectType, out inheritanceArray);
                }
                if (foundIt)
                {
                    // We've already scanned this type, so use the cached array of types.
                    foreach (Type inheritedType in inheritanceArray)
                    {
                        try
                        {
                            metricDefinition = RegisterType(inheritedType);
                        }
                        catch
                        {
                            metricDefinition = null;
                        }
                        if (metricDefinition != null)
                            definitions.Add(metricDefinition); // Add it to the list if found.
                    }
                }
                else
                {
                    // New top-level type, we have to scan its inheritance.
                    List<Type> inheritanceList = new List<Type>(); // List of all the inherited types we find with attributes on them.

                    // First, see if the main type they gave us defines an event metric.
                    if (userObjectType.IsDefined(typeof (EventMetricAttribute), false))
                    {
                        try
                        {
                            inheritanceList.Add(userObjectType); // Add the top level Type to our list of types.
                            metricDefinition = RegisterType(userObjectType);
                        }
                        catch
                        {
                            metricDefinition = null;
                        }
                        if (metricDefinition != null)
                            definitions.Add(metricDefinition); // Add it to the list if found.
                    }

                    // Now check all of its interfaces.
                    Type[] interfaces = userObjectType.GetInterfaces();
                    foreach (Type interfc in interfaces)
                    {
                        if (interfc.IsDefined(typeof (EventMetricAttribute), false))
                        {
                            // We found an interface with the right Attribute, get its definition.
                            try
                            {
                                inheritanceList.Add(interfc); // Add the interface to our list of types.
                                metricDefinition = RegisterType(interfc);
                            }
                            catch
                            {
                                metricDefinition = null;
                            }
                            if (metricDefinition != null)
                                definitions.Add(metricDefinition); // Add it to the list if found.
                        }
                    }

                    // And finally, drill down it's inheritance...
                    Type baseObjectType = (userObjectType.GetTypeInfo().IsInterface) ? null : userObjectType.GetTypeInfo().BaseType;

                    // ...unless it's an interface.
                    while (baseObjectType != null && baseObjectType != typeof (object) && baseObjectType.GetTypeInfo().IsInterface == false)
                    {
                        // See if an ancestor Type defines an event metric.
                        if (baseObjectType.IsDefined(typeof (EventMetricAttribute), false))
                        {
                            try
                            {
                                inheritanceList.Add(baseObjectType); // Add the inherited base to our list of types.
                                metricDefinition = RegisterType(baseObjectType);
                            }
                            catch
                            {
                                metricDefinition = null;
                            }
                            if (metricDefinition != null)
                                definitions.Add(metricDefinition); // Add it to the list if found.
                        }

                        // No need to check its interfaces, we already got all of them from the top level.

                        baseObjectType = baseObjectType.GetTypeInfo().BaseType; // Get the next deeper Type.
                    }

                    // Now, remember the list of attributed types we found in this walk.
                    lock (s_InheritanceMap) // Apparently Dictionaries aren't internally threadsafe.
                    {
                        s_InheritanceMap[userObjectType] = inheritanceList.ToArray();
                    }
                }
            }

            // If they gave us a null, we'll just return an empty array.
            return definitions.ToArray();
        }

        /// <summary>
        /// Determine the readable Type for a field, property, or method.
        /// </summary>
        /// <remarks>This method assumes that only MemberTypes of Field, Property, or Method will be given.  A method
        /// with void return type will return typeof(void), and properties with no get accessor will return null.
        /// This does not currently check method signature info for the zero-argument requirement.</remarks>
        /// <param name="member">The MemberInfo of a Field, Property, or Method member.</param>
        /// <returns>The Type of value which can be read from the field, property, or method.</returns>
        private static Type GetTypeOfMember(MemberInfo member)
        {
            Type readType;
            if (member.MemberType == MemberTypes.Method)
            {
                // For methods, it's the return value type.
                readType = ((MethodInfo)member).ReturnType;
            }
            else if (member.MemberType == MemberTypes.Property)
            {
                // For properties, it's the property type...
                PropertyInfo propertyInfo = (PropertyInfo)member;

                // ...But we must check that there's something to actually read...
                if (propertyInfo.CanRead)
                    readType = propertyInfo.PropertyType; // Great, get its Type.
                else
                    readType = null; // There's no value to read!  We'll report errors below for each attribute.
            }
            else if (member.MemberType == MemberTypes.Field)
            {
                // For fields, it's the field type... They can always be read (through reflection, that is).
                FieldInfo fieldInfo = (FieldInfo)member;
                readType = fieldInfo.FieldType;
            }
            else
            {
                // Nothing else is supported; return null.
                readType = null;
            }

            return readType;
        }

        /// <summary>
        /// Find or create an event metric definition from EventMetric and EventMetricValue attributes on a specific Type.
        /// </summary>
        /// <remarks>The provided type must have an EventMetric attribute and can have one or more fields, properties
        /// or zero-argument methods with EventMetricValue attributes defined.  This method creates a metric definition
        /// but does not create a specific metric instance, so it does not require a live object.  If the event metric
        /// definition already exists, it is just returned and no exception is thrown.  If the provided type is not suitable
        /// to create an event metric from because it is missing the appropriate attribute or the attribute has been
        /// miss-defined, an ArgumentException will be thrown.  Inheritance and interfaces will <b>not</b> be searched, so
        /// the provided Type must directly define an event metric, but valid objects of a type assignable to the specified
        /// bound Type of this definition <b>can</b> be sampled from this specific event metric definition.  Also see
        /// AddOrGetDefinitions() to find and return an array of definitions.</remarks>
        /// <param name="metricDataObjectType">A specific Type with attributes defining an event metric.</param>
        /// <returns>The single event metric definition determined by attributes on the given Type.</returns>
        /// <exception cref="ArgumentException">The specified metricDataObjectType does not have an EventMetric attribute 
        /// -or- 
        /// The specified Type does not have a usable EventMetric attribute, so it can't be used to define an event metric.
        /// -or-
        /// The specified Type's EventMetric has an empty metric namespace which is not allowed, so no metric can be defined.
        /// -or-
        /// The specified Type's EventMetric has an empty metric category name which is not allowed, so no metric can be defined.
        /// -or-
        /// The specified Type's EventMetric has an empty metric counter name which is not allowed, so no metric can be defined.
        /// -or-
        /// The specified Type's EventMetric attribute's 3-part Key is already used for a metric definition which is not an event metric.</exception>
        internal static EventMetricDefinition RegisterType(Type metricDataObjectType)
        {
            // See if there is already a definition known on this Type.
            // If there is, we just want to return it and not do any more.
            EventMetricDefinition newMetricDefinition;

            // ToDo: Need to overhaul error reporting, should log before throwing exceptions in case they are caught internally.
            // And throwing exceptions may be pointless if they can never get here without us catching exceptions and swallowing them.
            lock (s_DictionaryLock)
            {
                if (s_DefinitionMap.TryGetValue(metricDataObjectType, out newMetricDefinition) == false)
                {
                    s_DefinitionMap[metricDataObjectType] = null; // Pre-set to null in case of exception, so we don't scan it again.

                    // Check if it defines it at this specific level, no inheritance search, no interfaces search.
                    if (metricDataObjectType.IsDefined(typeof(EventMetricAttribute), false) == false)
                    {
                        // Sorry, Attribute not found.
                        throw new ArgumentException(
                            "The specified Type does not have an EventMetric attribute, so it can't be used to define an event metric.",
                            nameof(metricDataObjectType));
                    }

                    // OK, now waltz off and get the attribute we want.
                    var eventMetricAttribute = metricDataObjectType.GetTypeInfo().GetCustomAttribute<EventMetricAttribute>();
                    // Try to cast it to the specific kind of attribute we need

                    // Verify that the event metric attribute that we got is valid
                    if (eventMetricAttribute == null)
                    {
                        throw new ArgumentException(
                            "The specified Type does not have a usable EventMetric attribute, so it can't be used to define an event metric.",
                            nameof(metricDataObjectType));
                    }

                    //make sure the user didn't do any extraordinary funny business
                    string metricsSystem = eventMetricAttribute.MetricsSystem;
                    if (string.IsNullOrEmpty(metricsSystem))
                    {
                        throw new ArgumentException(
                            "The specified Type's EventMetric has an empty metric namespace which is not allowed, so no metric can be defined.");
                    }

                    string metricCategoryName = eventMetricAttribute.MetricCategoryName;
                    if (string.IsNullOrEmpty(metricCategoryName))
                    {
                        throw new ArgumentException(
                            "The specified Type's EventMetric has an empty metric category name which is not allowed, so no metric can be defined.");
                    }

                    string metricCounterName = eventMetricAttribute.CounterName;
                    if (string.IsNullOrEmpty(metricCounterName))
                    {
                        throw new ArgumentException(
                            "The specified Type's EventMetric has an empty metric counter name which is not allowed, so no metric can be defined.");
                    }

                    // See if there is already a definition with the specified keys.
                    // If there is, we just want to return it and not do any more.

                    // We use a lock because we need to have the check and the add (which we do at the end) happen as a single event.
                    // We'll just hold the collection lock the whole time since we don't have to wait on arbitrary client code.
                    lock (Log.MetricDefinitions.Lock)
                    {
                        if (Log.MetricDefinitions.TryGetValue(metricsSystem, metricCategoryName, metricCounterName, out var existingMetricDefinition))
                        {
                            //eh, we already had a definition.  We want to go no further.
                            newMetricDefinition = existingMetricDefinition as EventMetricDefinition;
                            if (newMetricDefinition == null)
                            {
                                throw new ArgumentException(
                                    "The specified Type's EventMetric attribute's 3-part Key is already used for a metric definition which is not an event metric.");
                            }
                        }
                        else
                        {

                            //OK, now we know we'll be good.
                            newMetricDefinition = new EventMetricDefinition(metricsSystem, metricCategoryName, metricCounterName);
                            newMetricDefinition.BoundType = metricDataObjectType;
                            newMetricDefinition.Caption = eventMetricAttribute.Caption;
                            newMetricDefinition.Description = eventMetricAttribute.Description;

                            //now that we have our new metric definition, do our level best to add the rest of the information to it

                            bool TypeFilterName(MemberInfo info, object criteriaObj) => true;

                            //reflect all of the field/property/methods in the type so we can inspect them for attributes
                            MemberInfo[] members = metricDataObjectType.GetTypeInfo().FindMembers(MemberTypes.Field | MemberTypes.Method | MemberTypes.Property,
                                                                              BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                                                              BindingFlags.Instance, TypeFilterName, "*");

                            foreach (MemberInfo curMember in members)
                            {
                                //and what can we get from our little friend?
                                if (curMember.IsDefined(typeof(EventMetricInstanceNameAttribute), false))
                                {
                                    //have we already bound our instance name?
                                    if (newMetricDefinition.NameBound)
                                    {
                                        //yes, so report a duplicate name warning
                                    }
                                    else
                                    {
                                        //nope, so lets go for it, set up our binding information
                                        newMetricDefinition.NameBound = true;
                                        newMetricDefinition.NameMemberType = curMember.MemberType;
                                        newMetricDefinition.NameMemberName = curMember.Name;
                                    }
                                }

                                //What about mappings to values?
                                if (curMember.IsDefined(typeof(EventMetricValueAttribute), false))
                                {
                                    // What type of value does this member give?  It'll be the same for every value attribute on it!
                                    Type curType = GetTypeOfMember(curMember);

                                    //they have at least one event metric value attribute, go get all of them
                                    //we get back an array of objects just in case there are any non-CLS compliant attributes defined, which there never are.
                                    EventMetricValueAttribute[] curMemberValueAttributes = curMember.GetCustomAttributes<EventMetricValueAttribute>(false).ToArray();

                                    foreach (EventMetricValueAttribute curValueAttribute in curMemberValueAttributes)
                                    {
                                        //cast it and test
                                        if (curValueAttribute != null)
                                        {
                                            //apply defaults (because this is the only place to get the name of the marked member)
                                            if (string.IsNullOrEmpty(curValueAttribute.Name))
                                            {
                                                curValueAttribute.SetName(curMember.Name);
                                            }

                                            if (newMetricDefinition.ValueCollection.ContainsKey(curValueAttribute.Name)) // Warn about duplicates.
                                            {
                                            }
                                            else if (curType == null) // Warn about an unreadable property.
                                            {
                                            }
                                            else if (curType == typeof(void)) // Warn about a void method.
                                            {
                                            }
                                            else
                                            {
                                                //We finally have validated everything and we're ready to set up the new value.
                                                EventMetricValueDefinition newValue =
                                                    newMetricDefinition.ValueCollection.Add(curValueAttribute.Name,
                                                        curType);
                                                //set up our binding information
                                                newValue.Bound = true;
                                                newValue.MemberType = curMember.MemberType;
                                                newValue.MemberName = curMember.Name;

                                                //now that we've added it, what else can we set?
                                                newValue.UnitCaption = curValueAttribute.UnitCaption;
                                                newValue.SummaryFunction = curValueAttribute.SummaryFunction;
                                                newValue.Caption = curValueAttribute.Caption;
                                                newValue.Description = curValueAttribute.Description;

                                                //and finally, is this our default value?
                                                if ((newMetricDefinition.DefaultValue == null) &&
                                                    (curValueAttribute.IsDefaultValue))
                                                {
                                                    newMetricDefinition.DefaultValue = newValue;
                                                }
                                            }
                                        }
                                    }
                                } // End of if value attribute defined
                            } // End of foreach over members

                            // Indicate that the specified metric definition is a bound definition, and register ourselves.
                            newMetricDefinition.IsBound = true;
                            newMetricDefinition.WrappedDefinition.SetReadOnly(); // Mark the internal definition as completed.
                            Log.MetricDefinitions.Add(newMetricDefinition);
                        } // End of if Log.MetricDefinitions.TryGetValue ELSE
                    } // End of LOCK on Log.MetricDefinitions

                    s_DefinitionMap[metricDataObjectType] = newMetricDefinition; // Remember it for next time.
                } // End of if DefinitionMap.TryGetValue == false

                // Otherwise, we read out the definition we found on this Type before, so we'll just return it.
            } // End of LOCK on DefinitionMap

            return newMetricDefinition;
        }

        /// <summary>
        /// Register the referenced EventMetricDefinition template, or update the reference to the official definition
        /// if a compatible event metric definition already exists for the same 3-part Key.
        /// </summary>
        /// <remarks>This is the final step for creating a new event metric definition programmatically, after constructing
        /// a new EventMetricDefinition(...) and calling AddValue(...) as desired to define value columns.  If a metric
        /// definition is already registered with the same Key, it will be checked for compatibility.  An incompatible
        /// existing definition (e.g. a sampled metric, or missing value columns from the provided template) will result
        /// in an ArgumentException to signal your programming mistake; each different metric definition in an application
        /// session must have a unique 3-part Key.  If a compatible existing event metric definition is found, the reference
        /// to the EventMetricDefinition will be updated to the registered definition.  If no metric definition exists with
        /// the same 3-part key as the template, then the new definition will be officially registered and may be used as
        /// a valid definition.  This approach ensures thread-safe creation of singular event metric definitions without
        /// the need for locking by your code.</remarks>
        /// <param name="newDefinition">A reference to an event metric definition template to be registered, and to receive the official registered event metric definition.</param>
        /// <exception cref="ArgumentNullException">A null definition can not be registered nor used to look up a registered event metric definition.</exception>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        public static void Register(ref EventMetricDefinition newDefinition)
        {
            // ToDo: Consider copy-in/copy-out of newDefinition to protect against pathological clients bypassing our sanity checks.
            EventMetricDefinition theDefinition = newDefinition;
            if (theDefinition == null)
            {
                throw new ArgumentNullException(nameof(newDefinition), "A null definition can not be registered nor used to look up a registered event metric definition.");
            }

            EventMetricDefinition registeredDefinition = theDefinition.Register();
            // We don't overwrite newDefinition immediately, in case we get back a null, so we can still inspect the
            // template in case of errors.  Also, they can inspect it in a debugger after we throw this exception...
            if (registeredDefinition == null || registeredDefinition.IsReadOnly == false)
            {
                // Hmmm, this really should not happen if we've coded registration correctly.
                // Any errors should already throw exceptions above.
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, 
                        "Unknown error registering new event metric definition: metrics system ({0}), category name ({1}), counter name ({2})",
                        theDefinition.MetricsSystem, theDefinition.CategoryName, theDefinition.CounterName));
            }

            newDefinition = registeredDefinition; // Finally, update the reference to whatever the official registration is.
        }

        /// <summary>
        /// Register the referenced EventMetricDefinition template, or update the reference to the official definition
        /// if a compatible event metric definition already exists for the same 3-part Key.
        /// </summary>
        /// <remarks><para>This is the final step for creating a new event metric definition programmatically, after constructing
        /// a new EventMetricDefinition(...) and calling AddValue(...) as desired to define value columns.  If a metric
        /// definition is already registered with the same Key, it will be checked for compatibility.  An incompatible
        /// existing definition (e.g. a sampled metric, or missing value columns from the provided template) will result
        /// in an ArgumentException to signal your programming mistake; each different metric definition in an application
        /// session must have a unique 3-part Key.  If a compatible existing event metric definition is found, the reference
        /// to the EventMetricDefinition will be updated to the registered definition.  If no metric definition exists with
        /// the same 3-part key as the template, then the new definition will be officially registered and may be used as
        /// a valid definition.  This approach ensures thread-safe creation of singular event metric definitions without
        /// the need for locking by your code.</para>
        /// <para>This overload allows a value column of the definition template to be designated as the default one to graph
        /// for this event metric.  The specified name must match a value column name in the definition template or a
        /// KeyNotFoundException will be thrown (and the template will not be registered).  The defaultValue parameter
        /// will overwrite any previous setting of the DefaultValue property of the event metric definition template.  If
        /// the completed template is not ultimately used because a metric definition already exists with the same 3-part
        /// Key, then the defaultValue parameter will have no effect; a metric definition which is already registered can
        /// not be altered, to ensure consistency within the session log.</para>
        /// <para>Also see the overload directly taking an EventMetricValueDefinition as the defaultValue for an approach
        /// which may be less prone to mistakes.</para></remarks>
        /// <param name="newDefinition">A reference to an event metric definition template to be registered, and to receive the official registered event metric definition.</param>
        /// <param name="defaultValue">The name of a value column to designate as the default one to graph for this metric.</param>
        /// <exception cref="ArgumentNullException">A null newDefinition or a null or empty defaultValue was provided.</exception>
        /// <exception cref="KeyNotFoundException">The specified defaultValue column name was not found in the provided definition.</exception>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        public static void Register(ref EventMetricDefinition newDefinition, string defaultValue)
        {
            EventMetricDefinition theDefinition = newDefinition;
            if (theDefinition == null)
            {
                throw new ArgumentNullException(nameof(newDefinition), "A null definition can not be registered nor used to look up a registered event metric definition.");
            }

            if (string.IsNullOrEmpty(defaultValue))
            {
                throw new ArgumentNullException(nameof(defaultValue), "The specified defaultValue name must be a legal value column name and thus may not be null or empty.");
            }

            // The definition should be held only by one thread until we're actually registered, anyway, but just to be safe...
            // Lock the new definition so there can't be any other attempted changes while we do this.
            lock (theDefinition.Lock)
            {
                EventMetricValueDefinition defaultValueDefinition;
                try
                {
                    defaultValueDefinition = theDefinition.ValueCollection[defaultValue];
                }
                catch (KeyNotFoundException ex)
                {
                    throw new KeyNotFoundException(
                        string.Format(CultureInfo.InvariantCulture, "The specified defaultValue column name ({0}) was not found in the definition.",
                                      defaultValue), ex);
                }

                theDefinition.DefaultValue = defaultValueDefinition; // Set the DefaultValue to the one identified.

                // And finally do the actual registration with our other overload.
                Register(ref theDefinition);
            }
            newDefinition = theDefinition;
        }

        /// <summary>
        /// Register the referenced EventMetricDefinition template, or update the reference to the official definition
        /// if a compatible event metric definition already exists for the same 3-part Key.
        /// </summary>
        /// <remarks><para>This is the final step for creating a new event metric definition programmatically, after constructing
        /// a new EventMetricDefinition(...) and calling AddValue(...) as desired to define value columns.  If a metric
        /// definition is already registered with the same Key, it will be checked for compatibility.  An incompatible
        /// existing definition (e.g. a sampled metric, or missing value columns from the provided template) will result
        /// in an ArgumentException to signal your programming mistake; each different metric definition in an application
        /// session must have a unique 3-part Key.  If a compatible existing event metric definition is found, the reference
        /// to the EventMetricDefinition will be updated to the registered definition.  If no metric definition exists with
        /// the same 3-part key as the template, then the new definition will be officially registered and may be used as
        /// a valid definition.  This approach ensures thread-safe creation of singular event metric definitions without
        /// the need for locking by your code.</para>
        /// <para>This overload allows an EventMetricValueDefinition to be designated as the default value column to graph
        /// for this event metric.  When adding value columns to the definition template, the EventMetricValueDefinition
        /// returned by one of them can be saved to pass in this overload, for convenience.  The defaultValue parameter
        /// will overwrite any previous setting of the DefaultValue property of the event metric definition template.  If
        /// the completed template is not ultimately used because a metric definition already exists with the same 3-part
        /// Key, then the defaultValue parameter will have no effect; a metric definition which is already registered can
        /// not be altered, to ensure consistency within the session log.</para></remarks>
        /// <param name="newDefinition">A reference to an event metric definition template to be registered, and to receive the official registered event metric definition.</param>
        /// <param name="defaultValue">The definition of a value column in this event metric definition to designate as the default one to graph for this metric.</param>
        /// <exception cref="ArgumentNullException">A null newDefinition was provided.</exception>
        /// <exception cref="KeyNotFoundException">A defaultValue was provided but it is not from the provided EventMetricDefinition</exception>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        public static void Register(ref EventMetricDefinition newDefinition, EventMetricValueDefinition defaultValue)
        {
            EventMetricDefinition theDefinition = newDefinition;
            if (theDefinition == null)
            {
                throw new ArgumentNullException(nameof(newDefinition), "A null definition can not be registered nor used to look up a registered event metric definition.");
            }

            lock (theDefinition.Lock)
            {
                if (defaultValue != null &&
                    (defaultValue.Definition != theDefinition || theDefinition.ValueCollection.Contains(defaultValue) == false))
                {
                    throw new KeyNotFoundException("The event metric value column definition specified is not associated with the specified event metric definition.");
                }

                theDefinition.DefaultValue = defaultValue; // Set the DefaultValue to the one identified.

                // And finally do the actual registration with our other overload.
                Register(ref theDefinition);
            }
            newDefinition = theDefinition;
        }

        /// <summary>
        /// Register this instance as a completed definition and return the valid usable definition for this event metric.
        /// </summary>
        /// <remarks>This call is necessary to complete a new event metric definition (after calls to AddValue(...)) before
        /// it can be used, and it signifies that all desired value columns have been added to the definition.  Only the
        /// first registration of a metric definition with a given Key (metrics system, category name, and counter name)
        /// will be effective and return the same definition object; subsequent calls (perhaps by another thread) will
        /// instead return the existing definition already registered.  If a definition already registered with that Key
        /// can not be an event metric (e.g. a sampled metric is defined with that Key) or if this instance defined value
        /// columns not present as compatible value columns in the existing registered definition with that Key, then an
        /// ArgumentException will be thrown to signal your programming mistake.</remarks>
        /// <returns>The actual usable definition with the same metrics system, category name, and counter name as this instance.</returns>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        internal EventMetricDefinition Register()
        {
            EventMetricDefinition officialDefinition;
            // We should be held only by one thread until we're actually registered, anyway, but just to be safe...
            // Lock our own definition so there can't be any other attempted changes while we do this.
            lock (Lock)
            {

                // We need to lock the collection while we check for an existing definition and maybe add this one to it.
                lock (Log.MetricDefinitions.Lock)
                {
                    if (Log.MetricDefinitions.TryGetValue(MetricsSystem, CategoryName, CounterName, out var rawDefinition) == false)
                    {
                        // There isn't already one by that Key.  Great!  Register ourselves.
                        m_WrappedDefinition.SetReadOnly(); // Mark the internal definition as completed.
                        officialDefinition = this;
                        Log.MetricDefinitions.Add(this);
                    }
                    else
                    {
                        // Oooh, we found one already registered.  We'll want to do some checking on this, but outside the lock.
                        officialDefinition = rawDefinition as EventMetricDefinition;
                    }
                } // End of collection lock

                if (officialDefinition == null)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture,
                            "There is already a metric definition for the same metrics system ({0}), category name ({1}), and counter name ({2}), but it is not an event metric.",
                            MetricsSystem, CategoryName, CounterName));
                }
                else if (this != officialDefinition)
                {
                    // There was one other than us, make sure it's compatible with us.
                    // It's read-only, so we don't need the definition lock for this check.
                    IEventMetricValueDefinitionCollection officialValues = officialDefinition.WrappedDefinition.Values;
                    foreach (Monitor.EventMetricValueDefinition ourValue in WrappedDefinition.Values)
                    {
                        if (officialValues.TryGetValue(ourValue.Name, out var officialValue) == false)
                        {
                            // It doesn't have one of our value columns!
                            throw new ArgumentException(
                                string.Format(CultureInfo.InvariantCulture,
                                    "There is already an event metric definition for the same metrics system ({0}), category name ({1}), and counter name ({2}), but it is not compatible; it does not define value column \"{3}\".",
                                    MetricsSystem, CategoryName, CounterName, ourValue.Name));
                        }
                        else if (ourValue.SerializedType != ((Monitor.EventMetricValueDefinition)officialValue).SerializedType)
                        {
                            throw new ArgumentException(
                                string.Format(CultureInfo.InvariantCulture,
                                    "There is already an event metric definition for the same metrics system ({0}), category name ({1}), and counter name ({2}), but it is not compatible; " +
                                    "it defines value column \"{3}\" with type {4} rather than type {5}.",
                                    MetricsSystem, CategoryName, CounterName, ourValue.Name, officialValue.Type.Name, ourValue.Type.Name));
                        }
                    }

                    // We got through all the values defined in this instance?  Then we're okay to return the official one.
                }
                // Otherwise, it's just us, so we're all good.
            }

            return officialDefinition;
        }

        /// <summary>
        /// Indicates if this definition is configured to retrieve its information directly from an object.
        /// </summary>
        /// <remarks>When true, metric instances and samples can be defined from a live object of the same type that was used 
        /// to generate the data binding.  It isn't necessary that the same object be used, just that it be a compatible
        /// type to the original type used to establish the binding.</remarks>
        public bool IsBound
        {
            get { return m_WrappedDefinition.IsBound; }
            internal set { m_WrappedDefinition.IsBound = value; }
        }

        /// <summary>
        /// When bound, indicates the exact interface or object type that was bound.
        /// </summary>
        /// <remarks>When creating new metrics or metric samples, this data type must be provided in bound mode.</remarks>
        public Type BoundType
        {
            get { return m_WrappedDefinition.BoundType; }
            internal set { m_WrappedDefinition.BoundType = value; }
        }


        /// <summary>
        /// Indicates the relative sort order of this object to another of the same type.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(EventMetricDefinition other)
        {
            //we let our base object do the compare, we're really just casting things
            return WrappedDefinition.CompareTo(other?.WrappedDefinition);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(EventMetricDefinition other)
        {
            //We're really just a type cast, refer to our base object
            return WrappedDefinition.Equals(other?.WrappedDefinition);
        }


        /// <summary>
        /// The set of metrics that use this definition.
        /// </summary>
        /// <remarks>All metrics with the same definition are of the same object type.</remarks>
        internal EventMetricCollection Metrics { get { return m_Metrics; } }

        /// <summary>
        /// The set of values defined for this metric definition.
        /// </summary>
        /// <remarks>Any number of different values can be recorded along with each event to provide additional summarization
        /// and filtering ability for later client analysis.</remarks>
        internal EventMetricValueDefinitionCollection ValueCollection { get { return m_MetricValues; } }

        /// <summary>
        /// The set of values defined for this metric definition. (A snapshot array copy.  AddValue() through this definition object.)
        /// </summary>
        /// <remarks>Any number of different values can be recorded along with each event to provide additional summarization
        /// and filtering ability for later client analysis.  While the definition is being built (with <see cref="AddValue">AddValue</see>
        /// the current set of value definitions can be examined as an array snapshot returned by this property.  Changes
        /// to the array will only affect that copy.</remarks>
        public EventMetricValueDefinition[] ValueDefinitions
        {
            get { return m_MetricValues.ToArray(); }
        }

        /// <summary>
        /// Indicates whether the provided object is a numeric type or can only be graphed by a SummaryFunction.Count.
        /// </summary>
        /// <param name="type">The type to be verified.</param>
        /// <returns>True if the supplied type is mathematically graphable, false otherwise.</returns>
        public static bool IsNumericValueType(Type type)
        {
            // Just ask our internal class.
            return Monitor.EventMetricDefinition.IsTrendableValueType(type);
        }

        /// <summary>
        /// Indicates whether the provided type can be stored as a value or not.
        /// </summary>
        /// <remarks>Most types can be stored, with the value of non-numeric types being the string representation
        /// of the type. Collections, arrays, and other such sets can't be stored as a single value.</remarks>
        /// <param name="type">The type to be verified.</param>
        /// <returns>True if the supplied type is supported, false otherwise.</returns>
        public static bool IsSupportedValueType(Type type)
        {
            // Just ask our internal class.
            return Monitor.EventMetricDefinition.IsSupportedValueType(type);
        }

        /// <summary>
        /// The default value column to display for this event metric.  Typically this should be a numeric value column.
        /// </summary>
        public EventMetricValueDefinition DefaultValue
        {
            get
            {
                lock (Lock)
                {
                    return m_MetricValues.Externalize((Monitor.EventMetricValueDefinition)m_WrappedDefinition.DefaultValue);
                }
            }
            //We do set in a round-the-world fashion to guarantee that the provided default value's name is in our collection.
            set
            {
                lock (Lock)
                {
                    m_WrappedDefinition.DefaultValue = ((value == null) ? null : value.WrappedValueDefinition);
                }
            }
        }

        /// <summary>
        /// Write a metric sample to the specified instance of this event metric definition using the provided data object.
        /// </summary>
        /// <remarks>
        /// <para>This overload may only be used if this metric definition was created by EventMetric and EventMetricValue
        /// attributes on a particular Type (class, struct, or interface), and only for userDataObjects of a type assignable
        /// to that bound type for this definition.  Also see the static WriteAllSamples() method.</para>
        /// <para>The provided instanceName parameter will override any instance name binding set for this definition
        /// with an EventMetricInstanceName attribute (this method overload ignores the instance name binding).  The
        /// specified metric instance is created if it does not already exist.  See the other overloads taking a
        /// userDataObject as the first parameter to use the bound member to determine a metric instance name
        /// from the user data object automatically, with an optional fall-back instance name.</para>
        /// </remarks>
        /// <param name="instanceName">The instance name to use, or null or empty for the default metric instance.</param>
        /// <param name="userDataObject">A data object to sample, compatible with the binding type of this definition.</param>
        internal void WriteSample(string instanceName, object userDataObject)
        {
            if (userDataObject == null)
            {
                throw new ArgumentNullException(nameof(userDataObject));
            }

            bool weAreRegistered = true;
            if (IsReadOnly == false)
            {
                // Uh-oh, we're not actually a registered definition!  Try to register ourselves.
                EventMetricDefinition registeredDefinition = Register();

                if (registeredDefinition == null)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, 
                            "Unknown error registering event metric definition: metrics system ({0}), category name ({1}), counter name ({2}).",
                            MetricsSystem, CategoryName, CounterName));
                }
                if (this != registeredDefinition)
                {
                    weAreRegistered = false; // So we won't try to do this again below.
                    registeredDefinition.WriteSample(instanceName, userDataObject); // Have the registered one do it.
                }
            }
            
            if (weAreRegistered)
            {
                if (IsBound == false)
                {
                    throw new ArgumentException(
                        "This event metric definition is not bound to sample automatically from a user data object.  CreateSample() and SetValue() must be used to specify the data values directly.");
                }

                Type userDataType = userDataObject.GetType();
                if (BoundType.IsAssignableFrom(userDataType) == false)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, 
                            "The provided user data object type ({0}) is not assignable to this event metric's bound type ({1}) and can not be sampled automatically for this metric definition.",
                            userDataType, BoundType));
                }

                EventMetric metricInstance = EventMetric.Register(this, instanceName); // Get the particular instance specified.
                metricInstance.WriteSample(userDataObject); // And write a sample from the provided data object.
            }
        }

        /// <summary>
        /// Write a metric sample to an automatically-determined instance of this metric definition using the provided data object, with a fall-back instance name.
        /// </summary>
        /// <param name="metricData">A data object to sample, compatible with the binding type of this definition.</param>
        /// <param name="fallbackInstanceName">The instance name to fall back on if this definition does not specify an instance name binding (may be null).</param>
        /// <remarks>
        /// 	<para>This overload may only be used if this metric definition was created by EventMetric and EventMetricValue
        /// attributes on a particular Type (class, struct, or interface), and only for userDataObjects of a type assignable
        /// to that bound type for this definition.</para>
        /// 	<para>The metric instance name will be obtained from the member which was marked with the EventMetricInstanceName
        /// attribute.  If none is bound, the instance name parameter will be used as a fall-back.  The determined metric
        /// instance will be created if it does not already exist.</para>
        /// </remarks>
        /// <exception caption="" cref="ArgumentNullException">No metricData object was provided.</exception>
        /// <exception caption="" cref="ArgumentException">This event metric definition is not bound to sample automatically from a user data object.  CreateSample() and SetValue() must be used to specify the data values directly.&lt;br /&gt;
        /// -or-&lt;br /&gt;
        /// The provided user data object is not assignable to this event metric's bound type and can not be sampled automatically for this metric definition.</exception>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// <code title="" description="" lang="neutral"></code></example>
        public void WriteSample(object metricData, string fallbackInstanceName)
        {
            if (metricData == null)
            {
                throw new ArgumentNullException(nameof(metricData));
            }

            bool weAreRegistered = true;
            if (IsReadOnly == false)
            {
                // Uh-oh, we're not actually a registered definition!  Try to register ourselves.
                EventMetricDefinition registeredDefinition = Register();

                if (registeredDefinition == null)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, 
                            "Unknown error registering event metric definition: metrics system ({0}), category name ({1}), counter name ({2}).",
                            MetricsSystem, CategoryName, CounterName));
                }
                if (this != registeredDefinition)
                {
                    weAreRegistered = false; // So we won't try to do this again below.
                    registeredDefinition.WriteSample(metricData, fallbackInstanceName); // Have the registered one do it.
                }
            }

            if (weAreRegistered)
            {
                if (IsBound == false)
                {
                    throw new ArgumentException(
                        "This event metric definition is not bound to sample automatically from a user data object.  CreateSample() and SetValue() must be used to specify the data values directly.");
                }

                Type userDataType = metricData.GetType();
                if (BoundType.IsAssignableFrom(userDataType) == false)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, 
                            "The provided user data object type ({0}) is not assignable to this event metric's bound type ({1}) and can not be sampled automatically for this metric definition.",
                            userDataType, BoundType));
                }

                string autoInstanceName = fallbackInstanceName; // Use the fall-back instance unless we find a specific instance name.

                if (NameBound)
                {
                    autoInstanceName = InvokeInstanceNameBinding(metricData) ?? fallbackInstanceName; // Use fall-back on errors.
                }

                if (string.IsNullOrEmpty(autoInstanceName))
                    autoInstanceName = null; // Convert empty string back to null.

                // Now use our other overload with the instance name we just grabbed (or the default we set first).
                WriteSample(autoInstanceName, metricData);
            }
        }

        /// <summary>
        /// Write a metric sample to an automatically-determined instance of this metric definition using the provided data object.
        /// </summary>
        /// <remarks>
        /// <para>This overload may only be used if this metric definition was created by EventMetric and EventMetricValue
        /// attributes on a particular Type (class, struct, or interface), and only for userDataObjects of a type assignable
        /// to that bound type for this definition.</para>
        /// <para>The metric instance name will be obtained from the member which was marked with the EventMetricInstanceName
        /// attribute.  If none is bound, the default instance will be used (a null instance name).  The determined metric
        /// instance will be created if it does not already exist.  See the overloads with an instanceName parameter to
        /// specify a particular metric instance name.</para>
        /// </remarks>
        /// <param name="metricData">A data object to sample, compatible with the binding type of this definition.</param>
        /// <exception caption="" cref="ArgumentException">This event metric definition is not bound to sample automatically from a user data object.  CreateSample() and SetValue() must be used to specify the data values directly.&lt;br /&gt;
        /// -or-&lt;br /&gt;
        /// The provided user data object is not assignable to this event metric's bound type and can not be sampled automatically for this metric definition.</exception>
        /// <example>
        /// See the <see cref="EventMetric">EventMetric Class Overview</see> for an example.
        /// </example>
        public void WriteSample(object metricData)
        {
            WriteSample(metricData, null);
        }

        /// <summary>
        /// Use the instance name binding for this definition to query the instance name of a given user data object.
        /// </summary>
        /// <remarks>If not bound, this method returns null.</remarks>
        /// <param name="metricData">A live object instance (does not work on a Type).</param>
        /// <returns>The instance name determined by the binding query.</returns>
        internal string InvokeInstanceNameBinding(object metricData)
        {
            Type userDataType = metricData.GetType();
            if (BoundType.IsAssignableFrom(userDataType) == false)
            {
                return null; // Doesn't match the bound type, can't invoke it.
            }

            // ToDo: Change instance name binding to use NVP (or a new Binding class).
            NameValuePair<MemberTypes> nameBinding = new NameValuePair<MemberTypes>(NameMemberName, NameMemberType);
            BindingFlags bindingFlags = GetBindingFlags(nameBinding);

            string autoInstanceName;
            if (bindingFlags != 0)
            {
                try
                {
                    object rawName = userDataType.InvokeMember(NameMemberName, bindingFlags, null, metricData, null, CultureInfo.InvariantCulture);
                    if (rawName == null || rawName.GetType() == typeof(string))
                        autoInstanceName = rawName as string; // Null, or an actual string.  We're cool with either.
                    else
                        autoInstanceName = rawName.ToString(); // Convert it to a string, because that's what we need.

                    if (autoInstanceName == null)
                        autoInstanceName = string.Empty; // Use this to report a valid "default instance" result.
                }
                catch
                {
                    autoInstanceName = null; // Null reports a failure case.
                }
            }
            else
            {
                autoInstanceName = null; // Something wrong, we couldn't figure out the binding flags, failure case.
            }

            return autoInstanceName;
        }

        internal BindingFlags GetBindingFlags(NameValuePair<MemberTypes> binding)
        {
            if (binding == null || BoundType == null)
                return 0;

            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic |
                                        BindingFlags.Instance | BindingFlags.Static; // | BindingFlags.ExactBinding;
            switch (binding.Value)
            {
                case MemberTypes.Field:
                    bindingFlags |= BindingFlags.GetField;
                    break;
                case MemberTypes.Property:
                    bindingFlags |= BindingFlags.GetProperty;
                    break;
                case MemberTypes.Method:
                    bindingFlags |= BindingFlags.InvokeMethod;
                    break;
                default:
                    // Uh-oh, this should not happen!
                    bindingFlags = 0;
                    break;
            }

            return bindingFlags;
        }

        /// <summary>
        /// Sample every event metric defined by EventMetric and EventMetricValue attributes on the provided data object at any interface or inheritance level.
        /// </summary>
        /// <param name="metricData">A user data object defining event metrics by attributes on itself or its interfaces or any inherited type.</param>
        /// <param name="fallbackInstanceName">The instance name to fall back on if a given definition does not specify an instance name binding (may be null).</param>
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
        public static void Write(object metricData, string fallbackInstanceName)
        {
            EventMetricDefinition[] allDefinitions = RegisterAll(metricData);

            foreach (EventMetricDefinition definition in allDefinitions)
            {
                try
                {
                    definition.WriteSample(metricData, fallbackInstanceName);
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
        /// Sample every event metric defined by EventMetric and EventMetricValue attributes on the provided data object at any interface or inheritance level.
        /// </summary>
        /// <param name="metricData">A user data object defining event metrics by attributes on itself or its interfaces or any inherited type.</param>
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
        /// </example>
        public static void Write(object metricData)
        {
            Write(metricData, null);
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Object Change Locking object.
        /// </summary>
        internal object Lock { get { return m_WrappedDefinition.Lock; } }

        /// <summary>
        /// The internal event metric definition we're wrapping. 
        /// </summary>
        internal Monitor.EventMetricDefinition WrappedDefinition
        {
            get { return m_WrappedDefinition; }
        }

        /// <summary>
        /// The internal metric definition this IMetricDefinition is wrapping.
        /// </summary>
        Monitor.MetricDefinition IMetricDefinition.WrappedDefinition
        {
            get { return m_WrappedDefinition; }
        }

        /// <summary>
        /// Indicates if there is a binding for metric instance name.
        /// </summary>
        /// <remarks>When true, the Name Member Name and Name Member Type properties are available.</remarks>
        internal bool NameBound
        {
            get { return m_WrappedDefinition.NameBound; }
            set { m_WrappedDefinition.NameBound = value; }
        }

        /// <summary>
        /// The name of the member to invoke to determine the metric instance name.
        /// </summary>
        /// <remarks>This property is only valid when NameBound is true.</remarks>
        internal string NameMemberName
        {
            get { return m_WrappedDefinition.NameMemberName; }
            set { m_WrappedDefinition.NameMemberName = value; }
        }

        /// <summary>
        /// The type of the member to be invoked to determine the metric instance name (field, method, or property)
        /// </summary>
        /// <remarks>This property is only valid when NameBound is true.</remarks>
        internal MemberTypes NameMemberType
        {
            get { return m_WrappedDefinition.NameMemberType; }
            set { m_WrappedDefinition.NameMemberType = value; }
        }

        #endregion

        #region IMetricDefinition Members

        /// <summary>
        /// The unique Id of this event metric definition.  This can reliably be used as a key to refer to this item, within the same session which created it.
        /// </summary>
        /// <remarks>The Id is limited to a specific session, and thus identifies a consistent unchanged definition. The
        /// Id can <b>not</b> be used to identify a definition across different sessions, which could have different
        /// actual definitions due to changing user code.  See the Key property to identify a metric definition across
        /// different sessions.</remarks>
        public Guid Id { get { return m_WrappedDefinition.Id; } }

        /// <summary>
        /// The three-part key of the metric definition being captured, as a single string.  
        /// </summary>
        /// <remarks>The Key is the combination of metrics capture system label, category name, and counter name to uniquely
        /// identify a specific metric definition.  It can also identify the same definition across different sessions.</remarks>
        public string Key { get { return m_WrappedDefinition.Name; } }

        /// <summary>
        /// A short display string for this metric definition, suitable for end-user display.
        /// </summary>
        public string Caption
        {
            get { return m_WrappedDefinition.Caption; }
            set { m_WrappedDefinition.Caption = value; }
        }

        /// <summary>
        /// A description of what is tracked by this metric, suitable for end-user display.
        /// </summary>
        public string Description
        {
            get { return m_WrappedDefinition.Description; }
            set { m_WrappedDefinition.Description = value; }
        }

        /// <summary>
        /// The recommended default display interval for graphing. 
        /// </summary>
        public SamplingInterval Interval
        {
            get { return (SamplingInterval)m_WrappedDefinition.Interval; }
        }

        /*
        /// <summary>
        /// The definitions collection that contains this definition.
        /// </summary>
        /// <remarks>This parent pointer should be used when walking from an object back to its parent instead of taking
        /// advantage of the static metrics definition collection to ensure your application works as expected when handling
        /// data that has been loaded from a database or data file.  The static metrics collection is for the metrics being
        /// actively captured in the current process, not for metrics that are being read or manipulated.</remarks>
        internal MetricDefinitionCollection Definitions { get { return s_Definitions; } }
        */

        /// <summary>
        /// The metric capture system label under which this metric definition was created.
        /// </summary>
        /// <remarks>This label distinguish metrics defined and captured by different libraries from each other,
        /// ensuring that metrics defined by different development groups will fall under separate namespaces and not
        /// require category names to be globally unique across third party libraries linked by an application.
        /// Pick your own label which will uniquely identify your library or namespace.</remarks>
        public string MetricsSystem { get { return m_WrappedDefinition.MetricTypeName; } }

        /// <summary>
        /// The category of this metric for display purposes. This can be a period delimited string to represent a variable height hierarchy.
        /// </summary>
        public string CategoryName
        {
            get { return m_WrappedDefinition.CategoryName; }
        }

        /// <summary>
        /// The display name of this metric (unique within the category name).
        /// </summary>
        public string CounterName
        {
            get { return m_WrappedDefinition.CounterName; }
        }

        /// <summary>
        /// The sample type of the metric.  Indicates whether the metric represents discrete events or a continuous value.
        /// </summary>
        Loupe.Extensibility.Data.SampleType IMetricDefinition.SampleType
        {
            get { return m_WrappedDefinition.SampleType; }
        }

        /// <summary>
        /// Indicates whether this event metric definition is now read-only because it has been officially registered and can be used to create event metric instances.
        /// </summary>
        public bool IsReadOnly
        {
            get { return m_WrappedDefinition.IsReadOnly; }
        }

        #endregion

        #region IComparable<IMetricDefinition> Members

        int IComparable<IMetricDefinition>.CompareTo(IMetricDefinition other)
        {
            return WrappedDefinition.CompareTo(other.WrappedDefinition);
        }

        #endregion

        #region IEquatable<IMetricDefinition> Members

        bool IEquatable<IMetricDefinition>.Equals(IMetricDefinition other)
        {
            return WrappedDefinition.Equals(other?.WrappedDefinition);
        }

        #endregion

        #region Public Collection Accessors

        /// <summary>
        /// Determines whether the collection of all metric definitions contains an element with the specified key.
        /// </summary>
        /// <param name="id">The metric definition Id to locate in the collection</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public static bool ContainsKey(Guid id)
        {
            //gateway to our inner dictionary 
            return s_Definitions.ContainsKey(id);
        }

        /// <summary>
        /// Determines whether the collection of all metric definitions contains an element with the specified key.
        /// </summary>
        /// <param name="key">The Key of the event metric definition to check (composed of the metrics system, category name,
        /// and counter name combined as a single string).</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public static bool ContainsKey(string key)
        {
            //protect ourself from a null before we do the trim (or we'll get an odd user the error won't understand)
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            //gateway to our alternate inner dictionary
            return s_Definitions.ContainsKey(key.Trim());
        }

        /// <summary>
        /// Determines whether the collection of all metric definitions contains an element with the specified key.
        /// </summary>
        /// <param name="metricsSystem">The metrics capture system label.</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <returns>true if the collection contains an element with the key; otherwise, false.</returns>
        public static bool ContainsKey(string metricsSystem, string categoryName, string counterName)
        {
            //gateway to our alternate inner dictionary
            return s_Definitions.ContainsKey(metricsSystem, categoryName, counterName);
        }

        /// <summary>
        /// Retrieve an EventMetricDefinition by its Id, if present. (Throws an ArgumentException if the Id resolves to a SampledMetricDefinition instead.)
        /// </summary>
        /// <remarks>This method looks in the collection of registered metric definitions for the specified Id key.  If it
        /// is not found, the output is set to null and the method returns false.  If the Id key is found and resolves to an
        /// EventMetricDefinition, it is stored in the value output parameter and the method returns true.  If the Id key
        /// is found but is not an EventMetricDefinition, an ArgumentException is thrown to signal a usage inconsistency
        /// in your code.</remarks>
        /// <param name="id">The Id of the event metric definition to get.</param>
        /// <param name="value">The output variable to receive the EventMetricDefinition object if found (null if not).</param>
        /// <returns>False if no metric definition is registered with the given Id, true if an EventMetricDefinition is
        /// registered with the given Id, or throws an exception if the registered definition is not an EventMetricDefinition.</returns>
        public static bool TryGetValue(Guid id, out EventMetricDefinition value)
        {
            //gateway to our internal collection TryGetValue()
            bool foundValue = s_Definitions.TryGetValue(id, out var definition);
            value = foundValue ? definition as EventMetricDefinition : null;
            if (foundValue && value == null)
            {
                // Uh-oh, we found one but it didn't resolve to an EventMetricDefinition!
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The metric definition found by Id ({0}) is not an event metric definition.", id));
            }
            return foundValue;
        }

        /// <summary>
        /// Retrieve an EventMetricDefinition by its combined three-part Key string, if present.
        /// </summary>
        /// <remarks>This method looks in the collection of registered metric definitions for the specified Id key.  If it
        /// is not found, the output is set to null and the method returns false.  If the Id key is found and resolves to an
        /// EventMetricDefinition, it is stored in the value output parameter and the method returns true.  If the Id key
        /// is found but is not an EventMetricDefinition, an ArgumentException is thrown to signal a usage inconsistency
        /// in your code.</remarks>
        /// <param name="key">The Key of the event metric definition to get (composed of the metrics system, category name,
        /// and counter name combined as a single string).</param>
        /// <param name="value">The output variable to receive the EventMetricDefinition object if found (null if not).</param>
        /// <returns>False if no metric definition is registered with the given Key, true if an EventMetricDefinition is
        /// registered with the given Key, or throws an exception if the registered definition is not an EventMetricDefinition.</returns>
        public static bool TryGetValue(string key, out EventMetricDefinition value)
        {
            //protect ourself from a null before we do the trim (or we'll get an odd user the error won't understand)
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            //gateway to our inner dictionary try get value
            bool foundValue = s_Definitions.TryGetValue(key.Trim(), out var definition);
            value = foundValue ? definition as EventMetricDefinition : null;
            if (foundValue && value == null)
            {
                // Uh-oh, we found one but it didn't resolve to an EventMetricDefinition!
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The metric definition found by Key \"{0}\" is not an event metric definition.", key));
            }
            return foundValue;
        }

        /// <summary>
        /// Retrieve an EventMetricDefinition by its three key strings (metrics system, category name, and counter name), if present.
        /// </summary>
        /// <remarks>This method looks in the collection of registered metric definitions for the specified Id key.  If it
        /// is not found, the output is set to null and the method returns false.  If the Id key is found and resolves to an
        /// EventMetricDefinition, it is stored in the value output parameter and the method returns true.  If the Id key
        /// is found but is not an EventMetricDefinition, an ArgumentException is thrown to signal a usage inconsistency
        /// in your code.</remarks>
        /// <param name="metricsSystem">The metrics capture system label of the definition to look up.</param>
        /// <param name="categoryName">The name of the category with which the definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="value">The output variable to receive the EventMetricDefinition object if found (null if not).</param>
        /// <returns>False if no metric definition is registered with the given Key, true if an EventMetricDefinition is
        /// registered with the given Key, or throws an exception if the registered definition is not an EventMetricDefinition.</returns>
        public static bool TryGetValue(string metricsSystem, string categoryName, string counterName, out EventMetricDefinition value)
        {
            //gateway to our inner dictionary try get value
            bool foundValue = s_Definitions.TryGetValue(metricsSystem, categoryName, counterName, out var definition);
            value = foundValue ? definition as EventMetricDefinition : null;
            if (foundValue && value == null)
            {
                // Uh-oh, we found one but it didn't resolve to an EventMetricDefinition!
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The metric definition found by metrics system ({0}) category name ({1}) counter name ({2}) is not an event metric definition.",
                                                          metricsSystem, categoryName, counterName));
            }
            return foundValue;
        }

        /// <summary>
        /// Find an existing event metric definition previously registered via EventMetric and EventMetricValue attributes on a specific Type.
        /// </summary>
        /// <remarks>This method overload can obtain a previously registered EventMetricDefinition created through
        /// EventMetric and EventMetricValue attributes, by specifying the Type containing those attributes.  If the
        /// specified Type does not have an EventMetric attribute defined, or if the Type has an EventMetric attribute but
        /// has not been registered (e.g. by a call to EventMetricDefinition.Register(userObjectType)), then false is
        /// returned (with out value set to null).  If an event metric defined by attributes on that Type has been
        /// successfully registered, then true is returned (with the registered EventMetricDefinition stored in the out
        /// value).  If the metric definition found by the 3-part Key used in the EventMetric attribute is not an event
        /// metric (e.g. a sampled metric definition was registered with that Key), then an ArgumentException is thrown to
        /// signal your programming mistake.  Inheritance and interfaces will <b>not</b> be searched, so the specified Type
        /// must directly define an event metric, but valid objects of a type assignable to the specified bound Type of
        /// this definition <b>can</b> be sampled from the specific event metric definition found.</remarks>
        /// <param name="metricDataObjectType">A specific Type with attributes defining an event metric.</param>
        /// <param name="value">The output variable to receive the EventMetricDefinition object if found (null if not).</param>
        /// <returns>False if no EventMetric attribute is found on the specified Type, or if no metric definition is
        /// registered with the 3-part Key found in that attribute, true if an EventMetricDefinition is registered with
        /// the given Key, or throws an exception if the registered definition found is not an EventMetricDefinition.</returns>
        public static bool TryGetValue(Type metricDataObjectType, out EventMetricDefinition value)
        {
            if (metricDataObjectType == null)
            {
                value = null;
                throw new ArgumentNullException(nameof(metricDataObjectType));
            }

            bool foundValue;
            // We shouldn't need a lock here because we aren't changing the dictionary, just doing a single read check.
            lock (s_DictionaryLock) // But apparently Dictionary may not be internally threadsafe, so we do need our lock.
            {
                foundValue = s_DefinitionMap.TryGetValue(metricDataObjectType, out value); // Fast lookup, for efficiency.
            }

            // We have to check for a possible null in the map, meaning we've seen that Type but it couldn't register it.
            // We'll treat a null as a not-found case, and look for the attribute.
            if (foundValue == false || value == null)
            {
                EventMetricAttribute eventMetricAttribute = null;
                if (metricDataObjectType.IsDefined(typeof(EventMetricAttribute), false))
                {
                    eventMetricAttribute =
                        metricDataObjectType.GetTypeInfo().GetCustomAttribute<EventMetricAttribute>();
                }

                if (eventMetricAttribute != null)
                {
                    string metricsSystem = eventMetricAttribute.MetricsSystem;
                    string categoryName = eventMetricAttribute.MetricCategoryName;
                    string counterName = eventMetricAttribute.CounterName;

                    //gateway to our inner dictionary try get value
                    foundValue = s_Definitions.TryGetValue(metricsSystem, categoryName, counterName, out var definition);
                    value = foundValue ? definition as EventMetricDefinition : null;
                    if (foundValue && value == null)
                    {
                        // Uh-oh, we found one but it didn't resolve to an EventMetricDefinition!
                        throw new ArgumentException(
                            string.Format(CultureInfo.InvariantCulture, 
                                "The metric definition registered for metrics system ({0}) category name ({1}) counter name ({2}) specified in EventMetric attribute on {3} is not an event metric definition.",
                                metricsSystem, categoryName, counterName, metricDataObjectType.Name));
                    }
                }
                else
                {
                    foundValue = false;
                    value = null;
                }
            }
            // else we found a valid definition in our Type-to-definition map, so we've output that and report success.

            return foundValue;
        }

        #endregion
    }
}
