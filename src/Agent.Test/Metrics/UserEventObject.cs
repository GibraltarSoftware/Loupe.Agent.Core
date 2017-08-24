using System;
using System.Globalization;
using Gibraltar.Agent.Metrics;

#pragma warning disable 3003

namespace Loupe.Agent.Test.Metrics
{
    /// <summary>
    /// This is a user-provided data object which implements multiple events through inheritance and interfaces.
    /// </summary>
    public class UserMultipleEventObject : UserEventObject, IEventMetricOne, IEventMetricTwo, IEventMetricThree
    {
        public UserMultipleEventObject(string instanceName)
            : base(instanceName)
        {
            // Just rely on our base constructor.
        }
    }

    /// <summary>
    /// This is a user-provided data object, demonstrating reflection for event metrics.
    /// </summary>
    [EventMetric("EventMetricsByAttributesTests", "Attributes.Event Metric Data", "UserDataObject", Caption = "Event metric via attributes", Description = "Generic user data object used for testing event metrics with a trend for every numeric value type supported.")]
    public class UserEventObject
    {
        private readonly string m_InstanceName;
        private short m_Short;
        private ushort m_UShort;
        private int m_Int;
        private uint m_UInt;
        private long m_Long;
        private ulong m_ULong;
        private decimal m_Decimal;
        private double m_Double;
        private float m_Float;
        private TimeSpan m_TimeSpan;
        private string m_String;
        private UserDataEnumeration m_Enum;

        public UserEventObject(string instanceName)
        {
            m_InstanceName = instanceName;
        }

        public bool IsTrendableType(Type type)
        {
            bool trendable = false;

            //we're using Is so we can check for compatibile types, not just base types.
            if ((type == typeof(short)) || (type == typeof(ushort)) || (type == typeof(int)) || (type == typeof(uint)) || (type == typeof(long)) || (type == typeof(ulong)) ||
                (type == typeof(decimal)) || (type == typeof(double)) || (type == typeof(float)))
            {
                trendable = true;
            }
            //Now check object types
            else if ((type == typeof(DateTime)) || (type == typeof(TimeSpan)))
            {
                trendable = true;
            }

            return trendable;
        }

        [EventMetricInstanceName]
        public string InstanceName
        {
            get { return m_InstanceName; }
        }

        [EventMetricValue("short_average", SummaryFunction.Average, null, Caption = "Short Average", Description = "Data of type Short")]
        [EventMetricValue("short_sum", SummaryFunction.Sum, null, Caption = "Short Sum", Description = "Data of type Short")]
        [EventMetricValue("short_runningaverage", SummaryFunction.RunningAverage, null, Caption = "Short Running Average", Description = "Data of type Short")]
        [EventMetricValue("short_runningsum", SummaryFunction.RunningSum, null, Caption = "Short Running Sum", Description = "Data of type Short")]
        public short Short
        {
            get { return m_Short; }
            set { m_Short = value; }
        }

        [EventMetricValue("ushort_average", SummaryFunction.Average, null, Caption = "UShort Average", Description = "Data of type UShort")]
        [EventMetricValue("ushort_sum", SummaryFunction.Sum, null, Caption = "UShort Sum", Description = "Data of type UShort")]
        public ushort UShort
        {
            get { return m_UShort; }
            set { m_UShort = value; }
        }

        [EventMetricValue("int_average", SummaryFunction.Average, null, Caption = "Int Average", Description = "Data of type Int", IsDefaultValue = true)]
        [EventMetricValue("int_sum", SummaryFunction.Sum, null, Caption = "Int Sum", Description = "Data of type Int")]
        public int Int
        {
            get { return m_Int; }
            set { m_Int = value; }
        }

        [EventMetricValue("uint_average", SummaryFunction.Average, null, Caption = "UInt Average", Description = "Data of type UInt")]
        [EventMetricValue("uint_sum", SummaryFunction.Sum, null, Caption = "UInt Sum", Description = "Data of type UInt")]
        public uint UInt
        {
            get { return m_UInt; }
            set { m_UInt = value; }
        }

        [EventMetricValue("long_average", SummaryFunction.Average, null, Caption = "Long Average", Description = "Data of type Long")]
        [EventMetricValue("long_sum", SummaryFunction.Sum, null, Caption = "Long Sum", Description = "Data of type Long")]
        public long Long
        {
            get { return m_Long; }
            set { m_Long = value; }
        }

