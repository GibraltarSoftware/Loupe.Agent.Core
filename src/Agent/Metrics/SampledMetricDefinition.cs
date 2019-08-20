using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Loupe.Agent.Metrics.Internal;
using Loupe.Core;
using Loupe.Metrics;

namespace Loupe.Agent.Metrics
{

    /// <summary>
    /// The definition of a user-defined sampled metric
    /// </summary>
    /// <remarks>Custom Sampled Metrics are the simplest form of Sampled Metrics, not requiring the developer
    /// to derive their own classes to encapsulate a sampled metric.  Review if this class can serve your needs before
    /// you create your own custom set of classes derived from SampledMetric (or derive from this class)</remarks>
    public sealed class SampledMetricDefinition : IMetricDefinition, IComparable<SampledMetricDefinition>, IEquatable<SampledMetricDefinition>
    {
        private readonly Core.Monitor.CustomSampledMetricDefinition m_WrappedDefinition;
        private readonly SampledMetricCollection m_Metrics;

        private static readonly Type[] s_SupportedDataTypes = new[]
        {
            typeof (double), typeof (float), typeof (decimal), typeof (long), typeof (ulong), typeof (int), typeof (uint),
            typeof (short), typeof (ushort), typeof (DateTime), typeof (DateTimeOffset), typeof (TimeSpan)
        };

        private static readonly MetricDefinitionCollection s_Definitions = Log.MetricDefinitions;
        private static readonly Dictionary<Type, bool> s_DataTypeSupported = InitTypeSupportedDictionary();
        private static readonly Dictionary<Type, Type[]> s_InheritanceMap = new Dictionary<Type, Type[]>(); // Array of all inherited types (that have attributes), by type.
        private static readonly Dictionary<Type, List<SampledMetricDefinition>> s_DefinitionsMap =
            new Dictionary<Type, List<SampledMetricDefinition>>(); // LOCKED List of definitions by specific bound type.
        private static readonly object s_DictionaryLock = new object(); // Lock for the DefinitionMap dictionary.

        private static Dictionary<Type, bool> InitTypeSupportedDictionary()
        {
            // We need to initialize our type-supported dictionary up front....
            Dictionary<Type, bool> dataTypeSupported = new Dictionary<Type, bool>(s_SupportedDataTypes.Length);
            foreach (Type type in s_SupportedDataTypes)
            {
                dataTypeSupported[type] = true;
            }
            return dataTypeSupported;
        }

        /// <summary>
        /// Create a new metric definition for the active log.
        /// </summary>
        /// <remarks>At any one time there should only be one metric definition with a given combination of 
        /// metric type, category, and counter name.  These values together are used to correlate metrics
        /// between sessions.  The metric definition will automatically be added to the provided collection.</remarks>
        /// <param name="metricsSystem">The metrics capture system label.</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="samplingType">The type of data captured for each metric under this definition.</param>
        /// <param name="unitCaption">A displayable caption for the units this metric's values represent, or null for unit-less values.</param>
        /// <param name="metricCaption">A displayable caption for this sampled metric counter.</param>
        /// <param name="description">A description of what is tracked by this metric, suitable for end-user display.</param>
        internal SampledMetricDefinition(string metricsSystem, string categoryName, string counterName, SamplingType samplingType, string unitCaption, string metricCaption, string description)
            : this(new Core.Monitor.CustomSampledMetricDefinition(metricsSystem, categoryName, counterName, samplingType, unitCaption, description))
        {
            m_WrappedDefinition.Caption = metricCaption; // ToDo: Add metricCaption parameter to Monitor.CSMD constructor.
        }

        /// <summary>
        /// Create a new API custom sampled metric object from the provided API metric definition collection and internal custom sampled metric definition.
        /// </summary>
        /// <remarks>The metric definition will automatically be added to the provided collection.</remarks>
        /// <param name="metricDefinition">The internal custom sampled metric definition to wrap.</param>
        internal SampledMetricDefinition(Core.Monitor.CustomSampledMetricDefinition metricDefinition)
        {
            m_WrappedDefinition = metricDefinition;
            m_Metrics = new SampledMetricCollection(this);

            // ToDo: Determine whether to keep this behavior for SampledMetrics or change to the EventMetric model with Register().
            // The internal definition automatically added itself to the internal collection of definitions.
            // Now we have to add this specific API definition to the collection of API definitions.
            // But we can't just Add(this) because that would try to add it internally, too.  So instead,
            // We just want to set up the mapping to externalize the internal definition to point to us.
            s_Definitions.Internalize(this);
        }

        #region Public Properties and Methods

