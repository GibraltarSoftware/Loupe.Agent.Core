using System;
using System.Diagnostics;
using Loupe.Extensibility.Data;

namespace Loupe.Core.Monitor
{
    /// <summary>
    /// A single display-ready metric value.  
    /// </summary>
    /// <remarks>
    /// This is the complementary object to a Metric Sample.  A Sample is a raw value that may require multiple
    /// samples to determine a display ready value.
    /// </remarks>
    [DebuggerDisplay("{Sequence}: {Timestamp} Value={Value}")]
    public class MetricValue : IMetricValue
    {
        private readonly long m_Sequence;
        private readonly MetricValueCollection m_MetricValueCollection;
        private readonly double m_Value;
        private readonly DateTimeOffset m_TimeStamp;

        /// <summary>Create a new metric value for the specified metric value set.</summary>
        /// <remarks>The new metric value is automatically added to the provided metric value set.</remarks>
        /// <param name="metricValueCollection">The metric value set this value is part of.</param>
        /// <param name="timeStamp">The unique date and time of this value sample.</param>
        /// <param name="value">The calculated value.</param>
        public MetricValue(MetricValueCollection metricValueCollection, DateTimeOffset timeStamp, double value)
        {
            m_MetricValueCollection = metricValueCollection;
            m_TimeStamp = timeStamp;
            m_Value = value;

            //get the unique sequence number from the metric
            m_Sequence = metricValueCollection.GetSampleSequence();

            //and add ourself to the metric value set
            m_MetricValueCollection.Add(this);
        }

        #region Public Properties and Methods

        /// <summary>
        /// The metric value set this value is part of.
        /// </summary>
        public IMetricValueCollection ValueCollection { get { return m_MetricValueCollection; } }

        /// <summary>
        /// The exact date and time the metric was captured.
        /// </summary>
        public virtual DateTimeOffset Timestamp
        {
            get
            {
                return m_TimeStamp;
            }
        }

        /// <summary>
        /// The date and time the metric was captured in the effective time zone.
        /// </summary>
        public DateTime LocalTimestamp
        {
            get { return Timestamp.DateTime; }
        }

        /// <summary>
        /// The value of the metric.
        /// </summary>
        public double Value { get { return m_Value; } }

        /// <summary>
        /// The value of the metric multiplied by 100 to handle raw percentage display
        /// </summary>
        /// <remarks>This value is scaled by 100 even if the underlying metric is not a percentage</remarks>
        public double PercentageValue { get { return m_Value * 100; } }

        /// <summary>
        /// The increasing sequence number of all sample packets for this metric to be used as an absolute order sort.
        /// </summary>
        public long Sequence { get { return m_Sequence; } }

        /// <summary>
        /// Compare this metric value to another for the purpose of sorting them in time.
        /// </summary>
        /// <remarks>MetricValue instances are sorted by their Sequence number property.</remarks>
        /// <param name="other">The MetricValue object to compare this object to.</param>
        /// <returns>An int which is less than zero, equal to zero, or greater than zero to reflect whether
        /// this MetricValue should sort as being less-than, equal to, or greater-than the other
        /// MetricValue, respectively.</returns>
        public int CompareTo(IMetricValue other)
        {
            //we are all about the sequence number baby!
            return m_Sequence.CompareTo(other.Sequence);
        }

        /// <summary>
        /// Determines if the provided MetricValue object is identical to this object.
        /// </summary>
        /// <param name="other">The MetricValue object to compare this object to.</param>
        /// <returns>True if the Metric Value objects represent the same data.</returns>
        public bool Equals(IMetricValue other)
        {
            // Careful, it could be null; check it without recursion
            if (object.ReferenceEquals(other, null))
            {
                return false; // Since we're a live object we can't be equal to a null instance.
            }

            //they are equal if they have the same sequence and value
            return ((Value == other.Value) && (Sequence == other.Sequence));
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="obj">The object to compare this object to</param>
        /// <returns>True if the other object is also a MetricValue and represents the same data.</returns>
        public override bool Equals(object obj)
        {
            MetricValue otherMetricValue = obj as MetricValue;

            return Equals(otherMetricValue); // Just have type-specific Equals do the check (it even handles null)
        }

        /// <summary>
        /// Provides a representative hash code for objects of this type to spread out distribution
        /// in hash tables.
        /// </summary>
        /// <remarks>Objects which consider themselves to be Equal (a.Equals(b) returns true) are
        /// expected to have the same hash code.  Objects which are not Equal may have the same
        /// hash code, but minimizing such overlaps helps with efficient operation of hash tables.
        /// </remarks>
        /// <returns>
        /// An int representing the hash code calculated for the contents of this object.
        /// </returns>
        public override int GetHashCode()
        {
            int myHash = (int) (m_Sequence >> 32) ^ (int) m_Sequence; // Fold long Sequence number down to an int

            myHash ^= m_Value.GetHashCode(); // Fold in hash code for double field Value

            return myHash;
        }

        #endregion

        #region Static Public Methods and Operators

        /// <summary>
        /// Compares two MetricValue instances for equality.
        /// </summary>
        /// <param name="left">The MetricValue to the left of the operator</param>
        /// <param name="right">The MetricValue to the right of the operator</param>
        /// <returns>True if the two MetricValues are equal.</returns>
        public static bool operator ==(MetricValue left, MetricValue right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (object.ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return object.ReferenceEquals(right, null);
            }
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two MetricValue instances for inequality.
        /// </summary>
        /// <param name="left">The MetricValue to the left of the operator</param>
        /// <param name="right">The MetricValue to the right of the operator</param>
        /// <returns>True if the two MetricValues are not equal.</returns>
        public static bool operator !=(MetricValue left, MetricValue right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (object.ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return ! object.ReferenceEquals(right, null);
            }
            return ! left.Equals(right);
        }

        /// <summary>
        /// Compares if one MetricValue instance should sort less than another.
        /// </summary>
        /// <param name="left">The MetricValue to the left of the operator</param>
        /// <param name="right">The MetricValue to the right of the operator</param>
        /// <returns>True if the MetricValue to the left should sort less than the MetricValue to the right.</returns>
        public static bool operator <(MetricValue left, MetricValue right)
        {
            return (left.CompareTo(right) < 0);
        }

        /// <summary>
        /// Compares if one MetricValue instance should sort greater than another.
        /// </summary>
        /// <param name="left">The MetricValue to the left of the operator</param>
        /// <param name="right">The MetricValue to the right of the operator</param>
        /// <returns>True if the MetricValue to the left should sort greater than the MetricValue to the right.</returns>
        public static bool operator >(MetricValue left, MetricValue right)
        {
            return (left.CompareTo(right) > 0);
        }

        #endregion
    }
}