        [EventMetricValue("ulong_average", SummaryFunction.Average, null, Caption = "ULong Average", Description = "Data of type ULong")]
        [EventMetricValue("ulong_sum", SummaryFunction.Sum, null, Caption = "ULong Sum", Description = "Data of type ULong")]
        public ulong ULong
        {
            get { return m_ULong; }
            set { m_ULong = value; }
        }

        [EventMetricValue("decimal_average", SummaryFunction.Average, null, Caption = "Decimal Average", Description = "Data of type Decimal")]
        [EventMetricValue("decimal_sum", SummaryFunction.Sum, null, Caption = "Decimal Sum", Description = "Data of type Decimal")]
        public decimal Decimal
        {
            get { return m_Decimal; }
            set { m_Decimal = value; }
        }

        [EventMetricValue("double_average", SummaryFunction.Average, null, Caption = "Double Average", Description = "Data of type Double")]
        [EventMetricValue("double_sum", SummaryFunction.Sum, null, Caption = "Double Sum", Description = "Data of type Double")]
        public double Double
        {
            get { return m_Double; }
            set { m_Double = value; }
        }

        [EventMetricValue("float_average", SummaryFunction.Average, null, Caption = "Float Average", Description = "Data of type Float")]
        [EventMetricValue("float_sum", SummaryFunction.Sum, null, Caption = "Float Sum", Description = "Data of type Float")]
        public float Float
        {
            get { return m_Float; }
            set { m_Float = value; }
        }

        [EventMetricValue("timespan_average", SummaryFunction.Average, null, Caption = "TimeSpan Average", Description = "Data of type TimeSpan")]
        [EventMetricValue("timespan_sum", SummaryFunction.Sum, null, Caption = "TimeSpan Sum", Description = "Data of type TimeSpan")]
        [EventMetricValue("timespan_runningaverage", SummaryFunction.RunningAverage, null, Caption = "TimeSpan Running Average", Description = "Data of type TimeSpan represented as a running average.")]
        [EventMetricValue("timespan_runningsum", SummaryFunction.RunningSum, null, Caption = "TimeSpan Running Sum", Description = "Data of type TimeSpan represented as a running sum.")]
        public TimeSpan TimeSpan
        {
            get { return m_TimeSpan; }
            set { m_TimeSpan = value; }
        }

        [EventMetricValue("string", SummaryFunction.Count, null, Caption = "String", Description = "Data of type String")]
        public string String
        {
            get { return m_String; }
            set { m_String = value; }
        }

        [EventMetricValue("system.enum", SummaryFunction.Count, null, Caption = "System.Enum", Description = "Data of type System.Enum (a numeric enum, UserDataEnumeration")]
        public UserDataEnumeration Enum
        {
            get { return m_Enum; }
            set { m_Enum = value; }
        }


        public void SetValues(short sample)
        {
            //we just set each value to the provided value, much faster than calculating the range.
            m_Short = sample;
            m_UShort = (ushort)sample;
            m_Int = sample;
            m_UInt = (uint)sample;
            m_Long = sample;
            m_ULong = (ulong)sample;
            m_Decimal = sample;
            m_Double = sample;
            m_Float = sample;
            m_TimeSpan = new TimeSpan(sample);
            m_Enum = (UserDataEnumeration)sample;
            m_String = string.Format(CultureInfo.CurrentCulture, "The Current Sample Value Is {0}.", sample);
        }

        private static double InterpolateValue(double minValue, double maxValue, short sample, short maxSamples)
        {
            if (sample < 0)
                sample = 0;

            if (sample > maxSamples)
                sample = maxSamples;

            double delta = maxValue - minValue;
            double interval = delta / maxSamples;
            double offset = interval * sample;
            double value = minValue + offset;

            return value;
        }

