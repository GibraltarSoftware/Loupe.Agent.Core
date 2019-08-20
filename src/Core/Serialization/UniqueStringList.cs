
using System;
using System.Diagnostics;



#pragma warning disable 1591
namespace Loupe.Serialization
{
    /// <summary>
    /// Helper class used by FieldReader and FieldWriter to maintain a cache of unique strings that have
    /// been read/written.
    /// <remarks>
    /// Provides a faster way to store string tokens both maintaining the order that they were added and
    /// providing a fast lookup.
    /// 
    /// Based on code developed by ewbi at http://ewbi.blogs.com/develops/2006/10/uniquestringlis.html
    /// </remarks>
    /// </summary>
    public sealed class UniqueStringList
    {
        #region Static

        private const float LoadFactor = .72f;

        // Based on Golden Primes (as far as possible from nearest two powers of two)
        // at http://planetmath.org/encyclopedia/GoodHashTablePrimes.html
        private static readonly int[] primeNumberList = new int[]
            {
                // 193, 769, 3079, 12289, 49157 removed to allow quadrupling of bucket table size
                // for smaller size then reverting to doubling
                389, 1543, 6151, 24593, 98317, 196613, 393241, 786433, 1572869, 3145739, 6291469,
                12582917, 25165843, 50331653, 100663319, 201326611, 402653189, 805306457, 1610612741
            };

        #endregion Static

        #region Fields

        private int m_BucketListCapacity;
        private int[] m_Buckets;
        private int m_LoadLimit;
        private int m_PrimeNumberListIndex;
        private string[] m_StringList;
        private int m_StringListIndex;
        private int m_CommittedStringListIndex;

        #endregion Fields

        // Extra odds and ends for handling DateTime field encoding
        #region DateTime Extras

        private DateTime m_ReferenceTime = DateTime.MinValue;
        private DateTime m_CommittedReferenceTime = DateTime.MinValue;
        private Int64 m_GenericFactor = 1; // 1 by default (equivalent to .NET Ticks)
        private Int64 m_CommittedGenericFactor = 1;

        public DateTime ReferenceTime { get { return m_ReferenceTime; } set { m_ReferenceTime = value; } }
        public Int64 GenericFactor { get { return m_GenericFactor; } set { m_GenericFactor = value; } }

        #endregion

        #region Constructors

        public UniqueStringList()
        {
            m_BucketListCapacity = primeNumberList[m_PrimeNumberListIndex++];
            m_StringList = new string[m_BucketListCapacity];
            m_Buckets = new int[m_BucketListCapacity];
            m_LoadLimit = (int)(m_BucketListCapacity * LoadFactor);
        }

        #endregion Constructors

        #region Properties

        public string this[int index] { get { return m_StringList[index]; } }

        public int Count { get { return m_StringListIndex; } }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Clears this string table and frees associated unused memory.
        /// Note that method also packs the StringReference table and invokes a GC.Collect.
        /// </summary>
        public void Clear()
        {
            m_StringListIndex = 0;
            m_PrimeNumberListIndex = 0;
            m_BucketListCapacity = primeNumberList[m_PrimeNumberListIndex++];
            m_StringList = new string[m_BucketListCapacity];
            m_Buckets = new int[m_BucketListCapacity];
            m_LoadLimit = (int)(m_BucketListCapacity * LoadFactor);
            StringReference.Pack();
            GC.Collect();
        }

        public void Commit()
        {
            m_CommittedStringListIndex = m_StringListIndex;
            m_CommittedReferenceTime = m_ReferenceTime;
            m_CommittedGenericFactor = m_GenericFactor;
        }

        public void Abort()
        {
            m_StringListIndex = m_CommittedStringListIndex;
            m_ReferenceTime = m_CommittedReferenceTime;
            m_GenericFactor = m_CommittedGenericFactor;
        }


        public int AddOrGet(string value)
        {
            int bucketIndex = GetBucketIndex(value);
            int index = m_Buckets[bucketIndex];
            if (index == 0)
            {
                //we missed - we need to add this to our list to the next spot.
                m_StringList[m_StringListIndex++] = value;
                m_Buckets[bucketIndex] = m_StringListIndex;
                if (m_StringListIndex > m_LoadLimit)
                    Expand();
                return m_StringListIndex - 1;
            }
            return index - 1;
        }

        #endregion Methods

        #region Private Methods

        private void Expand()
        {
            m_BucketListCapacity = primeNumberList[m_PrimeNumberListIndex++];
            m_Buckets = new int[m_BucketListCapacity];
            string[] newStringlist = new string[m_BucketListCapacity];
            m_StringList.CopyTo(newStringlist, 0);
            m_StringList = newStringlist;
            Reindex();
        }

        private void Reindex()
        {
            m_LoadLimit = (int)(m_BucketListCapacity * LoadFactor);
            for (int stringIndex = 0; stringIndex < m_StringListIndex; stringIndex++)
            {
                int index = GetBucketIndex(m_StringList[stringIndex]);
                m_Buckets[index] = stringIndex + 1;
            }
        }

        private int GetBucketIndex(string value)
        {
            Debug.Assert(value != null, "Adding a null string to string tables is not supported.");
            int hashCode = value.GetHashCode() & 0x7fffffff;
            int bucketIndex = hashCode % m_BucketListCapacity;
            int increment = (bucketIndex > 1) ? bucketIndex : 1;
            int i = m_BucketListCapacity;
            while (0 < i--)
            {
                int stringIndex = m_Buckets[bucketIndex];
                if (stringIndex == 0)
                    return bucketIndex;
                if (string.CompareOrdinal(value, m_StringList[stringIndex - 1]) == 0)
                    return bucketIndex;
                bucketIndex = (bucketIndex + increment) % m_BucketListCapacity; // Probe.
            }
            throw new InvalidOperationException("Failed to locate a bucket.");
        }

        #endregion Private Methods
    }
}