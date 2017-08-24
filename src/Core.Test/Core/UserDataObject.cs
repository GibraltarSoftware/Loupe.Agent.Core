using System;
using System.Globalization;



namespace Loupe.Core.Test.Core
{
    /// <summary>
    /// This is a user-provided data object, demonstrating reflection for event metrics
    /// </summary>
    // ToDo: Compile turned off, but left around in case we want to port these test cases to Agent.Test.
    //[EventMetric("EventMetricTests", "Gibraltar.Monitor.Test", "UserDataObject", "Generic user data object used for testing event metrics with a trend for every numeric value type supported.")]
    public class UserDataObject
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

        public UserDataObject(string instanceName)
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

        //[EventMetricInstanceName]
        public string InstanceName
        {
            get { return m_InstanceName; }
        }

        //[EventMetricValue("short_average", "Short Average", "Data of type Short", DefaultTrend=EventMetricValueTrend.Average)]
        //[EventMetricValue("short_sum", "Short Sum", "Data of type Short", DefaultTrend = EventMetricValueTrend.Sum)]
        //[EventMetricValue("short_runningaverage", "Short Running Average", "Data of type Short", DefaultTrend = EventMetricValueTrend.RunningAverage)]
        //[EventMetricValue("short_runningsum", "Short Running Sum", "Data of type Short", DefaultTrend = EventMetricValueTrend.RunningSum)]
        public short Short
        {
            get { return m_Short; }
            set { m_Short = value; }
        }

        //[EventMetricValue("ushort_average", "UShort Average", "Data of type UShort", DefaultTrend = EventMetricValueTrend.Average)]
        //[EventMetricValue("ushort_sum", "UShort Sum", "Data of type UShort", DefaultTrend = EventMetricValueTrend.Sum)]
        public ushort UShort
        {
            get { return m_UShort; }
            set { m_UShort = value; }
        }

        //[EventMetricValue("int_average", "Int Average", "Data of type Int", DefaultTrend = EventMetricValueTrend.Average, IsDefaultValue = true)]
        //[EventMetricValue("int_sum", "Int Sum", "Data of type Int", DefaultTrend = EventMetricValueTrend.Sum)]
        public int Int
        {
            get { return m_Int; }
            set { m_Int = value; }
        }

        //[EventMetricValue("uint_average", "UInt Average", "Data of type UInt", DefaultTrend = EventMetricValueTrend.Average)]
        //[EventMetricValue("uint_sum", "UInt Sum", "Data of type UInt", DefaultTrend = EventMetricValueTrend.Sum)]
        public uint UInt
        {
            get { return m_UInt; }
            set { m_UInt = value; }
        }

        //[EventMetricValue("long_average", "Long Average", "Data of type Long", DefaultTrend = EventMetricValueTrend.Average)]
        //[EventMetricValue("long_sum", "Long Sum", "Data of type Long", DefaultTrend = EventMetricValueTrend.Sum)]
        public long Long
        {
            get { return m_Long; }
            set { m_Long = value; }
        }

        //[EventMetricValue("ulong_average", "ULong Average", "Data of type ULong", DefaultTrend = EventMetricValueTrend.Average)]
        //[EventMetricValue("ulong_sum", "ULong Sum", "Data of type ULong", DefaultTrend = EventMetricValueTrend.Sum)]
        public ulong ULong
        {
            get { return m_ULong; }
            set { m_ULong = value; }
        }

        //[EventMetricValue("decimal_average", "Decimal Average", "Data of type Decimal", DefaultTrend = EventMetricValueTrend.Average)]
        //[EventMetricValue("decimal_sum", "Decimal Sum", "Data of type Decimal", DefaultTrend = EventMetricValueTrend.Sum)]
        public decimal Decimal
        {
            get { return m_Decimal; }
            set { m_Decimal = value; }
        }

        //[EventMetricValue("double_average", "Double Average", "Data of type Double", DefaultTrend = EventMetricValueTrend.Average)]
        //[EventMetricValue("double_sum", "Double Sum", "Data of type Double", DefaultTrend = EventMetricValueTrend.Sum)]
        public double Double
        {
            get { return m_Double; }
            set { m_Double = value; }
        }

        //[EventMetricValue("float_average", "Float Average", "Data of type Float", DefaultTrend = EventMetricValueTrend.Average)]
        //[EventMetricValue("float_sum", "Float Sum", "Data of type Float", DefaultTrend = EventMetricValueTrend.Sum)]
        public float Float
        {
            get { return m_Float; }
            set { m_Float = value; }
        }

        //[EventMetricValue("timespan_average", "TimeSpan Average", "Data of type TimeSpan", DefaultTrend = EventMetricValueTrend.Average)]
        //[EventMetricValue("timespan_sum", "TimeSpan Sum", "Data of type TimeSpan", DefaultTrend = EventMetricValueTrend.Sum)]
        //[EventMetricValue("timespan_runningaverage", "TimeSpan Running Average", "Data of type TimeSpan represented as a running average.", DefaultTrend = EventMetricValueTrend.RunningAverage)]
        //[EventMetricValue("timespan_runningsum", "TimeSpan Running Sum", "Data of type TimeSpan represented as a running sum.", DefaultTrend = EventMetricValueTrend.RunningSum)]
        public TimeSpan TimeSpan
        {
            get { return m_TimeSpan; }
            set { m_TimeSpan = value; }
        }
/*        public double TimeSpan
        {
            get { return m_TimeSpan.TotalMilliseconds; }
            set { m_TimeSpan = new TimeSpan((long)value); }
        } */

        //[EventMetricValue("string", "String", "Data of type String")]
        public string String
        {
            get { return m_String; }
            set { m_String = value; }
        }

        //[EventMetricValue("system.enum", "System.Enum", "Data of type System.Enum (a numeric enum, UserDataEnumeration")]
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

        public void SetValues(short sample, short maxSamples)
        {
            //we have to set each numeric value, and we want to demonstrate a range of values.
            m_Short = (short)(short.MinValue + (((short.MaxValue - short.MinValue) / maxSamples) * sample));
            m_UShort = (ushort)(ushort.MinValue + ((ushort.MaxValue - ushort.MinValue) / maxSamples) * sample);

            m_Int = (int)(int.MinValue + ((int.MaxValue / (maxSamples / 2)) * (sample / 2)) + ((int.MaxValue / (maxSamples / 2)) * (sample / 2)));
            m_UInt = (uint)(uint.MinValue + ((uint.MaxValue - uint.MinValue) / maxSamples) * sample);

            m_Long = (long)(long.MinValue + ((long.MaxValue / (maxSamples / 2)) * (sample / 2)) + ((long.MaxValue / (maxSamples / 2)) * (sample / 2)));
            m_ULong = (ulong)(ulong.MinValue + ((ulong.MaxValue - ulong.MinValue) / (ulong)maxSamples) * (ulong)sample);

            m_Decimal = (decimal)(decimal.MinValue + ((decimal.MaxValue / ((decimal)maxSamples / 2)) * ((decimal)sample / 2)) + ((decimal.MaxValue / ((decimal)maxSamples / 2)) * ((decimal)sample / 2)));
            
            m_Double = (double)(double.MinValue + ((double.MaxValue - double.MinValue) / maxSamples) * sample);
            
            m_Float = (float)(float.MinValue + ((float.MaxValue - float.MinValue) / maxSamples) * sample);

            m_TimeSpan = new TimeSpan(m_Long); // just use the long as a # of ticks.

            m_Enum = (UserDataEnumeration)sample;

            m_String = string.Format(CultureInfo.CurrentCulture, "The Current Sample Value Is {0}.", sample);
        }
    }
}
