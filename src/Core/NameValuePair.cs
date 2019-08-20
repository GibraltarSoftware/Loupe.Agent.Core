using System;



namespace Loupe.Core
{
    /// <summary>
    /// An interface to get the Name of NameValuePair&lt;TValue&gt; of any value type.
    /// </summary>
    public interface IName
    {
        /// <summary>
        /// The name of the item
        /// </summary>
        string Name { get; }
    }

    /// <summary>
    /// Simple class that contains a name and corresponding value
    /// </summary>
    /// <remarks>NameValuePairs are compared to each other by Name for sorting purposes.</remarks>
    public class NameValuePair<TValue> : IName, IComparable, IComparable<IName>, IComparable<NameValuePair<TValue>>
    {
        /// <summary>
        /// The name of the item
        /// </summary>
        public string Name { get; set; }


        /// <summary>
        /// The value of the item
        /// </summary>
        public TValue Value { get; set; }


        /// <summary>
        /// Default constructor used to initialize the class
        /// </summary>
        /// <remarks>No Remarks</remarks>
        public NameValuePair()
        {
            Name = string.Empty;
            Value = default(TValue);
        }

        /// <summary>
        /// Default constructor used to initialize the class
        /// </summary>
        /// <param name="name">The name of the item</param>
        /// <param name="value">The value of the corresponding item</param>
        /// <remarks>No Remarks</remarks>
        public NameValuePair(string name, TValue value)
        {
/*            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }
*/
            Name = name;
            Value = value;
        }

        /// <summary>
        /// Returns a System.String that represents current System.Object
        /// </summary>
        /// <returns>Returns a System.String that represents current System.Object</returns>
        /// <remarks>No Remarks</remarks>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Compares this object with the passed in object, if it is an INameValuePair.
        /// </summary>
        /// <param name="obj">The other object that is to be compared with this instance.</param>
        /// <returns>A value that is less than, equal to, or greater than zero.</returns>
        public int CompareTo(object obj)
        {
            if (obj == null)
                return int.MaxValue; // We're not null, so we're greater than any null.

            IName otherPair = obj as IName;
            if (otherPair == null)
                return int.MinValue; // Invalid comparison, we can't compare against unknown types.

            return CompareTo(otherPair);
        }

        /// <summary>
        /// Compares this IName with another of any data type.
        /// </summary>
        /// <param name="other">The other IName that is to be compared with this instance.</param>
        /// <returns>A value that is less than, equal to, or greater than zero.</returns>
        public int CompareTo(IName other)
        {
            if (other == null)
                return int.MaxValue; // We're not null, so we're greater than any null.

            return Name.CompareTo(other.Name);
        }

        /// <summary>
        /// Compares this NameValuePair&lt;TValue&gt; with another with the same data type.
        /// </summary>
        /// <param name="other">The other NameValuePair&lt;TValue&gt; that is to be compared with this instance.</param>
        /// <returns>A value that is less than, equal to, or greater than zero.</returns>
        public int CompareTo(NameValuePair<TValue> other)
        {
            return CompareTo((IName)other); // Just use the broader type-cast comparison.
        }

    }
}
