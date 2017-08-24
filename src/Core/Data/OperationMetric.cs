using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Gibraltar.Monitor;
using Loupe.Extensibility.Data;



namespace Gibraltar.Data
{
    /// <summary>
    /// Record an event metric for a single execution of a data operation
    /// </summary>
    /// <remarks>This class is optimized to be used in a using statement.  It will automatically
    /// time the duration of the command and record an event metric when disposed.  It will also
    /// record trace messages for the start and end of each command so that it is unnecessary to add
    /// redundant trace messages in your method invocation to denote the boundaries of a command.
    /// If not explicitly Dispose'd (automatically done for you by a using statement)
    /// the metric will not be generated.</remarks>
    public class OperationMetric : IDisposable
    {
        //constants we use in place of what was previously on the attribute for the class
        /// <summary>
        /// The metric type name
        /// </summary>
        public const string MetricTypeName = "Gibraltar Software";

        /// <summary>
        /// The metric counter name
        /// </summary>
        public const string MetricCounterName = "Operation";

        /// <summary>
        /// The metric counter description
        /// </summary>
        public const string MetricDefinitionDescription = "Information about each time a data operation is performed";

        private Stopwatch m_Timer;
        private TimeSpan m_Duration;
        private string m_Category;
        private string m_OperationName;
        private string m_EndMessage;
        private object[] m_Args;
        private bool m_Disposed;

        /// <summary>
        /// Create a new operation metric monitoring object to record a single operation.
        /// </summary>
        /// <remarks>All event metrics are recorded under the same metric counter in Gibraltar.Data called Repository Operation.</remarks>
        /// <param name="category">The category to use for the metric</param>
        /// <param name="operationName">The name of the operation for tracking purposes</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public OperationMetric(string category, string operationName)
        {
            Initialize(category, operationName, null, null, null);
        }

        /// <summary>
        /// Create a new operation metric monitoring object to record a single operation.
        /// </summary>
        /// <remarks>All event metrics are recorded under the same metric counter in Gibraltar.Data called Repository Operation.</remarks>
        /// <param name="category">The category to use for the metric</param>
        /// <param name="operationName">The name of the operation for tracking purposes</param>
        /// <param name="startMessage">A trace message to add at the start of the operation.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public OperationMetric(string category, string operationName, string startMessage)
        {
            Initialize(category, operationName, startMessage, null, null);
        }

        /// <summary>
        /// Create a new operation metric monitoring object to record a single operation.
        /// </summary>
        /// <remarks>All event metrics are recorded under the same metric counter in Gibraltar.Data called Repository Operation.</remarks>
        /// <param name="category">The category to use for the metric</param>
        /// <param name="operationName">The name of the operation for tracking purposes</param>
        /// <param name="startMessage">A trace message to add at the start of the operation.</param>
        /// <param name="endMessage">A trace message to add at the end of the operation.</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public OperationMetric(string category, string operationName, string startMessage, string endMessage)
        {
            Initialize(category, operationName, startMessage, endMessage, null);
        }

        /// <summary>
        /// Create a new operation metric monitoring object to record a single operation.
        /// </summary>
        /// <remarks>All event metrics are recorded under the same metric counter in Gibraltar.Data called Repository Operation.</remarks>
        /// <param name="category">The category to use for the metric</param>
        /// <param name="operationName">The name of the operation for tracking purposes</param>
        /// <param name="startMessage">A trace message to add at the start of the operation. Any args provided will be inserted.</param>
        /// <param name="endMessage">A trace message to add at the end of the operation.  Any args provided will be inserted.</param>
        /// <param name="args">A variable number of arguments to insert into the start and end messages</param>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public OperationMetric(string category, string operationName, string startMessage, string endMessage, params object[] args)
        {
            Initialize(category, operationName, startMessage, endMessage, args);
        }

        #region Public Properties and Methods

        /// <summary>
        /// The operation that was executed.
        /// </summary>
        public string OperationName { get { return m_OperationName; } }