        /// <summary>
        /// Find or create a sampled metric definition from the specified parameters.
        /// </summary>
        /// <param name="metricsSystem">The metrics capture system label.</param>
        /// <param name="categoryName">The name of the category with which this definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="samplingType">The sampling type of the sampled metric counter.</param>
        /// <param name="unitCaption">A displayable caption for the units this metric's values represent, or null for unit-less values.</param>
        /// <param name="metricCaption">A displayable caption for this sampled metric counter.</param>
        /// <param name="description">An extended end-user description of this sampled metric counter.</param>
        /// <remarks>If a metric definition does not already exist for the specified metrics system, category name, and
        /// counter name, a new sampled metric definition will be created from the provided parameters.  If one already
        /// exists then it will be checked for compatibility.  A sampled metric defined with the same SamplingType will
        /// be considered compatible, otherwise an ArgumentException will be thrown.  Each distinct metric definition (all
        /// sampled metrics and event metrics) must have a distinct Key (the metrics system, category, and counter name).
        /// Multiple metric instances can then be created (each with its own instance name) from the same metric definition.
        /// </remarks>
        /// <exception caption="" cref="ArgumentNullException">The provided metricsSystem, categoryName, or counterName was null.</exception>
        /// <exception caption="" cref="ArgumentException">There is already a metric definition for the same key, but it is not a sampled metric.&lt;br /&gt;-or-&lt;br /&gt;
        /// There is already a sampled metric definition for the same key but it uses an incompatible sampling type.</exception>
        public static SampledMetricDefinition Register(string metricsSystem, string categoryName, string counterName, SamplingType samplingType,
                                                       string unitCaption, string metricCaption, string description)
        {
            SampledMetricDefinition officialDefinition;
            bool newCreation;

            // We need to lock the collection while we check for an existing definition and maybe add a new one to it.
            lock (Log.MetricDefinitions.Lock)
            {
                if (Log.MetricDefinitions.TryGetValue(metricsSystem, categoryName, counterName, out var rawDefinition) == false)
                {
                    // There isn't already one by that Key.  Great!  Make one from our parameters.
                    newCreation = true;
                    officialDefinition = new SampledMetricDefinition(metricsSystem, categoryName, counterName, samplingType,
                                                                     unitCaption, metricCaption, description);
                }
                else
                {
                    // Oooh, we found one already registered.  We'll want to do some checking on this, but outside the lock.
                    newCreation = false;
                    officialDefinition = rawDefinition as SampledMetricDefinition;
                }
            } // End of collection lock

            if (officialDefinition == null)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, 
                        "There is already a metric definition for the same metrics system ({0}), category name ({1}), and counter name ({2}), but it is not a sampled metric.",
                        metricsSystem, categoryName, counterName));
            }
            else if (newCreation == false)
            {
                // There was one other than us, make sure it's compatible with us.  Just check SamplingType.
                if (officialDefinition.SamplingType != samplingType)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, 
                            "There is already a sampled metric definition for the same metrics system ({0}), category name ({1}), and counter name ({2}), but it is not compatible; " +
                            "it defines sampling type as {3} rather than {4}.",
                            metricsSystem, categoryName, counterName, officialDefinition.SamplingType, samplingType));
                }

                // If the SamplingType matches, then we're okay to return the official one.
            }
            // Otherwise, we just made this one, so we're all good.

            return officialDefinition;
        }

        /// <summary>
        /// Find or create multiple sampled metrics definitions (defined via SampledMetric attributes) for the provided object or Type.
        /// </summary>
        /// <remarks>The provided Type or the GetType() of the provided object instance will be scanned for SampledMetric
        /// attributes on itself and any of its interfaces to identify a list of sampled metrics defined for instances of
        /// that type, creating them as necessary by scanning its members for SampledMetricValue attributes.  Inheritance
        /// will be followed into base types, along with all interfaces inherited to the top level.  This method will not
        /// throw exceptions, so a null argument will return an empty array, as will an argument which does not define any
        /// valid sampled metrics.  Also see AddOrGet() to find or create sampled metrics definitions for a specific
        /// Type, without digging into inheritance or interfaces of that Type.</remarks>
        /// <param name="userInstanceObject">A Type or an instance defining sampled metrics by attributes on itself and/or its interfaces and/or inherited types.</param>
        /// <returns>An array of zero or more sampled metric definitions found for the provided object or Type.</returns>
        /// <exception cref="ArgumentException">The specified Type does not have a SampledMetric attribute, so it can't be used to define sampled metrics.<br />
        /// - or -<br />
        /// The specified Type does not have a usable SampledMetric attribute, so it can't be used to define sampled metrics.<br />
        /// - or -<br />
        /// The specified Type's SampledMetric attribute has an empty metric namespace which is not allowed, so no metrics can be defined under it.<br />
        /// - or -<br />
        /// The specified Type's SampledMetric attribute has an empty metric category name which is not allowed, so no metrics can be defined under it.</exception>
        internal static SampledMetricDefinition[] RegisterAll(object userInstanceObject)
        {
            List<SampledMetricDefinition> definitions = new List<SampledMetricDefinition>();

            if (userInstanceObject != null)
            {
                // Either they gave us a Type, or we need to get the type of the object instance they gave us.
                Type userObjectType = (userInstanceObject as Type) ?? userInstanceObject.GetType();

                SampledMetricDefinition[] metricDefinitions;
                Type[] inheritanceArray;
                bool foundIt;
                lock (s_InheritanceMap) // Apparently Dictionaries are not internally threadsafe.
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
                            metricDefinitions = RegisterGroup(inheritedType, null);
                        }
                        catch
                        {
                            metricDefinitions = null;
                        }
                        if (metricDefinitions != null)
                            definitions.AddRange(metricDefinitions); // Add them to the list if found.
                    }
                }
                else
                {
                    // New top-level type, we have to scan its inheritance.
                    List<Type> inheritanceList = new List<Type>(); // List of all the inherited types we find with attributes on them.

                    // First, see if the main type they gave us defines a sampled metric group (metricsSystem and categoryName).
                    if (userObjectType.IsDefined(typeof (SampledMetricAttribute), false))
                    {
                        try
                        {
                            inheritanceList.Add(userObjectType); // Add the top level Type to our list of types.
                            metricDefinitions = RegisterGroup(userObjectType, null);
                        }
                        catch
                        {
                            metricDefinitions = null;
                        }
                        if (metricDefinitions != null)
                            definitions.AddRange(metricDefinitions); // Add them to the list if found.
                    }

                    // Now check all of its interfaces.
                    Type[] interfaces = userObjectType.GetInterfaces();
                    foreach (Type interfc in interfaces)
                    {
                        if (interfc.IsDefined(typeof (SampledMetricAttribute), false))
                        {
                            // We found an interface with the right Attribute, get its group of definitions.
                            try
                            {
                                inheritanceList.Add(interfc); // Add the interface to our list of types.
                                metricDefinitions = RegisterGroup(interfc, null);
                            }
                            catch
                            {
                                metricDefinitions = null;
                            }
                            if (metricDefinitions != null)
                                definitions.AddRange(metricDefinitions); // Add them to the list if found.
                        }
                    }

                    // And finally, drill down it's inheritance... unless it's an interface (which will have a null base type).
                    Type baseObjectType = userObjectType.GetTypeInfo().BaseType;

                    // The IsInterface check shouldn't be needed, but just in case, we want to be sure we don't cause a duplicate.
                    while (baseObjectType != null && baseObjectType != typeof (object) && baseObjectType.GetTypeInfo().IsInterface == false)
                    {
                        // See if an ancestor Type defines a group of sampled metrics.
                        if (baseObjectType.IsDefined(typeof (SampledMetricAttribute), false))
                        {
                            try
                            {
                                inheritanceList.Add(baseObjectType); // Add the inherited base to our list of types.
                                metricDefinitions = RegisterGroup(baseObjectType, null);
                            }
                            catch
                            {
                                metricDefinitions = null;
                            }
                            if (metricDefinitions != null)
                                definitions.AddRange(metricDefinitions); // Add them to the list if found.
                        }

                        // No need to check its interfaces, we already got all of them from the top level.

                        baseObjectType = baseObjectType.GetTypeInfo().BaseType; // Get the next deeper Type.
                    }

                    // Now, remember the list of attributed types we found in this walk.
                    lock (s_InheritanceMap) // Apparently Dictionaries are not internally threadsafe.
                    {
                        s_InheritanceMap[userObjectType] = inheritanceList.ToArray();
                    }
                }
            }

            // And finally, return the full list of definitions we found.
            // If they gave us a null, we'll just return an empty array.
            return definitions.ToArray();
        }

        /// <summary>
        /// Find or create sampled metric definitions from SampledMetric and SampledMetricValue attributes on a specific Type.
        /// </summary>
        /// <remarks>The provided type must have a SampledMetric attribute and can have one or more fields, properties
        /// or zero-argument methods with SampledMetricValue attributes defined.  This method creates metric definitions
        /// but does not create specific metric instances, so it does not require a live object.  If the sampled metric
        /// definition already exists, it is just returned and no exception is thrown.  If the provided type is not suitable
        /// to create sampled metrics from because it is missing the appropriate attributes or the attributes have been
        /// miss-defined, an ArgumentException will be thrown.  Inheritance and interfaces will <b>not</b> be searched, so
        /// the provided Type must directly define sampled metrics, but valid objects of a type assignable to the specified
        /// bound Type of this definition <b>can</b> be sampled from these specific sampled metric definitions.  Also see
        /// AddOrGetDefinitions() to find and return all definitions in the inheritance chain of a type or object.</remarks>
        /// <param name="userObjectType">A specific Type with attributes defining a group of sampled metrics.</param>
        /// <returns>The group of sampled metric definitions (as an array) determined by attributes on the given Type.</returns>
        /// <exception cref="ArgumentNullException">The userObjectType was null</exception>
        /// <exception cref="ArgumentException">The specified Type does not have a SampledMetric attribute, so it can't be used to define sampled metrics.<br />
        /// - or -<br />
        /// The specified Type does not have a usable SampledMetric attribute, so it can't be used to define sampled metrics.<br />
        /// - or -<br />
        /// The specified Type's SampledMetric attribute has an empty metric namespace which is not allowed, so no metrics can be defined under it.<br />
        /// - or -<br />
        /// The specified Type's SampledMetric attribute has an empty metric category name which is not allowed, so no metrics can be defined under it.</exception>
        internal static SampledMetricDefinition[] RegisterType(Type userObjectType)
        {
            if (userObjectType == null)
            {
                throw new ArgumentNullException(nameof(userObjectType), "A valid Type must be provided.");
            }
            return RegisterGroup(userObjectType, string.Empty); // Search single Type only, no inheritance.
        }

        /// <summary>
        /// Find or create a single sampled metric definition (by counter name) from SampledMetric and SampledMetricValue attributes on a specific Type.
        /// </summary>
        /// <remarks>The provided type must have a SampledMetric attribute and can have one or more fields, properties
        /// or zero-argument methods with SampledMetricValue attributes defined.  This method creates a metric definition
        /// but does not create a specific metric instance, so it does not require a live object.  If the sampled metric
        /// definition already exists, it is just returned and no exception is thrown.  If the provided type is not suitable
        /// to create an sampled metric from because it is missing the appropriate attribute or the attribute has been
        /// miss-defined, an ArgumentException will be thrown.  Inheritance and interfaces will <b>not</b> be searched, so
        /// the provided Type must directly define an sampled metric, but valid objects of a type assignable to the specified
        /// bound Type of this definition <b>can</b> be sampled from this specific sampled metric definition.  Also see
        /// AddOrGetDefinitions() to find and return an array of definitions.</remarks>
        /// <param name="userObjectType">A specific Type with attributes defining sampled metrics including the specified counter name.</param>
        /// <param name="counterName">The counterName of a specific sampled metric to find or create under the SampledMetric attribute on the specified Type.</param>
        /// <returns>The single sampled metric definition selected by counter name and determined by attributes on the given Type.</returns>
        /// <exception cref="ArgumentException">The specified Type does not have a SampledMetric attribute, so it can't be used to define sampled metrics.<br />
        /// - or -<br />
        /// The specified Type does not have a usable SampledMetric attribute, so it can't be used to define sampled metrics.<br />
        /// - or -<br />
        /// The specified Type's SampledMetric attribute has an empty metric namespace which is not allowed, so no metrics can be defined under it.<br />
        /// - or -<br />
        /// The specified Type's SampledMetric attribute has an empty metric category name which is not allowed, so no metrics can be defined under it.</exception>
        internal static SampledMetricDefinition Register(Type userObjectType, string counterName)
        {
            if (userObjectType == null)
            {
                throw new ArgumentNullException(nameof(userObjectType), "A valid Type must be provided.");
            }
            if (string.IsNullOrEmpty(counterName))
            {
                throw new ArgumentNullException(nameof(counterName), "A valid counter name must be specified to select a single sampled metric definition, or use the overload without a counterName parameter.");
            }

            SampledMetricDefinition[] definitions = RegisterGroup(userObjectType, counterName); // Specific counter.
            return (definitions.Length > 0) ? definitions[0] : null;
        }

        /*
        /// <summary>
        /// Find or create multiple sampled metrics definitions (defined via SampledMetric attributes) for the provided object or Type.
        /// </summary>
        /// <remarks>The provided Type or the GetType() of the provided object instance will be scanned for SampledMetric
        /// attributes on itself and any of its interfaces to identify a list of sampled metrics defined for instances of
        /// that type, creating them as necessary by scanning its members for SampledMetricValue attributes.  Inheritance
        /// will be followed into base types, along with all interfaces inherited to the top level.  This method may throw
        /// exceptions, so a null argument will return an empty array, as will an argument which does not define any
        /// valid sampled metrics.</remarks>
        */

        /// <summary>
        /// Find or create sampled metric definition from SampledMetric and SampledMetricValue attributes on a specific Type.
        /// </summary>
        /// <remarks>The provided type must have a SampledMetric attribute and can have one or more fields, properties
        /// or zero-argument methods with SampledMetricValue attributes defined.  This method creates a metric definition
        /// but does not create a specific metric instance, so it does not require a live object.  If the sampled metric
        /// definition already exists, it is just returned and no exception is thrown.  If the provided type is not suitable
        /// to create sampled metrics from because it is missing the appropriate attributes or the attributes have been
        /// miss-defined, an ArgumentException will be thrown.  Inheritance and interfaces will <b>not</b> be searched, so
        /// the provided Type must directly define an sampled metric, but valid objects of a type assignable to the specified
        /// bound Type of this definition <b>can</b> be sampled from these specific sampled metric definitions.</remarks>
        /// <param name="userObjectType">A specific Type with attributes defining an sampled metric.</param>
        /// <param name="counterName">The counterName of a specific sampled metric to find or create, string.Empty to return the entire group of sampled metric definitions unless there are errors, or null to swallow errors.</param>
        /// <returns>The single sampled metric definition determined by attributes on the given Type.</returns>
        /// <exception cref="ArgumentException">The specified Type does not have a SampledMetric attribute, so it can't be used to define sampled metrics.<br />
        /// - or -<br />
        /// The specified Type does not have a usable SampledMetric attribute, so it can't be used to define sampled metrics.<br />
        /// - or -<br />
        /// The specified Type's SampledMetric attribute has an empty metric namespace which is not allowed, so no metrics can be defined under it.<br />
        /// - or -<br />
        /// The specified Type's SampledMetric attribute has an empty metric category name which is not allowed, so no metrics can be defined under it.</exception>
        internal static SampledMetricDefinition[] RegisterGroup(Type userObjectType, string counterName)
        {
            List<SampledMetricDefinition> definitions;
            if (userObjectType == null)
            {
                return new SampledMetricDefinition[0]; // Return an empty array; This is already checked in the public wrappers.
            }

            // Check if we've scanned this specific Type before.  We need the lock...
            lock (s_DictionaryLock)
            {
                if (s_DefinitionsMap.TryGetValue(userObjectType, out definitions) == false || definitions == null)
                {
                    // We haven't scanned this Type before, start a new list.
                    definitions = new List<SampledMetricDefinition>();

                    // In this internal catch-all, counterName may be empty or null or a specific counter name.
                    // All errors must be swallowed (but logged) if counterName is null.

                    // Check if it defines a group at this specific level, no inheritance search, no interfaces search.
                    if (userObjectType.IsDefined(typeof (SampledMetricAttribute), false) == false)
                    {
                        if (counterName == null)
                        {
                            return definitions.ToArray(); // Swallow all errors.  Return empty array.
                        }
                        // Sorry, Attribute not found
                        throw new ArgumentException(
                            "The specified Type does not have a SampledMetric attribute, so it can't be used to define sampled metrics.",
                            nameof(userObjectType));
                    }

                    // OK, now waltz off and get the attribute we want.
                    SampledMetricAttribute sampledMetricAttribute = userObjectType.GetTypeInfo().GetCustomAttribute<SampledMetricAttribute>();

                    // Verify that the sampled metric attribute that we got is valid
                    if (sampledMetricAttribute == null)
                    {
                        if (counterName == null)
                        {
                            return definitions.ToArray(); // Swallow all errors.  Return empty array.
                        }
                        throw new ArgumentException(
                            "The specified Type does not have a usable SampledMetric attribute, so it can't be used to define sampled metrics.",
                            nameof(userObjectType));
                    }

                    //make sure the user didn't do any extraordinary funny business
                    string metricsSystem = sampledMetricAttribute.MetricsSystem;
                    if (string.IsNullOrEmpty(metricsSystem))
                    {
                        if (counterName == null)
                        {
                            return definitions.ToArray(); // Swallow all errors.  Return empty array.
                        }
                        throw new ArgumentException(
                            "The specified Type's SampledMetric attribute has an empty metric namespace which is not allowed, so no metrics can be defined under it.");
                    }

                    string metricCategoryName = sampledMetricAttribute.MetricCategoryName;
                    if (string.IsNullOrEmpty(metricCategoryName))
                    {
                        if (counterName == null)
                        {
                            return definitions.ToArray(); // Swallow all errors.  Return empty array.
                        }
                        throw new ArgumentException(
                            "The specified Type's SampledMetric attribute has an empty metric category name which is not allowed, so no metrics can be defined under it.");
                    }

                    bool TypeFilterName(MemberInfo info, object criteria) => true;

                    // Now reflect all of the field/property/methods in the type so we can inspect them for attributes.
                    MemberInfo[] members = userObjectType.GetTypeInfo().FindMembers(MemberTypes.Field | MemberTypes.Method | MemberTypes.Property,
                                                                      BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                                                      BindingFlags.Instance, TypeFilterName, "*");

                    // These will apply to every sampled metric in the logical group on this Type.
                    NameValuePair<MemberTypes> instanceNameBinding = null;

                    // We need to collect the mapping of divisors for later.
                    Dictionary<string, MemberInfo> divisors = new Dictionary<string, MemberInfo>();

                    foreach (MemberInfo curMember in members)
                    {
                        //and what can we get from our little friend?
                        if (curMember.IsDefined(typeof (SampledMetricInstanceNameAttribute), false))
                        {
                            //have we already bound our name?
                            if (instanceNameBinding != null)
                            {
                                //yes, so report a duplicate name warning
                            }
                            else
                            {
                                //nope, we're good, so remember our binding information
                                instanceNameBinding = new NameValuePair<MemberTypes>(curMember.Name, curMember.MemberType);
                            }
                        }

                        if (curMember.IsDefined(typeof (SampledMetricDivisorAttribute), false))
                        {
                            //they have at least one sampled metric divisor attribute, go get all of them
                            //we get back an array of objects just in case there are any non-CLS compliant attributes defined, which there never are.
                            var curMemberValueAttributes =
                                curMember.GetCustomAttributes<SampledMetricDivisorAttribute>();

                            foreach (var curDivisorAttribute in curMemberValueAttributes)
                            {
                                //cast it and test
                                if (curDivisorAttribute != null)
                                {
                                    string divisorCounterName = curDivisorAttribute.CounterName;
                                    if (string.IsNullOrEmpty(divisorCounterName))
                                    {
                                    }
                                    else if (Log.MetricDefinitions.TryGetValue(metricsSystem, metricCategoryName, divisorCounterName,
                                                                               out var existingMetricDefinition))
                                    {
                                        SampledMetricDefinition sampledMetricDefinition = existingMetricDefinition as SampledMetricDefinition;
                                        if (sampledMetricDefinition == null)
                                        {
                                            // Uh-oh, the definition already exists, but it isn't a sampled metric.
                                            // This is only a warning because this attribute just gets ignored by the existing metric,
                                            // And they'll get a real error below if there's actually a SampledMetricValue attribute for this counter name.
                                        }
                                        else if (sampledMetricDefinition.IsBound == false || sampledMetricDefinition.BoundType != userObjectType)
                                        {
                                            // Uh-oh, the definition already exists, but it isn't bound to this Type!
                                        }
                                        else if (BindingMatchesMember(sampledMetricDefinition.DivisorBinding, curMember) == false)
                                        {
                                            // Uh-oh, the definition already exists, but it isn't bound to this member!
                                        }

                                        // Otherwise, the existing definition is correctly bound to this divisor already.  We're good here.
                                        // If it's already bound, we must have scanned this before and this was the first matching
                                        // divisor attribute for the counter name, so mark us as the match.
                                        divisors[divisorCounterName] = curMember; // Remember this member for later.
                                    }
                                    else if (divisors.ContainsKey(divisorCounterName))
                                    {
                                        // They already had one with that counter name!
                                    }
                                    else
                                    {
                                        divisors[divisorCounterName] = curMember; // Remember this member for later.
                                    }
                                }
                            }
                        }
                    }

                    // We had to scan every member first for the instance name so we could bind it in every sampled metric.
                    // Otherwise we would have problems if it wasn't found before the sampled metrics.  Now go back and scan again.

                    foreach (MemberInfo curMember in members)
                    {

                        // Look for SampledMetricValue attributes to actually defined sampled metric counters.
                        if (curMember.IsDefined(typeof (SampledMetricValueAttribute), false))
                        {
                            // What type of value does this member give?  It'll be the same for every value attribute on it!
                            Type curType = GetTypeOfMember(curMember);

                            //they have at least one sampled metric value attribute, go get all of them
                            //we get back an array of objects just in case there are any non-CLS compliant attributes defined, which there never are.
                            var curMemberValueAttributes = curMember.GetCustomAttributes<SampledMetricValueAttribute>();

                            foreach (var curValueAttribute in curMemberValueAttributes)
                            {
                                //cast it and test
                                if (curValueAttribute != null)
                                {
                                    //apply defaults (because this is the only place to get the name of the marked member)
                                    string metricCounterName = curValueAttribute.CounterName;
                                    if (string.IsNullOrEmpty(metricCounterName))
                                    {
                                        curValueAttribute.SetCounterName(curMember.Name);
                                        metricCounterName = curValueAttribute.CounterName;
                                    }

                                    // First time we've seen this Type, scan the whole thing even if they only wanted one.

                                    SamplingType samplingType = curValueAttribute.SamplingType;
                                    //We use a lock because we need to have the check and the add (which happens as part of the new metric definition) happen as a single event.
                                    object metricDefinitionsLock = Log.MetricDefinitions.Lock;
                                    lock (metricDefinitionsLock)
                                    {
                                        //System.Threading.Monitor.Enter(metricDefinitionsLock);

                                        if (Log.MetricDefinitions.TryGetValue(metricsSystem, metricCategoryName, metricCounterName,
                                                                              out var rawMetricDefinition))
                                        {
                                            SampledMetricDefinition sampledMetricDefinition = rawMetricDefinition as SampledMetricDefinition;
                                            if (sampledMetricDefinition == null)
                                            {
                                                // Uh-oh, the definition already exists, but it isn't a sampled metric!
                                            }
                                            else if (sampledMetricDefinition.IsBound == false ||
                                                     sampledMetricDefinition.BoundType != userObjectType)
                                            {
                                                // Uh-oh, the definition already exists, but it isn't bound to this Type!
                                            }
                                            else if (BindingMatchesMember(sampledMetricDefinition.DataBinding, curMember) == false)
                                            {
                                                // Uh-oh, the definition already exists, but it isn't bound to this member!
                                            }
                                            else
                                            {
                                                definitions.Add(sampledMetricDefinition); // Found one! Add it to our list.
                                            }
                                        }
                                        else if (curType == null) // Warn about an unreadable property.
                                        {
                                        }
                                        else if (curType == typeof (void)) // Warn about a void method.
                                        {
                                        }
                                        else if (IsValidDataType(curType) == false)
                                        {
                                        }
                                        else
                                        {
                                            if (divisors.TryGetValue(metricCounterName, out var divisorInfo))
                                            {
                                                // We found a divisor attribute for this counter name earlier.
                                                // Does this counter actually need one?
                                                if (RequiresDivisor(samplingType) == false)
                                                {
                                                    // It doesn't use one.  Warn the user.
                                                    divisorInfo = null;
                                                }

                                                // Otherwise, we leave divisorInfo valid, so we bind it below.
                                            }
                                            else
                                            {
                                                divisorInfo = null; // Didn't find any, make sure it's marked invalid.
                                                if (RequiresDivisor(samplingType))
                                                {
                                                    // Uh-oh!  We need a divisor but they didn't specify one.
                                                }
                                            }

                                            // Now that we have the info for a new sampled metric definition
                                            // and passed all the checks, we need to create it.
                                            SampledMetricDefinition newMetricDefinition =
                                                new SampledMetricDefinition(metricsSystem, metricCategoryName, metricCounterName,
                                                                            samplingType, curValueAttribute.UnitCaption,
                                                                            curValueAttribute.Caption, curValueAttribute.Description)
                                                    {
                                                        BoundType = userObjectType,
                                                        IsBound = true,
                                                        // This is a bound definition.
                                                    };

                                            newMetricDefinition.SetDataBinding(curMember);

                                            if (divisorInfo != null)
                                            {
                                                newMetricDefinition.SetDivisorBinding(divisorInfo);
                                            }

                                            if (instanceNameBinding != null)
                                            {
                                                // ToDo: Push NVP binding handle through for instanceName binding instead of separate properties.
                                                newMetricDefinition.NameMemberType = instanceNameBinding.Value;
                                                newMetricDefinition.NameMemberName = instanceNameBinding.Name;
                                                newMetricDefinition.NameBound = true;
                                            }

                                            // ToDo: Set it read-only and add to collection, following EventMetric model?

                                            definitions.Add(newMetricDefinition); // Add it to our list.
                                        }
                                    } // end of metric definitions lock

                                } // end check that this SampledMetricValue attribute is valid
                            } // end foreach loop over SampledMetricValue attributes on a given member
                        } // end check for SampledMetricValue attribute

                    } // end of foreach loop over members

                    // Now we need to remember this list of definitions, to save time on future lookups.
                    s_DefinitionsMap[userObjectType] = definitions;
                } // end of definition map dictionary-lookup-failed.

                // If we found an entry, definitions was set for us by the TryGetValue(), so we're good.

            } // end of dictionary lock

            // We scanned for all of them (the first time), but did they ask for just one?
            if (string.IsNullOrEmpty(counterName) == false)
            {
                SampledMetricDefinition[] mappedDefinitions = definitions.ToArray(); // Save a snapshot to loop over...
                definitions = new List<SampledMetricDefinition>(); // Now reset the list; they only want one.
                foreach (SampledMetricDefinition definition in mappedDefinitions)
                {
                    if (definition.CounterName == counterName)
                    {
                        definitions.Add(definition); // Add the right one to the list.
                    }
                }
            }

            return definitions.ToArray(); // Return a copy of what we found (they can't change our internal cached list).
        }

        /// <summary>
        /// Checks the provided Type against the list of recognized numeric types and special types supported for SampledMetric data.
        /// </summary>
        /// <remarks>Sampled metrics require inherently numeric samples, so only data with a numeric Type or of a recognized
        /// Type which can be converted to a numeric value in a standard way can be sampled for a sampled metric counter.
        /// Supported numeric .NET types include: Double, Single, Decimal, Int64, UInt64, Int32, UInt32, Int16, and UInt16.
        /// The common time representation types: DateTime, DateTimeOffset, and TimeSpan are also supported by automatically
        /// taking their Ticks value.  All sampled metric data samples are converted to a Double (double-precision floating
        /// point) value when sampled.</remarks>
        /// <param name="userDataType">The typeof(SomeSpecificType) or dataValue.GetType() to check.</param>
        /// <returns>True if Loupe supports sampled metric data samples with that Type, otherwise false.</returns>
        public static bool IsValidDataType(Type userDataType)
        {
            return s_DataTypeSupported.ContainsKey(userDataType);
        }

        /// <summary>
        /// Checks the provided SamplingType enum value to determine if that sampling type requires two values per sample.
        /// </summary>
        /// <remarks>Sampled metrics sample either single values (*Count sampling types) or pairs of values (*Fraction
        /// sampling types), determined by their sampling type.  This method distinguishes between the two scenarios.
        /// The *Fraction sampling types record a numerator and a divisor for each sample, so when defining sampled
        /// metric counters with SampledMetric and SampledMetricValue attributes, these sampling types require a
        /// corresponding SampledMetricDivisor attribute for the same counter name.  The *Count sampling types
        /// only record a single value for each sample, so they do not need a divisor to be specified.</remarks>
        /// <param name="samplingType">A SamplingType enum value to check.</param>
        /// <returns>True if the given sampling type requires a second value for each sample as the divisor.</returns>
        public static bool RequiresDivisor(SamplingType samplingType)
        {
            bool required;
            switch (samplingType)
            {
                case SamplingType.RawFraction:
                case SamplingType.IncrementalFraction:
                case SamplingType.TotalFraction:
                    required = true;
                    break;
                default:
                    required = false;
                    break;
            }

            return required;
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
        /// Check that the specified binding matches the specified member, assuming the same declaring type.
        /// </summary>
        /// <param name="binding">A NameValuePair&lt;MemberTypes&gt; representing the name and member type binding to check.</param>
        /// <param name="member">The MemberInfo of the member to check against.</param>
        /// <returns>True if the binding matches the given member, false if the binding (or member) is null or does not match.</returns>
        private static bool BindingMatchesMember(NameValuePair<MemberTypes> binding, MemberInfo member)
        {
            return (binding != null && member != null && binding.Value == member.MemberType && binding.Name == member.Name);
        }

        /// <summary>
        /// The intended method of interpreting the sampled counter value.
        /// </summary>
        public SamplingType SamplingType
        {
            get { return (SamplingType)m_WrappedDefinition.MetricSampleType; }
        }

        /// <summary>
        /// Indicates whether a final value can be determined from just one sample or if two comparable samples are required.
        /// </summary>
        public bool RequiresMultipleSamples { get { return m_WrappedDefinition.RequiresMultipleSamples; } }

        /// <summary>
        /// Indicates if this definition is configured to retrieve its information directly from an object.
        /// </summary>
        /// <remarks>When true, metric instances and samples can be generated from a live object of the same type that was used 
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
        /// Compare this custom sampled metric definition with another to determine if they are identical.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(SampledMetricDefinition other)
        {
            //we let our base object do the compare, we're really just casting things
            return WrappedDefinition.CompareTo(other?.WrappedDefinition);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(SampledMetricDefinition other)
        {
            //We're really just a type cast, refer to our base object
            return WrappedDefinition.Equals(other?.WrappedDefinition);
        }


        /// <summary>
        /// The set of custom sampled metrics that use this definition.
        /// </summary>
        /// <remarks>All metrics with the same definition are of the same object type.</remarks>
        internal SampledMetricCollection Metrics
        {
            get { return m_Metrics; }
        }

        /// <summary>
        /// Write a metric sample to the specified metric instance with the provided data, for non-fraction sampling types.
        /// </summary>
        /// <remarks>The specified metric instance is created if it does not already exist.</remarks>
        /// <param name="instanceName">The instance name to use, or null or empty for the default metric instance.</param>
        /// <param name="rawValue">The raw data value</param>
        public void WriteSample(string instanceName, double rawValue)
        {
            m_WrappedDefinition.WriteSample(instanceName, rawValue);
        }

        /// <summary>
        /// Write a metric sample to the specified metric instance with the provided data and recent timestamp, for non-fraction sampling types.
        /// </summary>
        /// <remarks>The specified metric instance is created if it does not already exist.</remarks>
        /// <param name="instanceName">The instance name to use, or null or empty for the default metric instance.</param>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="rawTimestamp">The exact date and time the raw value was determined</param>
        public void WriteSample(string instanceName, double rawValue, DateTimeOffset rawTimestamp)
        {
            m_WrappedDefinition.WriteSample(instanceName, rawValue, rawTimestamp);
        }

        /// <summary>
        /// Write a metric sample to the specified metric instance with the provided data pair, for fraction sampling types.
        /// </summary>
        /// <remarks>The specified metric instance is created if it does not already exist.</remarks>
        /// <param name="instanceName">The instance name to use, or null or empty for the default metric instance.</param>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="baseValue">The divisor entry of this sample.</param>
        public void WriteSample(string instanceName, double rawValue, double baseValue)
        {
            m_WrappedDefinition.WriteSample(instanceName, rawValue, baseValue);
        }

        /// <summary>
        /// Write a metric sample to the specified metric instance with the provided data pair and recent timestamp, for fraction sampling types.
        /// </summary>
        /// <remarks>The specified metric instance is created if it does not already exist.</remarks>
        /// <param name="instanceName">The instance name to use, or null or empty for the default metric instance.</param>
        /// <param name="rawValue">The raw data value</param>
        /// <param name="rawTimestamp">The exact date and time the raw value was determined</param>
        /// <param name="baseValue">The divisor entry of this sample.</param>
        public void WriteSample(string instanceName, double rawValue, double baseValue, DateTimeOffset rawTimestamp)
        {
            m_WrappedDefinition.WriteSample(instanceName, rawValue, baseValue, rawTimestamp);
        }

        /// <summary>
        /// Write a metric sample to the specified instance of this metric definition using the provided data object.
        /// </summary>
        /// <remarks>
        /// <para>This overload may only be used if this metric definition was created by SampledMetric and SampledMetricValue
        /// attributes on a particular Type (class, struct, or interface), and only for userDataObjects of a type assignable
        /// to that bound type for this definition.  Also see the static WriteAllSamples() method.</para>
        /// <para>The provided instanceName parameter will override any instance name binding set for this definition
        /// with a SampledMetricInstanceName attribute (this method overload ignores the instance name binding).  The
        /// specified metric instance is created if it does not already exist.  See the other overloads taking a
        /// userDataObject as the first parameter to use the bound member to determine a metric instance name
        /// from the user data object automatically, with an optional fall-back instance name.</para>
        /// </remarks>
        /// <param name="instanceName">The instance name to use, or null or empty for the default metric instance.</param>
        /// <param name="metricData">A data object to sample, compatible with the binding type of this definition.</param>
        /// <exception cref="ArgumentNullException">The provided metricData object is null</exception>
        /// <exception cref="ArgumentException">This sampled metric definition is not bound to sample automatically from a user data object.  
        /// A different overload must be used to specify the data value(s) directly.<br />
        /// - or -<br />
        /// The provided user data object type is not assignable to this sampled metric's bound type and can not be sampled automatically for this metric definition.</exception>
        internal void WriteSample(string instanceName, object metricData)
        {
            if (metricData == null)
            {
                throw new ArgumentNullException(nameof(metricData));
            }

            if (IsBound == false)
            {
                throw new ArgumentException("This sampled metric definition is not bound to sample automatically from a user data object.  A different overload must be used to specify the data value(s) directly.");
            }

            Type userDataType = metricData.GetType();
            if (BoundType.IsAssignableFrom(userDataType) == false)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The provided user data object type ({0}) is not assignable to this sampled metric's bound type ({1}) and can not be sampled automatically for this metric definition.",
                                                          userDataType, BoundType));
            }

            SampledMetric metricInstance = SampledMetric.Register(this, instanceName); // Get the particular instance specified.
            metricInstance.WriteSample(metricData); // And write a sample from the provided data object.
        }

        /// <summary>
        /// Write a metric sample to an automatically-determined instance of this metric definition using the provided data object, with a fall-back instance name.
        /// </summary>
        /// <remarks>
        /// <para>This overload may only be used if this metric definition was created by SampledMetric and SampledMetricValue
        /// attributes on a particular Type (class, struct, or interface), and only for userDataObjects of a type assignable
        /// to that bound type for this definition.</para>
        /// <para>The metric instance name will be obtained from the member which was marked with the SampledMetricInstanceName
        /// attribute.  If none is bound, the instance name parameter will be used as a fall-back.  The determined metric
        /// instance will be created if it does not already exist.</para>
        /// </remarks>
        /// <param name="metricData">A data object to sample, compatible with the binding type of this definition.</param>
        /// <param name="fallbackInstanceName">The instance name to fall back on if this definition does not specify an instance name binding (may be null).</param>
        /// <exception cref="ArgumentNullException">The provided metricData object is null</exception>
        /// <exception cref="ArgumentException">This sampled metric definition is not bound to sample automatically from a user data object. 
        /// A different overload must be used to specify the data value(s) directly.<br />
        /// - or -<br />
        /// The provided user data object type is not assignable to this sampled metric's bound type and can not be sampled automatically for this metric definition.</exception>
        public void WriteSample(object metricData, string fallbackInstanceName)
        {
            if (metricData == null)
            {
                throw new ArgumentNullException(nameof(metricData));
            }

            if (IsBound == false)
            {
                throw new ArgumentException("This sampled metric definition is not bound to sample automatically from a user data object.  A different overload must be used to specify the data value(s) directly.");
            }

            Type userDataType = metricData.GetType();
            if (BoundType.IsAssignableFrom(userDataType) == false)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The provided user data object type ({0}) is not assignable to this sampled metric's bound type ({1}) and can not be sampled automatically for this metric definition.",
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

        /// <summary>
        /// Write a metric sample to an automatically-determined instance of this metric definition using the provided data object.
        /// </summary>
        /// <remarks>
        /// <para>This overload may only be used if this metric definition was created by SampledMetric and SampledMetricValue
        /// attributes on a particular Type (class, struct, or interface), and only for userDataObjects of a type assignable
        /// to that bound type for this definition.</para>
        /// <para>The metric instance name will be obtained from the member which was marked with the SampledMetricInstanceName
        /// attribute.  If none is bound, the default instance will be used (a null instance name).  The determined metric
        /// instance will be created if it does not already exist.  See the overloads with an instanceName parameter to
        /// specify a particular metric instance name.</para>
        /// </remarks>
        /// <param name="metricData">A data object to sample, compatible with the binding type of this definition.</param>
        /// <exception cref="ArgumentNullException">The provided metricData object is null</exception>
        /// <exception cref="ArgumentException">This sampled metric definition is not bound to sample automatically from a user data object. 
        /// A different overload must be used to specify the data value(s) directly.<br />
        /// - or -<br />
        /// The provided user data object type is not assignable to this sampled metric's bound type and can not be sampled automatically for this metric definition.</exception>
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
        /// Sample every sampled metric defined by SampledMetric and SampledMetricValue attributes on the provided data object at any interface or inheritance level.
        /// </summary>
        /// <param name="metricData">A user data object defining sampled metrics by attributes on itself or its interfaces or any inherited type.</param>
        /// <param name="fallbackInstanceName">The instance name to fall back on if a given definition does not specify an instance name binding (may be null).</param>
        public static void Write(object metricData, string fallbackInstanceName)
        {
            // Actual logic is in SampledMetric class.
            SampledMetric.Write(metricData, fallbackInstanceName);
        }

        /// <summary>
        /// Sample every sampled metric defined by SampledMetric and SampledMetricValue attributes on the provided data object at any interface or inheritance level.
        /// </summary>
        /// <param name="metricData">A user data object defining sampled metrics by attributes on itself or its interfaces or any inherited type.</param>
        public static void Write(object metricData)
        {
            // Actual logic is in SampledMetric class.
            SampledMetric.Write(metricData, string.Empty);
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Indicates whether two samples are required to calculate a metric value or not. 
        /// </summary>
        /// <remarks>
        /// Many sample types require multiple samples to determine an output value because they work with 
        /// the change between two points.
        /// </remarks>
        internal static bool SampledMetricTypeRequiresMultipleSamples(SamplingType samplingType)
        {
            bool multipleRequired;

            //based purely on the counter type, according to Microsoft documentation
            switch (samplingType)
            {
                case SamplingType.RawFraction:
                case SamplingType.RawCount:
                    //these just require one sample
                    multipleRequired = false;
                    break;
                default:
                    //everything else requires more than one sample
                    multipleRequired = true;
                    break;
            }

            return multipleRequired;
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

        /// <summary>
        /// Get the lock object for this sampled metric definition.
        /// </summary>
        internal object Lock { get { return m_WrappedDefinition.Lock; } }

        /// <summary>
        /// The internal custom sampled metric definition we're wrapping. 
        /// </summary>
        internal Core.Monitor.CustomSampledMetricDefinition WrappedDefinition { get { return m_WrappedDefinition; } }

        /// <summary>
        /// The internal metric definition this IMetricDefinition is wrapping.
        /// </summary>
        Core.Monitor.MetricDefinition IMetricDefinition.WrappedDefinition { get { return m_WrappedDefinition; } }

        /// <summary>
        /// Set the binding for the primary sampling data value (numerator);
        /// </summary>
        /// <param name="member">The MemberInfo of the member to bind to.</param>
        private void SetDataBinding(MemberInfo member)
        {
            DataBinding = new NameValuePair<MemberTypes>(member.Name, member.MemberType);
        }

        /// <summary>
        /// Contains a name-value pair of data member name and MemberType, or null if not bound.
        /// </summary>
        internal NameValuePair<MemberTypes> DataBinding { get; private set; }

        /// <summary>
        /// Indicates whether the value is configured for automatic collection through binding.
        /// </summary>
        /// <remarks>If true, the other binding-related properties are available.</remarks>
        internal bool DataBound { get { return (DataBinding != null); } }

        /// <summary>
        /// The type of member that this value is bound to (field, property or method).
        /// </summary>
        /// <remarks>This property is only valid if Bound is true.</remarks>
        internal MemberTypes DataMemberType {get { return DataBound ? DataBinding.Value : 0; } }

        /// <summary>
        /// The name of the member that this value is bound to.
        /// </summary>
        /// <remarks>This property is only valid if Bound is true.</remarks>
        internal string DataMemberName { get { return DataBound ? DataBinding.Name : null; } }

        /// <summary>
        /// Set the binding for the secondary sampling data value (divisor);
        /// </summary>
        /// <param name="member">The MemberInfo of the member to bind to.</param>
        private void SetDivisorBinding(MemberInfo member)
        {
            DivisorBinding = new NameValuePair<MemberTypes>(member.Name, member.MemberType);
        }

        /// <summary>
        /// Contains a name-value pair of divisor member name and MemberType, or null if not bound.
        /// </summary>
        internal NameValuePair<MemberTypes> DivisorBinding { get; private set; }

        /// <summary>
        /// Indicates whether the divisor is configured for automatic collection through binding.
        /// </summary>
        /// <remarks>If true, the other binding-related properties are available.</remarks>
        internal bool DivisorBound { get { return (DivisorBinding != null); } }

        /// <summary>
        /// The type of member that the divisor is bound to (field, property or method).
        /// </summary>
        /// <remarks>This property is only valid if Bound is true.</remarks>
        internal MemberTypes DivisorMemberType { get { return DivisorBound ? DivisorBinding.Value : 0; } }

        /// <summary>
        /// The name of the member that the divisor is bound to.
        /// </summary>
        /// <remarks>This property is only valid if Bound is true.</remarks>
        internal string DivisorMemberName { get { return DivisorBound ? DivisorBinding.Name : null; } }

        #endregion

        #region IMetricDefinition Members

        /// <summary>
        /// The unique Id of this sampled metric definition.  This can reliably be used as a key to refer to this item, within the same session which created it.
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
            //internal set { m_WrappedDefinition.Caption = value; }
        }

        /// <summary>
        /// A description of what is tracked by this metric, suitable for end-user display.
        /// </summary>
        public string Description
        {
            get { return m_WrappedDefinition.Description; }
            //internal set { m_WrappedDefinition.Description = value; }
        }

        /// <summary>
        /// The recommended default display interval for graphing. 
        /// </summary>
        public SamplingInterval Interval
        {
            get { return (SamplingInterval)m_WrappedDefinition.Interval; }
        }

        /// <summary>
        /// The display caption for the units this metric's values represent, or null for unit-less values.
        /// </summary>
        public string UnitCaption
        {
            get { return m_WrappedDefinition.UnitCaption; }
        }

        /// <summary>
        /// The metric capture system label under which this metric definition was created.
        /// </summary>
        /// <remarks>This label distinguishes metrics defined and captured by different libraries from each other,
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
        /// Indicates that this sampled metric definition has been registered and can not be altered (always true for sampled metric definitions).
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
        /// <exception cref="ArgumentNullException">The provided key was null.</exception>
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
        /// <exception cref="ArgumentNullException">The provided metricsSystem, categoryName, or counterName was null.</exception>
        public static bool ContainsKey(string metricsSystem, string categoryName, string counterName)
        {
            //gateway to our alternate inner dictionary
            return s_Definitions.ContainsKey(metricsSystem, categoryName, counterName);
        }

        /// <summary>
        /// Retrieve a SampledMetricDefinition by its Id, if present. (Throws an ArgumentException if the Id resolves to an EventMetricDefinition instead.)
        /// </summary>
        /// <remarks>This method looks in the collection of registered metric definitions for the specified Id key.  If it
        /// is not found, the output is set to null and the method returns false.  If the Id key is found and resolves to a
        /// SampledMetricDefinition, it is stored in the value output parameter and the method returns true.  If the Id key
        /// is found but is not a SampledMetricDefinition, an ArgumentException is thrown to signal a usage inconsistency
        /// in your code.</remarks>
        /// <param name="id">The Id of the sampled metric definition to get.</param>
        /// <param name="value">The output variable to receive the SampledMetricDefinition object if found (null if not).</param>
        /// <returns>False if no metric definition is registered with the given Id, true if a SampledMetricDefinition is
        /// registered with the given Id, or throws an exception if the registered definition is not a SampledMetricDefinition.</returns>
        public static bool TryGetValue(Guid id, out SampledMetricDefinition value)
        {
            //gateway to our internal collection TryGetValue()
            bool foundValue = s_Definitions.TryGetValue(id, out var definition);
            value = foundValue ? definition as SampledMetricDefinition : null;
            if (foundValue && value == null)
            {
                // Uh-oh, we found one but it didn't resolve to an EventMetricDefinition!
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The metric definition found by Id ({0}) is not a sampled metric definition.", id));
            }
            return foundValue;
        }

        /// <summary>
        /// Retrieve a SampledMetricDefinition by its combined three-part Key string, if present.
        /// </summary>
        /// <remarks>This method looks in the collection of registered metric definitions for the specified Key.  If it
        /// is not found, the output is set to null and the method returns false.  If the Id key is found and resolves to a
        /// SampledMetricDefinition, it is stored in the value output parameter and the method returns true.  If the Id key
        /// is found but is not a SampledMetricDefinition, an ArgumentException is thrown to signal a usage inconsistency
        /// in your code.</remarks>
        /// <param name="key">The Key of the sampled metric definition to get (composed of the metrics system, category name,
        /// and counter name combined as a single string).</param>
        /// <param name="value">The output variable to receive the SampledMetricDefinition object if found (null if not).</param>
        /// <returns>False if no metric definition is registered with the given Key, true if a SampledMetricDefinition is
        /// registered with the given Key, or throws an exception if the registered definition is not a SampledMetricDefinition.</returns>
        /// <exception cref="ArgumentNullException">The provided key was null.</exception>
        /// <exception cref="ArgumentException">The metric definition found for the specified key is not a sampled metric definition.</exception>
        public static bool TryGetValue(string key, out SampledMetricDefinition value)
        {
            //protect ourself from a null before we do the trim (or we'll get an odd user the error won't understand)
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            //gateway to our inner dictionary try get value
            bool foundValue = s_Definitions.TryGetValue(key.Trim(), out var definition);
            value = foundValue ? definition as SampledMetricDefinition : null;
            if (foundValue && value == null)
            {
                // Uh-oh, we found one but it didn't resolve to an EventMetricDefinition!
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The metric definition found by Key \"{0}\" is not a sampled metric definition.", key));
            }
            return foundValue;
        }

        /// <summary>
        /// Retrieve a SampledMetricDefinition by its three key strings (metrics system, category name, and counter name), if present.
        /// </summary>
        /// <remarks>This method looks in the collection of registered metric definitions for the specified 3-part key.  If it
        /// is not found, the output is set to null and the method returns false.  If the Id key is found and resolves to a
        /// SampledMetricDefinition, it is stored in the value output parameter and the method returns true.  If the Id key
        /// is found but is not a SampledMetricDefinition, an ArgumentException is thrown to signal a usage inconsistency
        /// in your code.</remarks>
        /// <param name="metricsSystem">The metrics capture system label of the definition to look up.</param>
        /// <param name="categoryName">The name of the category with which the definition is associated.</param>
        /// <param name="counterName">The name of the definition within the category.</param>
        /// <param name="value">The output variable to receive the SampledMetricDefinition object if found (null if not).</param>
        /// <returns>False if no metric definition is registered with the given Key, true if a SampledMetricDefinition is
        /// registered with the given Key, or throws an exception if the registered definition is not a SampledMetricDefinition.</returns>
        /// <exception cref="ArgumentNullException">The provided metricsSystem, categoryName, or counterName was null.</exception>
        /// <exception cref="ArgumentException">The metric definition found for the specified key is not a sampled metric definition.</exception>
        public static bool TryGetValue(string metricsSystem, string categoryName, string counterName, out SampledMetricDefinition value)
        {
            //gateway to our inner dictionary try get value
            bool foundValue = s_Definitions.TryGetValue(metricsSystem, categoryName, counterName, out var definition);
            value = foundValue ? definition as SampledMetricDefinition : null;
            if (foundValue && value == null)
            {
                // Uh-oh, we found one but it didn't resolve to an EventMetricDefinition!
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The metric definition found by metrics system ({0}) category name ({1}) counter name ({2}) is not a sampled metric definition.",
                                                          metricsSystem, categoryName, counterName));
            }
            return foundValue;
        }

        /// <summary>
        /// Find an existing sampled metric definition previously registered via SampledMetric and SampledMetricValue attributes on a specific Type, by its counter name.
        /// </summary>
        /// <remarks>This method overload can obtain a previously registered SampledMetricDefinition created through
        /// SampledMetric and SampledMetricValue attributes, by specifying the Type containing those attributes.  If the
        /// specified Type does not have a SampledMetric attribute defined, or if the Type has a SampledMetric attribute
        /// but has not been registered (e.g. by a call to SampledMetricDefinition.Register(userObjectType)), then false is
        /// returned (with out value set to null).  If a sampled metric defined by attributes on that Type has been
        /// successfully registered, then true is returned (with the registered SampledMetricDefinition stored in the out
        /// value).  If the metric definition found by the 3-part Key used in the SampledMetric attribute (along with the
        /// specified counter name) is not a sampled
        /// metric (e.g. an event metric definition was registered with that Key), then an ArgumentException is thrown to
        /// signal your programming mistake.  Inheritance and interfaces will <b>not</b> be searched, so the specified Type
        /// must directly define the sampled metric, but valid objects of a type assignable to the specified bound Type of
        /// this definition <b>can</b> be sampled from the specific sampled metric definition found.</remarks>
        /// <param name="userObjectType">A specific Type with attributes defining one or more sampled metrics.</param>
        /// <param name="counterName">The counter name of the desired individual sampled metric definition defined by
        /// attributes on the specified Type.</param>
        /// <param name="value">The output variable to receive the SampledMetricDefinition object if found (null if not).</param>
        /// <returns>False if no SampledMetric attribute is found on the specified Type, or if no metric definition is
        /// registered with the 3-part Key found in that attribute (combined with the specified counter name),
        /// true if a SampledMetricDefinition is registered with
        /// the given Key, or throws an exception if the registered definition found is not a SampledMetricDefinition.</returns>
        /// <exception cref="ArgumentNullException">The userObjectType or counterName was null or empty.</exception>
        /// <exception cref="ArgumentException">The metric definition found for the specified key is not a sampled metric definition.</exception>
        public static bool TryGetValue(Type userObjectType, string counterName, out SampledMetricDefinition value)
        {
            value = null; // In case we don't find it.
            if (userObjectType == null)
            {
                throw new ArgumentNullException(nameof(userObjectType));
            }

            if (counterName == null)
            {
                throw new ArgumentNullException(nameof(counterName));
            }

            counterName = counterName.Trim(); // Trim any whitespace around it.
            if (string.IsNullOrEmpty(counterName))
            {
                throw new ArgumentNullException(
                    "The specified counter name is empty which is not allowed, so no metrics can be found for it.");
            }

            bool foundValue = false; // Haven't found the actual definition yet.
            // We shouldn't need a lock because we aren't changing the dictionary, just doing a single read check.
            lock (s_DictionaryLock) // But apparently Dictionaries are not internally threadsafe.
            {
                if (s_DefinitionsMap.TryGetValue(userObjectType, out var definitionList)) // Fast lookup???, for efficiency.
                {
                    if (definitionList != null && definitionList.Count > 0)
                    {
                        SampledMetricDefinition[] definitionArray = definitionList.ToArray(); // Snapshot it for thread safety.
                        foreach (SampledMetricDefinition definition in definitionArray)
                        {
                            if (definition.CounterName == counterName)
                            {
                                value = definition; // Hey, we found it!
                                foundValue = true; // Report success!
                                break; // Stop looking through the array.
                            }
                        }
                    }
                }
            }

            if (foundValue == false) // If we didn't find it on the known list for that type...
            {
                SampledMetricAttribute sampledMetricAttribute = null;
                if (userObjectType.IsDefined(typeof(SampledMetricAttribute), false))
                {
                    sampledMetricAttribute = userObjectType.GetTypeInfo().GetCustomAttribute<SampledMetricAttribute>();
                }

                if (sampledMetricAttribute != null)
                {
                    string metricsSystem = sampledMetricAttribute.MetricsSystem;
                    string categoryName = sampledMetricAttribute.MetricCategoryName;

                    //gateway to our inner dictionary try get value
                    foundValue = s_Definitions.TryGetValue(metricsSystem, categoryName, counterName, out var definition);
                    value = foundValue ? definition as SampledMetricDefinition : null;
                    if (foundValue && value == null)
                    {
                        // Uh-oh, we found one but it didn't resolve to a SampledMetricDefinition!
                        throw new ArgumentException(
                            string.Format(CultureInfo.InvariantCulture, 
                                "The metric definition registered for metrics system ({0}) and category name ({1}) from SampledMetric attribute on {3}, and specified counter name ({2}) is not a sampled metric definition.",
                                metricsSystem, categoryName, counterName, userObjectType.Name));
                    }
                }
                // Otherwise we already pre-set value to null, and foundvalue is still false, so we'll report the failure.
            }
            // Otherwise we found a valid definition in our Type-to-definition map, so we've output that and report success.

            return foundValue;
        }

        #endregion

    }
}
