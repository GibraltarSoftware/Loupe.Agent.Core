using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Gibraltar.Monitor.Serialization;

#pragma warning disable 1591
namespace Gibraltar.Monitor
{
    [DebuggerDisplay("(Thread #{ThreadIndex} : {Description})")]
    public class ThreadInfo : IDisplayable, IComparable<ThreadInfo>, IEquatable<ThreadInfo>
    {
        private readonly ThreadInfoPacket m_Packet;
        private volatile int m_ThreadInstance; // Used to distinguish threads with the same name.
        private volatile string m_Caption;
        private volatile string m_Description;

        public event PropertyChangedEventHandler PropertyChanged;

        public ThreadInfo()
        {
            m_Packet = new ThreadInfoPacket();
            m_Packet.ThreadIndex = ThreadToken.GetCurrentThreadIndex(); // Each ThreadInfo we create gets a unique index value.

            Thread thread = Thread.CurrentThread;
            m_Packet.ThreadId = thread.ManagedThreadId; // These can get recycled, so they aren't domain-lifetime unique.
            m_Packet.ThreadName = thread.Name ?? string.Empty; // prevent null, but let empty name pass through
            m_Packet.DomainId = 0;
            m_Packet.DomainName = string.Empty;
            m_Packet.IsBackground = thread.IsBackground;
            m_Packet.IsThreadPoolThread = false;
            m_ThreadInstance = 0;
            m_Caption = null;
            m_Description = null;
        }

        internal ThreadInfo(ThreadInfoPacket packet)
        {
            m_Packet = packet;
            m_ThreadInstance = 0;
            m_Caption = null;
            m_Description = null;
        }

        #region Public Properties and Methods
        public string Caption
        {
            get
            {
                if (m_Caption == null)
                {
                    StringBuilder buffer = new StringBuilder();
                    if (string.IsNullOrEmpty(m_Packet.ThreadName))
                    {
                        buffer.AppendFormat("Thread {0}", m_Packet.ThreadId);
                    }
                    else
                    {
                        buffer.Append(m_Packet.ThreadName);
                    }
                    
                    if (m_ThreadInstance > 0)
                        buffer.AppendFormat(" #{0}", m_ThreadInstance);

                    m_Caption = StringReference.GetReference(buffer.ToString());
                }

                return m_Caption;
            }
        }

        public string Description
        {
            get
            {
                if (m_Description == null)
                {
                    StringBuilder buffer = new StringBuilder();
                    // Threads are either foreground, background, or threadpool (which are a subset of background)
                    if (m_Packet.IsBackground)
                    {
                        buffer.Append(m_Packet.IsThreadPoolThread ? "ThreadPool Thread " : "Background Thread ");
                    }
                    else
                    {
                        buffer.Append("Foreground Thread ");
                    }
                    buffer.Append(m_Packet.ThreadId);

                    if (!string.IsNullOrEmpty(m_Packet.ThreadName)) // Add specific name, if it had one
                    {
                        buffer.AppendFormat(" {0}", m_Packet.ThreadName);
                    }

                    if (m_ThreadInstance > 0)
                        buffer.AppendFormat(" #{0}", m_ThreadInstance);

                    m_Description = StringReference.GetReference(buffer.ToString());
                }

                return m_Description;
            }
        }

        public Guid Id { get { return m_Packet.ID; } }
        
        public int ThreadId { get { return m_Packet.ThreadId; } }
        
        public int ThreadIndex { get { return m_Packet.ThreadIndex; } }
        
        /// <summary>
        /// A uniquifier for display purposes (set by Analyst)
        /// </summary>
        public int ThreadInstance
        {
            get { return m_ThreadInstance; }
            set
            {
                if (m_ThreadInstance != value)
                {
                    m_ThreadInstance = value;
                    m_Caption = null; // Clear the cache so it recomputes with the new instance number.
                    m_Description = null; // Clear the cache so it recomputes with the new instance number.
                }
            }
        }

        public string ThreadName 
        { 
            get 
            {
                return m_Packet.ThreadName;
            }
            set
            {
                if (m_Packet.ThreadName != value)
                {
                    m_Packet.ThreadName = value;

                    //and signal that we changed a property we expose
                    SendPropertyChanged("ThreadName");
                }
            }
        }

        public int DomainId { get { return m_Packet.DomainId; } }

        public string DomainName { get { return m_Packet.DomainName; } }