        /// <summary>
        /// The duration of the command
        /// </summary>
        public TimeSpan Duration { get { return m_Duration; } }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// <remarks>Calling Dispose() (automatic when a using statement ends) will generate the metric.</remarks>
        /// </summary>
        /// <filterpriority>2</filterpriority>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public void Dispose()
        {
            Dispose(true);

            //SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.
        /// </summary>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (!m_Disposed)
            {
                if (releaseManaged)
                {
                    // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                    // Other objects may be referenced in this case

                    // We'll get here when the using statement ends, so generate the metric now...

                    // users do NOT expect exceptions when recording event metrics like this
                    try
                    {
                        StopAndRecordMetric();

                        // Use skipFrames = 2 to attribute the EndMessage to whoever called Dispose() (which then called us).

                        // careful, built-in stuff doesn't like nulls for args
                        if (m_Args == null)
                        {
                            Log.WriteMessage(LogMessageSeverity.Verbose, LogWriteMode.Queued, 2, null, null, null, m_EndMessage);
                        }
                        else
                        {
                            Log.WriteMessage(LogMessageSeverity.Verbose, LogWriteMode.Queued, 2, null, null, null, m_EndMessage, m_Args);
                        }
                    }
                    catch
                    {
#if DEBUG
                        throw;
#endif
                    }
                }
                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here

                m_Disposed = true; // Make sure we only do this once
            }
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Our real constructor logic
        /// </summary>
        /// <remarks>This is in its own special method so that the number of stack frames from the caller to this
        /// method is constant regardless of constructor.</remarks>
        /// <param name="category">The category to use for the metric</param>
        /// <param name="operationName">The name of the command for tracking purposes</param>
        /// <param name="startMessage">A trace message to add at the start of the command. Any args provided will be inserted.</param>
        /// <param name="endMessage">A trace message to add at the end of the command.  Any args provided will be inserted.</param>
        /// <param name="args">A variable number of arguments to insert into the start and end messages</param>
        private void Initialize(string category, string operationName, string startMessage, string endMessage, params object[] args)
        {
            //we start when we get called
            m_Timer = Stopwatch.StartNew();

            //and record off our input
            m_Category = string.IsNullOrEmpty(category) ? "Gibraltar.Data" : category;
            m_OperationName = string.IsNullOrEmpty(operationName) ? "Operation" : operationName;
            m_Args = args;

            //users do NOT expect exceptions when recording metrics like this.
            try
            {
                // Use skipFrames = 2 to attribute the start message to whoever called the constructor (which then called us).

                //behave sanely if either message argument is missing
                if (string.IsNullOrEmpty(startMessage))
                {
                    //because it will know where we were when we started, and we want these messages to be easily filterable, 
                    //use a static string.
                    Log.WriteMessage(LogMessageSeverity.Verbose, LogWriteMode.Queued, 2, null, null, null,
                                     "{0} started.", m_OperationName);
                }
                else
                {
                    //careful, built-in stuff doesn't like nulls for args
                    if (m_Args == null)
                    {
                        Log.WriteMessage(LogMessageSeverity.Verbose, LogWriteMode.Queued, 2, null, null, null, startMessage);
                    }
                    else
                    {
                        Log.WriteMessage(LogMessageSeverity.Verbose, LogWriteMode.Queued, 2, null, null, null,
                                         startMessage, m_Args);
                    }
                }

                if (string.IsNullOrEmpty(endMessage))
                {
                    //because it will know where we were when we completed, and we want these messages to be easily filterable, 
                    //use a static string.
                    m_EndMessage = string.Format(CultureInfo.CurrentCulture, "{0} completed.", m_OperationName);
                }
                else
                {
                    m_EndMessage = endMessage;
                }
            }
            catch
            {
#if DEBUG
                throw;
#endif
            }
        }


        private void StopAndRecordMetric()
        {
            //record our end time
            if (m_Timer == null)
            {
                m_Duration = new TimeSpan(0);
            }
            else
            {
                m_Timer.Stop();
                m_Duration = m_Timer.Elapsed;
            }

            //Get the METRIC DEFINITION
            IMetricDefinition metricDefinition;
            EventMetricDefinition eventDefinition;
            if (Log.Metrics.TryGetValue(MetricTypeName, m_Category, MetricCounterName, out metricDefinition) == false)
            {
                //it doesn't exist yet - add it
                eventDefinition = new EventMetricDefinition(MetricTypeName, m_Category, MetricCounterName);
                eventDefinition.Description = MetricDefinitionDescription;

                EventMetricValueDefinitionCollection valueDefinitionCollection = (EventMetricValueDefinitionCollection)eventDefinition.Values;
                valueDefinitionCollection.Add("operationname", typeof(string), "Operation Name", "The operation that was executed.");

                valueDefinitionCollection.Add("duration", typeof(TimeSpan), "Duration", "The duration the operation executed.");
                ((EventMetricValueDefinition)eventDefinition.Values["duration"]).UnitCaption = "Milliseconds";
                eventDefinition.DefaultValue = eventDefinition.Values["duration"];

                //and don't forget to register it!
                eventDefinition = eventDefinition.Register();
            }
            else
            {
                eventDefinition = (EventMetricDefinition)metricDefinition;
            }

            //Get the METRIC
            IMetric metric;
            EventMetric eventMetric;
            if (eventDefinition.Metrics.TryGetValue(null, out metric) == false)
            {
                eventMetric = new EventMetric(eventDefinition, (string)null);
            }
            else
            {
                eventMetric = (EventMetric)metric;
            }


            //and finally we can RECORD THE SAMPLE.
            EventMetricSample metricSample = eventMetric.CreateSample();
            metricSample.SetValue("operationname", OperationName);
            metricSample.SetValue("duration", Duration);
            metricSample.Write();
        }

        #endregion
    }
}
