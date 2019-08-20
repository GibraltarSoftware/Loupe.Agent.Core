using System;
using System.Collections.Generic;
using System.Diagnostics;
using Loupe.Core.IO.Serialization;
using Loupe.Extensibility.Data;

namespace Loupe.Core.Data
{
    /// <summary>
    /// Extended information for a single application user
    /// </summary>
    [DebuggerDisplay("User {Key} : {FullyQualifiedUserName}")]
    [Obsolete("This type may be removable and ApplicationUserPacket just used directly due to IApplicationUser interface")]
    public class ApplicationUser : IComparable<ApplicationUser>, IEquatable<ApplicationUser>, IApplicationUser
    {
        private readonly ApplicationUserPacket m_Packet;

        internal ApplicationUser(string userName, DateTimeOffset timestamp, long sequence)
        {
            m_Packet = new ApplicationUserPacket
                           {
                               Timestamp = timestamp,
                               Sequence = sequence,
                               FullyQualifiedUserName = userName
                           };
        }

        internal ApplicationUser(ApplicationUserPacket packet)
        {
            m_Packet = packet;
        }

        internal ApplicationUserPacket Packet { get { return m_Packet; } }

        #region Public Properties and Methods

        /// <summary>
        /// The unique id of this application user in this session
        /// </summary>
        public Guid Id
        {
            get { return m_Packet.ID; }
        }

        /// <summary>
        /// Optional. An absolute, unique key for the user to use as a primary match
        /// </summary>
        public string Key
        {
            get { return m_Packet.Key; }
            set { m_Packet.Key = value; }
        }

        /// <summary>
        /// The fully qualified user name
        /// </summary>
        /// <remarks>If Key isn't specified this value is used as the alternate key</remarks>
        public string FullyQualifiedUserName { get { return m_Packet.FullyQualifiedUserName; } }

        /// <summary>
        /// A display label for the user (such as their full name)
        /// </summary>
        public string Caption
        {
            get { return m_Packet.Caption; }
            set { m_Packet.Caption = value; }
        }

        /// <summary>
        /// Optional.  A title for the user (e.g. job title)
        /// </summary>
        public string Title
        {
            get { return m_Packet.Title; }
            set { m_Packet.Title = value; }
        }

        /// <summary>
        /// Optional.  A primary email address for the user
        /// </summary>
        public string EmailAddress
        {
            get { return m_Packet.EmailAddress; }
            set { m_Packet.EmailAddress = value; }
        }

        /// <summary>
        /// Optional.  A phone number or other telecommunication alias
        /// </summary>
        public string Phone
        {
            get { return m_Packet.Phone; }
            set { m_Packet.Phone = value; }
        }

        /// <summary>
        /// Optional.  A label for the organization this user is a part of
        /// </summary>
        public string Organization
        {
            get { return m_Packet.Organization; }
            set { m_Packet.Organization = value; }
        }

        /// <summary>
        /// Optional.  A primary role for this user with respect to this application
        /// </summary>
        public string Role
        {
            get { return m_Packet.Role; }
            set { m_Packet.Role = value; }
        }

        /// <summary>
        /// Optional.  The primary tenant this user is a part of.
        /// </summary>
        public string Tenant
        {
            get { return m_Packet.Tenant; }
            set { m_Packet.Tenant = value; }
        }

        /// <summary>
        /// Optional.  The time zone the user is associated with
        /// </summary>
        public string TimeZoneCode
        {
            get { return m_Packet.TimeZoneCode; }
            set { m_Packet.TimeZoneCode = value; }
        }

        /// <summary>
        /// Application provided properties 
        /// </summary>
        public IDictionary<string, string> Properties { get { return m_Packet.Properties; } }


        #endregion

        #region IComparable and IEquatable Methods

        /// <summary>
        /// Compares this ApplicationUser object to another to determine sorting order.
        /// </summary>
        /// <remarks>ApplicationUser instances are sorted by their Domain then User Name properties.</remarks>
        /// <param name="other">The other ApplicationUser object to compare this object to.</param>
        /// <returns>An int which is less than zero, equal to zero, or greater than zero to reflect whether
        /// this ApplicationUser should sort as being less-than, equal to, or greater-than the other
        /// ApplicationUser, respectively.</returns>
        public int CompareTo(ApplicationUser other)
        {
            if (ReferenceEquals(other, null))
                return 1; // We're not null, so we're greater than anything that is null.

            if (ReferenceEquals(this, other))
                return 0; // Refers to the same instance, so obviously we're equal.

            //we want to sort by the domain and user name, but we don't want to let things be considered equal if they have a key missmatch..
            int compare = String.Compare(FullyQualifiedUserName, other.FullyQualifiedUserName, StringComparison.OrdinalIgnoreCase);

            if ((compare == 0) && (string.IsNullOrEmpty(Key) == false))
                compare = String.Compare(Key, other.Key, StringComparison.OrdinalIgnoreCase);

            return compare;
        }

        /// <summary>
        /// Determines if the provided ApplicationUser object is identical to this object.
        /// </summary>
        /// <param name="other">The ApplicationUser object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(ApplicationUser other)
        {
            if (CompareTo(other) == 0)
                return true;

            return false;
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="obj">The object to compare this object to</param>
        /// <returns>True if the other object is also a ApplicationUser and represents the same data.</returns>
        public override bool Equals(object obj)
        {
            var otherUser = obj as ApplicationUser;

            return Equals(otherUser); // Just have type-specific Equals do the check (it even handles null)
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
            if (string.IsNullOrEmpty(Key) == false)
                return Key.GetHashCode();

            int myHash = FullyQualifiedUserName.GetHashCode();
           
            return myHash;
        }

        /// <summary>
        /// Compares two ApplicationUser instances for equality.
        /// </summary>
        /// <param name="left">The ApplicationUser to the left of the operator</param>
        /// <param name="right">The ApplicationUser to the right of the operator</param>
        /// <returns>True if the two ApplicationUser are equal.</returns>
        public static bool operator ==(ApplicationUser left, ApplicationUser right)
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
        /// Compares two ApplicationUser instances for inequality.
        /// </summary>
        /// <param name="left">The ApplicationUser to the left of the operator</param>
        /// <param name="right">The ApplicationUser to the right of the operator</param>
        /// <returns>True if the two ApplicationUser are not equal.</returns>
        public static bool operator !=(ApplicationUser left, ApplicationUser right)
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
        /// Compares if one ApplicationUser instance should sort less than another.
        /// </summary>
        /// <param name="left">The ApplicationUser to the left of the operator</param>
        /// <param name="right">The ApplicationUser to the right of the operator</param>
        /// <returns>True if the ApplicationUser to the left should sort less than the ThreadInfo to the right.</returns>
        public static bool operator <(ApplicationUser left, ApplicationUser right)
        {
            return (left.CompareTo(right) < 0);
        }

        /// <summary>
        /// Compares if one ApplicationUser instance should sort greater than another.
        /// </summary>
        /// <param name="left">The ApplicationUser to the left of the operator</param>
        /// <param name="right">The ApplicationUser to the right of the operator</param>
        /// <returns>True if the ApplicationUser to the left should sort greater than the ApplicationUser to the right.</returns>
        public static bool operator >(ApplicationUser left, ApplicationUser right)
        {
            return (left.CompareTo(right) > 0);
        }

        #endregion
    }
}