        public void SetValues(short sample, short maxSamples)
        {
            //we have to set each numeric value, and we want to demonstrate a range of values.
            m_Short = (short)InterpolateValue(short.MinValue, short.MaxValue, sample, maxSamples);
            m_UShort = (ushort)InterpolateValue(ushort.MinValue, ushort.MaxValue, sample, maxSamples);

            m_Int = (int)InterpolateValue(int.MinValue, int.MaxValue, sample, maxSamples);
            m_UInt = (uint)InterpolateValue(uint.MinValue, uint.MaxValue, sample, maxSamples);

            m_Long = (long)InterpolateValue(long.MinValue, long.MaxValue, sample, maxSamples);
            m_ULong = (ulong)InterpolateValue(ulong.MinValue, ulong.MaxValue, sample, maxSamples);
            
            // We have to fudge these closer to 0, or else rounding into and out of double causes overflow on endpoints.
            var minDecimal = (double)(decimal.MinValue + 7590000000000m);
            var maxDecimal = (double)(decimal.MaxValue - 7590000000000m);
            m_Decimal = (decimal) InterpolateValue(minDecimal, maxDecimal, sample, maxSamples);
            //m_Decimal = (decimal)InterpolateValue((double)decimal.MinValue, (double)decimal.MaxValue, sample, maxSamples);

            // More interesting endpoints than their own min and max.
            m_Double = InterpolateValue(short.MinValue, ulong.MaxValue, sample, maxSamples);
            m_Float = (float)InterpolateValue(short.MinValue, long.MaxValue, sample, maxSamples);

            // Negatives wouldn't be sensible, right?  So interpolate from 0 to the largest long instead.
            m_TimeSpan = new TimeSpan((long)InterpolateValue(0, long.MaxValue, sample, maxSamples));

            m_Enum = (UserDataEnumeration)sample;

            m_String = string.Format(CultureInfo.CurrentCulture, "The Current Sample Value Is {0}.", sample);
        }
    }

    [EventMetric("EventMetricsByAttributesTests", "Attributes.Event Metric Data", "IEventMetricOne", Caption = "Event metric One", Description = "First event metric defined on an interface.")]
    public interface IEventMetricOne : IEventMetricThree
    {
        [EventMetricValue("short_average", SummaryFunction.Average, null, Caption = "Short Average", Description = "Data of type Short")]
        [EventMetricValue("short_sum", SummaryFunction.Sum, null, Caption = "Short Sum", Description = "Data of type Short")]
        short Short { get; }

        [EventMetricValue("int_average", SummaryFunction.Average, null, Caption = "Int Average", Description = "Data of type Int", IsDefaultValue = true)]
        [EventMetricValue("int_sum", SummaryFunction.Sum, null, Caption = "Int Sum", Description = "Data of type Int")]
        int Int { get; }

        [EventMetricValue("long_average", SummaryFunction.Average, null, Caption = "Long Average", Description = "Data of type Long")]
        [EventMetricValue("long_sum", SummaryFunction.Sum, null, Caption = "Long Sum", Description = "Data of type Long")]
        long Long { get; }
    }

    [EventMetric("EventMetricsByAttributesTests", "Attributes.Event Metric Data", "IEventMetricTwo", Caption = "Event metric Two", Description = "Second event metric defined on an interface.")]
    public interface IEventMetricTwo : IEventMetricFour
    {
        [EventMetricValue("ushort_average", SummaryFunction.Average, null, Caption = "UShort Average", Description = "Data of type UShort")]
        [EventMetricValue("ushort_sum", SummaryFunction.Sum, null, Caption = "UShort Sum", Description = "Data of type UShort")]
        ushort UShort { get; }

        [EventMetricValue("uint_average", SummaryFunction.Average, null, Caption = "UInt Average", Description = "Data of type UInt", IsDefaultValue = true)]
        [EventMetricValue("uint_sum", SummaryFunction.Sum, null, Caption = "UInt Sum", Description = "Data of type UInt")]
        uint UInt { get; }

        [EventMetricValue("ulong_average", SummaryFunction.Average, null, Caption = "ULong Average", Description = "Data of type ULong")]
        [EventMetricValue("ulong_sum", SummaryFunction.Sum, null, Caption = "ULong Sum", Description = "Data of type ULong")]
        ulong ULong { get; }
    }

    [EventMetric("EventMetricsByAttributesTests", "Attributes.Event Metric Data", "IEventMetricThree", Caption = "Event metric Three", Description = "Third event metric defined on an interface.")]
    public interface IEventMetricThree : IEventMetricFour
    {
        [EventMetricValue("double_average", SummaryFunction.Average, null, Caption = "Double Average", Description = "Data of type Double")]
        [EventMetricValue("double_sum", SummaryFunction.Sum, null, Caption = "Double Sum", Description = "Data of type Double")]
        double Double { get; }

        [EventMetricValue("float_average", SummaryFunction.Average, null, Caption = "Float Average", Description = "Data of type Float")]
        [EventMetricValue("float_sum", SummaryFunction.Sum, null, Caption = "Float Sum", Description = "Data of type Float")]
        float Float { get; }
    }

    [EventMetric("EventMetricsByAttributesTests", "Attributes.Event Metric Data", "IEventMetricFour", Caption = "Event metric Four", Description = "Fourth event metric defined on an interface.")]
    public interface IEventMetricFour
    {
        [EventMetricValue("string", SummaryFunction.Count, null, Caption = "String", Description = "Data of type String")]
        string String { get; }
    }
}