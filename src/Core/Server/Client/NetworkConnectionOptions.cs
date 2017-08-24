using System;
using System.Diagnostics;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Connection options used to establish a socket from the local system to an endpoint
    /// </summary>
    [DebuggerDisplay("{HostName}:{Port} UseSsl: {UseSsl}")]
    public class NetworkConnectionOptions : IComparable<NetworkConnectionOptions>, IEquatable<NetworkConnectionOptions>
    {
        private volatile int m_HashCode;

        /// <summary>
        /// The TCP Port to connect to
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// The host name or IP Address to connect to 
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// Indicates if the connection should be encrypted using Ssl or not.
        /// </summary>
        public bool UseSsl { get; set; }

        /// <summary>
        /// Create a copy of this set of connection options
        /// </summary>
        /// <returns></returns>
        public NetworkConnectionOptions Clone()
        {
            return new NetworkConnectionOptions() {HostName = HostName, Port = Port, UseSsl = UseSsl};
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return string.Format("{0}:{1} UseSsl: {2}", HostName, Port, UseSsl);
        }

        /// <summary>
        /// Compares this ThreadInfo object to another to determine sorting order.
        /// </summary>
        /// <remarks>ThreadInfo instances are sorted by their ThreadId property.</remarks>
        /// <param name="other">The other ThreadInfo object to compare this object to.</param>
        /// <returns>An int which is less than zero, equal to zero, or greater than zero to reflect whether
        /// this ThreadInfo should sort as being less-than, equal to, or greater-than the other
        /// ThreadInfo, respectively.</returns>
        public int CompareTo(NetworkConnectionOptions other)
        {
            if (ReferenceEquals(other, null))
                return 1; // We're not null, so we're greater than anything that is null.

            if (ReferenceEquals(this, other))
                return 0; // Refers to the same instance, so obviously we're equal.

            //the most important comparison is hostname, which unfortunately can be null.
            int compare = 0;
            if (HostName != other.HostName)
            {
                //they aren't the same so dig into it...
                if (HostName == null)
                {
                    //then the other can't be null, so it's greater than us.
                    compare = -1;
                }
                else if (other.HostName == null)
                {
                    //we can't be null, so we're greater than anything that is null.
                    compare = 1;
                }
                else
                {
                    //neither of us are null so now we can do a normal string compare.
                    compare = string.Compare(HostName, other.HostName, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Unfortunately, ThreadId isn't as unique as we thought, so do some follow-up compares.
            if (compare == 0)
                compare = Port.CompareTo(other.Port);

            if (compare == 0)
                compare = UseSsl.CompareTo(other.UseSsl);

            return compare;
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>; otherwise, false.
        /// </returns>
        /// <param name="obj">The <see cref="T:System.Object"/> to compare with the current <see cref="T:System.Object"/>. 
        ///                 </param><exception cref="T:System.NullReferenceException">The <paramref name="obj"/> parameter is null.
        ///                 </exception><filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            NetworkConnectionOptions otherObject = obj as NetworkConnectionOptions;

            return Equals(otherObject); // Just have type-specific Equals do the check (it even handles null)
        }

        /// <summary>
        /// Determines if the provided NetworkConnectionOptions object is identical to this object.
        /// </summary>
        /// <param name="other">The NetworkConnectionOptions object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(NetworkConnectionOptions other)
        {
            if (CompareTo(other) == 0)
                return true;

            return false;
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
            if (m_HashCode == 0)
            {
                CalculateHash();
            }
            return m_HashCode;
        }

        /// <summary>
        /// Compares two NetworkConnectionOptions instances for equality.
        /// </summary>
        /// <param name="left">The NetworkConnectionOptions to the left of the operator</param>
        /// <param name="right">The NetworkConnectionOptions to the right of the operator</param>
        /// <returns>True if the two NetworkConnectionOptionss are equal.</returns>
        public static bool operator ==(NetworkConnectionOptions left, NetworkConnectionOptions right)
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
        /// Compares two NetworkConnectionOptions instances for inequality.
        /// </summary>
        /// <param name="left">The NetworkConnectionOptions to the left of the operator</param>
        /// <param name="right">The NetworkConnectionOptions to the right of the operator</param>
        /// <returns>True if the two NetworkConnectionOptionss are not equal.</returns>
        public static bool operator !=(NetworkConnectionOptions left, NetworkConnectionOptions right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return !ReferenceEquals(right, null);
            }
            return !left.Equals(right);
        }

        /// <summary>
        /// Compares if one NetworkConnectionOptions instance should sort less than another.
        /// </summary>
        /// <param name="left">The NetworkConnectionOptions to the left of the operator</param>
        /// <param name="right">The NetworkConnectionOptions to the right of the operator</param>
        /// <returns>True if the NetworkConnectionOptions to the left should sort less than the NetworkConnectionOptions to the right.</returns>
        public static bool operator <(NetworkConnectionOptions left, NetworkConnectionOptions right)
        {
            return (left.CompareTo(right) < 0);
        }

        /// <summary>
        /// Compares if one NetworkConnectionOptions instance should sort greater than another.
        /// </summary>
        /// <param name="left">The NetworkConnectionOptions to the left of the operator</param>
        /// <param name="right">The NetworkConnectionOptions to the right of the operator</param>
        /// <returns>True if the NetworkConnectionOptions to the left should sort greater than the NetworkConnectionOptions to the right.</returns>
        public static bool operator >(NetworkConnectionOptions left, NetworkConnectionOptions right)
        {
            return (left.CompareTo(right) > 0);
        }


        private void CalculateHash()
        {
            int myHash = UseSsl.GetHashCode();
            myHash ^= Port.GetHashCode();

            //since we are comparing without case we need to get rid of hash code variations by case.
            if (HostName != null) myHash ^= HostName.ToLowerInvariant().GetHashCode(); // Fold in hash code for string

            m_HashCode = myHash;
        }    
    }
}
