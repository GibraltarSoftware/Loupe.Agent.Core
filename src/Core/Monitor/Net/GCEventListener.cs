using System;
using System.Diagnostics.Tracing;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor.Net
{
    /// <summary>
    /// Records Garbage Collector metrics
    /// </summary>
    /// <remarks>Leverages ETW metrics.</remarks>
    internal class GCEventListener : EventListener
    {
        private const int GC_KEYWORD = 0x0000001;

        private const string GenerationSize0Field = "GenerationSize0";
        private const string TotalPromotedSize0Field = "TotalPromotedSize0";
        private const string GenerationSize1Field = "GenerationSize1";
        private const string TotalPromotedSize1Field = "TotalPromotedSize1";
        private const string GenerationSize2Field = "GenerationSize2";
        private const string TotalPromotedSize2Field = "TotalPromotedSize2";
        private const string GenerationSize3Field = "GenerationSize3";
        private const string TotalPromotedSize3Field = "TotalPromotedSize3";
        private const string FinalizationPromotedSizeField = "FinalizationPromotedSize";
        private const string FinalizationPromotedCountField = "FinalizationPromotedCount";
        private const string PinnedObjectCountField = "PinnedObjectCount";
        private const string SinkBlockCountField = "SinkBlockCount";
        private const string GCHandleCountField = "GCHandleCount";
        private const string ClrInstanceIDField = "ClrInstanceID";

        private readonly EventMetric _eventMetric;

        /// <summary>
        /// Create a GC Event Listener (which will automatically start listening to and recording events)
        /// </summary>
        public GCEventListener()
        {
            //define our event metric now.
            var newEventMetricDefinition = new EventMetricDefinition(Log.Metrics, ProcessMonitor.ProcessMetricType, "Process.Memory", "Garbage Collection");

            EventMetricValueDefinition valueDefinition;
            var valueDefinitions = (EventMetricValueDefinitionCollection)newEventMetricDefinition.Values;
            valueDefinition = valueDefinitions.Add(GenerationSize0Field, typeof(ulong), "Gen 0 Heap Size", "The size, in bytes, of generation 0 memory");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Bytes";

            valueDefinition = valueDefinitions.Add(TotalPromotedSize0Field, typeof(ulong), "Total Promoted Size 0", "The size, in bytes, of generation 0 memory");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Bytes";

            valueDefinition = valueDefinitions.Add(GenerationSize1Field, typeof(ulong), "Gen 1 Heap Size", "The size, in bytes, of generation 1 memory");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Bytes";

            valueDefinition = valueDefinitions.Add(TotalPromotedSize1Field, typeof(ulong), "Total Promoted Size 1", "The number of bytes that are promoted from generation 1 to generation 2.");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Bytes";

            valueDefinition = valueDefinitions.Add(GenerationSize2Field, typeof(ulong), "Gen 2 Heap Size", "The size, in bytes, of generation 2 memory.");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Bytes";

            valueDefinition = valueDefinitions.Add(TotalPromotedSize2Field, typeof(ulong), "Total Promoted Size 2", "The number of bytes that survived in generation 2 after the last collection.");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Bytes";

            valueDefinition = valueDefinitions.Add(GenerationSize3Field, typeof(ulong), "Gen 3 Heap Size", "The size, in bytes, of the large object heap.");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Bytes";

            valueDefinition = valueDefinitions.Add(TotalPromotedSize3Field, typeof(ulong), "Total Promoted Size 3", "The number of bytes that survived in the large object heap after the last collection.");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Bytes";

            valueDefinition = valueDefinitions.Add(FinalizationPromotedSizeField, typeof(ulong), "Finalization Promoted Size", "The total size, in bytes, of the objects that are ready for finalization.");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Bytes";

            valueDefinition = valueDefinitions.Add(FinalizationPromotedCountField, typeof(ulong), "Finalization Promoted Count", "The number of objects that are ready for finalization.");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Objects";

            valueDefinition = valueDefinitions.Add(PinnedObjectCountField, typeof(uint), "Pinned Object Count", "The number of pinned (unmovable) objects.");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Objects";

            valueDefinition = valueDefinitions.Add(SinkBlockCountField, typeof(uint), "Sink Block Count", "The number of synchronization blocks in use.");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Locks";

            valueDefinition = valueDefinitions.Add(GCHandleCountField, typeof(uint), "GC Handle Count", "The number of garbage collection handles in use.");
            valueDefinition.DefaultTrend = EventMetricValueTrend.Average;
            valueDefinition.UnitCaption = "Handles";

            valueDefinition = valueDefinitions.Add(ClrInstanceIDField, typeof(ushort), "CLR Instance Id", "Unique ID for the instance of CLR or CoreCLR.");

            newEventMetricDefinition.DefaultValue = newEventMetricDefinition.Values[GenerationSize0Field];
            newEventMetricDefinition = newEventMetricDefinition.Register(); //register it with the collection.

            //create the default instance for us to log to.
            _eventMetric = EventMetric.AddOrGet(newEventMetricDefinition, null);
        }

        /// <inheritdoc />
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // look for .NET Garbage Collection events
            if (eventSource.Name.Equals("Microsoft-Windows-DotNETRuntime"))
            {
                EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)GC_KEYWORD);
            }
        }

        /// <inheritdoc />
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            try //we don't want to throw an exception back to ETW.
            {
                switch (eventData.EventName)
                {
                    case "GCHeapStats_V1":
                        ProcessHeapStats(eventData);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Warning, LogWriteMode.Queued, ex, true, "Loupe.Listener", 
                        "Unable to record GC Event due to " + ex.GetBaseException().GetType(), 
                        "We will skip recording this event.");
            }
        }

        private void ProcessHeapStats(EventWrittenEventArgs eventData)
        {
            var sample = _eventMetric?.CreateSample();

            if (sample == null) return;

            sample.SetValue(GenerationSize0Field, (ulong)eventData.Payload[0]);
            sample.SetValue(TotalPromotedSize0Field, (ulong)eventData.Payload[1]);
            sample.SetValue(GenerationSize1Field, (ulong)eventData.Payload[2]);
            sample.SetValue(TotalPromotedSize1Field, (ulong)eventData.Payload[3]);
            sample.SetValue(GenerationSize2Field, (ulong)eventData.Payload[4]);
            sample.SetValue(TotalPromotedSize2Field, (ulong)eventData.Payload[5]);
            sample.SetValue(GenerationSize3Field, (ulong)eventData.Payload[6]);
            sample.SetValue(TotalPromotedSize3Field, (ulong)eventData.Payload[7]);
            sample.SetValue(FinalizationPromotedSizeField, (ulong)eventData.Payload[8]);
            sample.SetValue(FinalizationPromotedCountField, (ulong)eventData.Payload[9]);
            sample.SetValue(PinnedObjectCountField, (uint)eventData.Payload[10]);
            sample.SetValue(SinkBlockCountField, (uint)eventData.Payload[11]);
            sample.SetValue(GCHandleCountField, (uint)eventData.Payload[12]);
            sample.SetValue(ClrInstanceIDField, (ushort)eventData.Payload[13]);
            sample.Write();
        }
    }
}