        public bool IsBackground { get { return m_Packet.IsBackground; } }

        public bool IsThreadPoolThread { get { return m_Packet.IsThreadPoolThread; } }

        #endregion

        internal ThreadInfoPacket Packet { get { return m_Packet; } }

        /// <summary>
        /// Is the thread this instance is about still active in memory?  Only legitimate within the session where
        /// the thread was running.  Do not query this for playback outside the original running session.
        /// </summary>
        internal bool IsStillAlive { get { return ThreadToken.IsThreadStillAlive(ThreadIndex); } }

        /// <summary>
        /// Is the thread with the specified threadIndex still active in memory?  Only legitimate within the session where
        /// the thread was running.  Do not query this for playback outside the original running session.
        /// </summary>
        /// <param name="threadIndex">The unique ThreadIndex value which the Agent assigned to the thread in question.</param>
        /// <returns>Reports true if the managed thread which was assigned the specified threadIndex still exists
        /// or has not yet garbage collected its [ThreadStatic] variables.  Reports false after garbage collection.</returns>
        internal static bool IsThreadStillAlive(int threadIndex)
        {
            return ThreadToken.IsThreadStillAlive(threadIndex);
        }

        /// <summary>
        /// Returns the unique threadIndex value assigned to the current thread.
        /// </summary>
        /// <returns>The threadIndex value for the current thread which is unique across the life of this log session.</returns>
        internal static int GetCurrentThreadIndex()
        {
            return ThreadToken.GetCurrentThreadIndex();
        }

        #region Private Properties and Methods

