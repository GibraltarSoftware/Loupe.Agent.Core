using System;
using System.Reflection;
using Loupe.Extensibility.Data;
using Loupe.Logging;


namespace Loupe.Monitor
{
    /// <summary>
    /// The collection of event metrics for a given event metric definition.
    /// </summary>
    public class EventMetricCollection : MetricCollection
    {
        /// <summary>
        /// Create a new event metric dictionary for the provided definition.
        /// </summary>
        /// <remarks>This dictionary is created automatically by the Custom Sampled Metric Definition during its initialization.</remarks>
        /// <param name="metricDefinition">The definition of the custom sampled metric to create a metric dictionary for</param>
        public EventMetricCollection(EventMetricDefinition metricDefinition)
            : base(metricDefinition)
        {

        }

        /// <summary>
        /// Create a new metric object with the provided instance name and add it to the collection
        /// </summary>
        /// <param name="instanceName">The instance name to use, or blank or null for the default metric.</param>
        /// <returns>The new metric object that was added to the collection</returns>
        public EventMetric Add(string instanceName)
        {
            //Create a new metric object with the provided instance name (it will get added to us automatically)
            EventMetric newMetric = new EventMetric(Definition, instanceName);

            //finally, return the newly created metric object to our caller
            return newMetric;
        }

        /// <summary>Creates a new metric instance or returns an existing one by inspecting the provided object for EventMetricDefinition attributes.</summary>
        /// <remarks>If the metric doesn't exist, it will be created.
        /// If the metric definition does exist, but is not an Event Metric (or a derived class) an exception will be thrown.
        /// If the metric definition isn't bound to an object type, the default metric will be returned.
        /// The provided object must not be null and must be of the type the metric definition owning this dictionary is bound to.</remarks>
        /// <param name="userDataObject">The object to create a metric from.</param>
        /// <returns>The event metric object for the specified event metric instance.</returns>
        public EventMetric AddOrGet(object userDataObject)
        {
            EventMetric newMetric;

            //we need a live object, not a null object or we'll fail
            if (userDataObject == null)
            {
                throw new ArgumentNullException(nameof(userDataObject));
            }

            //great.  We now know a lot - namely that it has to have the right attributes, etc. to define a metric so we can 
            //now go and find all of the information we need to create a new metric.
            string instanceName = null;
            if (Definition.NameBound)
            {
                //we don't even need to get it - we just care that it's defined.
                try
                {
                    //To be righteous, we need to only invoke the member we're looking at
                    //invoke the bound instance name from the type our definition is associated with.  This way if the object provided
                    //has multiple implementations that are metric-enabled, we use the correct one.
                    object rawValue;
                    switch (Definition.NameMemberType)
                    {
                        case MemberTypes.Field:
                            var field = Definition.BoundType.GetTypeInfo().GetDeclaredField(Definition.NameMemberName);
                            rawValue = field?.GetValue(userDataObject);
                            break;
                        case MemberTypes.Method:
                            var method = Definition.BoundType.GetTypeInfo().GetDeclaredMethod(Definition.NameMemberName);
                            rawValue = method?.Invoke(userDataObject, null);
                            break;
                        case MemberTypes.Property:
                            var property = Definition.BoundType.GetTypeInfo().GetDeclaredProperty(Definition.NameMemberName);
                            rawValue = property?.GetValue(userDataObject);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    //and the raw value is either null or something we're going to convert to a string
                    if (rawValue == null)
                    {
                        instanceName = null;
                    }
                    else
                    {
                        instanceName = rawValue.ToString();
                    }
                }
                catch (Exception ex)
                {
                    //just trace log this - we can continue, they'll just get the default instance until they fix their code.
                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, true, "Loupe", "Unable to add metric to collection due to " + ex.GetBaseException().GetType().Name,
                            "We will not report this error to our caller.  We'll return no metric");
                    return null; //because our instance name isn't valid.
                }
            }

            //now that we have our instance name, we go ahead and see if there is already an instance with the right name or just add it
            lock (this.Lock) //make sure the try & add are atomic
            {
                if (TryGetValue(instanceName, out newMetric) == false)
                {
                    //there isn't one with the right name, we need to create it.
                    newMetric = Add(instanceName);
                }
            }

            //return what we got - we have an object one way or another, or we threw an exception.
            return newMetric;
        }


        /// <summary>
        /// The definition of all of the metrics in this collection.
        /// </summary>
        public new EventMetricDefinition Definition { get { return (EventMetricDefinition)base.Definition; } }


        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(Guid key, out EventMetric value)
        {
            //We are playing a few games to get native typing here.  Because it's an OUt value, we
            //have to swap types around ourselves so we can cast.

            //gateway to our inner dictionary try get value
            bool result= base.TryGetValue(key, out var innerValue);

            value = innerValue as EventMetric; //No one expects exceptions from try get value, so if it's the wrong type, return null

            return (value != null);
        }

        /// <summary>
        /// Retrieve an item from the collection by its key if present.  If not present, the default value of the object is returned.
        /// </summary>
        /// <param name="key">The metric name to locate in the collection</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the collection contains an element with the specified key; otherwise false.</returns>
        public bool TryGetValue(string key, out EventMetric value)
        {
            //We are playing a few games to get native typing here.  Because it's an OUt value, we
            //have to swap types around ourselves so we can cast.

            //gateway to our inner dictionary try get value
            bool result = base.TryGetValue(key, out var innerValue);

            value = innerValue as EventMetric; //No one expects exceptions from try get value, so if it's the wrong type, return null

            return (value != null);
        }


        /// <summary>
        /// Retrieve the event metric by its zero-based index in collection. 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public new EventMetric this[int index]
        {
            get
            {
                return (EventMetric)base[index];
            }
            set
            {
                //we don't want to support setting an object by index, we are sorted.
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Retrieve event metric object by its Id
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public new EventMetric this[Guid ID]
        {
            get
            {
                return (EventMetric)base[ID];
            }
        }

        /// <summary>
        /// Retrieve event metric object by its name
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public new EventMetric this[string key]
        {
            get
            {
                return (EventMetric)base[key];
            }
        }
    }
}