        private void SendPropertyChanged(String propertyName)
        {
            if ((PropertyChanged != null))
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region IComparable and IEquatable Methods

        /// <summary>
        /// Compares this ThreadInfo object to another to determine sorting order.
        /// </summary>
        /// <remarks>ThreadInfo instances are sorted by their ThreadId property.</remarks>
        /// <param name="other">The other ThreadInfo object to compare this object to.</param>
        /// <returns>An int which is less than zero, equal to zero, or greater than zero to reflect whether
        /// this ThreadInfo should sort as being less-than, equal to, or greater-than the other
        /// ThreadInfo, respectively.</returns>
        public int CompareTo(ThreadInfo other)
        {
            if (ReferenceEquals(other, null))
                return 1; // We're not null, so we're greater than anything that is null.

            if (ReferenceEquals(this, other))
                return 0; // Refers to the same instance, so obviously we're equal.

            // But in general, we compare first based on ThreadId.
            int compare = ThreadId.CompareTo(other.ThreadId);

            // Unfortunately, ThreadId isn't as unique as we thought, so do some follow-up compares.
            if (compare == 0)
                compare = m_Packet.ThreadIndex.CompareTo(other.ThreadIndex);

            if (compare == 0)
                compare = m_Packet.Timestamp.CompareTo(other.Packet.Timestamp);

            if (compare == 0)
                compare = m_Packet.Sequence.CompareTo(other.Packet.Sequence);

            if (compare == 0)
                compare = Id.CompareTo(other.Id); // Finally, compare by Guid if we have to.

            return compare;
        }

        /// <summary>
        /// Determines if the provided ThreadInfo object is identical to this object.
        /// </summary>
        /// <param name="other">The ThreadInfo object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(ThreadInfo other)
        {
            if (CompareTo(other) == 0)
                return true;

            return false;
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="obj">The object to compare this object to</param>
        /// <returns>True if the other object is also a ThreadInfo and represents the same data.</returns>
        public override bool Equals(object obj)
        {
            ThreadInfo otherThreadInfo = obj as ThreadInfo;

            return Equals(otherThreadInfo); // Just have type-specific Equals do the check (it even handles null)
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
            // We can't use timestamp or sequence here because they might not be set initially (or with older Agent versions)
            // and bad things could happen if the hash code changes while it's placed in a hashed collection.  So use Guid.
            int myHash = Id.GetHashCode(); // Guid is guaranteed to exist and remain constant.

            return myHash;
        }

        /// <summary>
        /// Compares two ThreadInfo instances for equality.
        /// </summary>
        /// <param name="left">The ThreadInfo to the left of the operator</param>
        /// <param name="right">The ThreadInfo to the right of the operator</param>
        /// <returns>True if the two ThreadInfos are equal.</returns>
        public static bool operator ==(ThreadInfo left, ThreadInfo right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return ReferenceEquals(right, null);
            }
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two ThreadInfo instances for inequality.
        /// </summary>
        /// <param name="left">The ThreadInfo to the left of the operator</param>
        /// <param name="right">The ThreadInfo to the right of the operator</param>
        /// <returns>True if the two ThreadInfos are not equal.</returns>
        public static bool operator !=(ThreadInfo left, ThreadInfo right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return ! ReferenceEquals(right, null);
            }
            return ! left.Equals(right);
        }

        /// <summary>
        /// Compares if one ThreadInfo instance should sort less than another.
        /// </summary>
        /// <param name="left">The ThreadInfo to the left of the operator</param>
        /// <param name="right">The ThreadInfo to the right of the operator</param>
        /// <returns>True if the ThreadInfo to the left should sort less than the ThreadInfo to the right.</returns>
        public static bool operator <(ThreadInfo left, ThreadInfo right)
        {
            return (left.CompareTo(right) < 0);
        }

        /// <summary>
        /// Compares if one ThreadInfo instance should sort greater than another.
        /// </summary>
        /// <param name="left">The ThreadInfo to the left of the operator</param>
        /// <param name="right">The ThreadInfo to the right of the operator</param>
        /// <returns>True if the ThreadInfo to the left should sort greater than the ThreadInfo to the right.</returns>
        public static bool operator >(ThreadInfo left, ThreadInfo right)
        {
            return (left.CompareTo(right) > 0);
        }

        #endregion

        #region Private Subclass ThreadToken

        /// <summary>
        /// A class to help detect when a managed thread no longer exists.
        /// </summary>
        private class ThreadToken
        {
            [ThreadStatic] private static ThreadToken t_ThreadToken;

            private static int s_LatestThreadIndex; // Only modify by Interlocked.Increment().
            private static readonly Dictionary<int, WeakReference> ThreadTokenMap = new Dictionary<int, WeakReference>();
            private static readonly object MapLock = new object(); // Lock for ThreadTokenMap.

            private readonly int m_ThreadIndex;

            /// <summary>
            /// This class can not be instantiated elsewhere.
            /// </summary>
            private ThreadToken(int threadIndex)
            {
                m_ThreadIndex = threadIndex;
            }

            /// <summary>
            /// Get the unique-within-this-session ThreadIndex value.
            /// </summary>
            private int ThreadIndex { get { return m_ThreadIndex; } }

            /// <summary>
            /// Register the current thread so that we can detect when it no longer exists, and return its unique ThreadIndex.
            /// </summary>
            /// <returns>The unique ThreadIndex value assigned to the current thread.</returns>
            internal static int GetCurrentThreadIndex()
            {
                int index;
                if (t_ThreadToken == null)
                {
                    index = GetNextThreadIndex();
                    ThreadToken newToken = new ThreadToken(index);
                    lock (MapLock)
                    {
                        ThreadTokenMap[index] = new WeakReference(newToken);
                    }
                    t_ThreadToken = newToken;
                }
                else
                {
                    // This shouldn't normally happen, but if they call us again from the same thread....
                    index = t_ThreadToken.ThreadIndex;
                }

                return index;
            }

            /// <summary>
            /// Determine whether an identifed thread likely still exists or definitely no longer exists in this process.
            /// </summary>
            /// <param name="threadIndex">The unique ThreadIndex value which the Agent assigned to the thread in question.</param>
            /// <returns>Reports true if the managed thread which was assigned the specified threadIndex still exists
            /// or has not yet garbage collected its [ThreadStatic] variables.  Reports false after garbage collection.</returns>
            internal static bool IsThreadStillAlive(int threadIndex)
            {
                WeakReference reference;
                bool alive;
                lock (MapLock)
                {
                    alive = ThreadTokenMap.TryGetValue(threadIndex, out reference);
                }
                if (alive)
                    alive = (reference != null && reference.IsAlive);

                return alive;
            }

            /// <summary>
            /// Get an integer to identify a thread uniquely across the entire life of this session.
            /// </summary>
            /// <returns>A unique index which will not be reused within the life of this session.</returns>
            private static int GetNextThreadIndex()
            {
                int nextIndex = Interlocked.Increment(ref s_LatestThreadIndex); // Thread-safe Increment and read the new value.
                return nextIndex;
            }
        }

        #endregion
    }
}
