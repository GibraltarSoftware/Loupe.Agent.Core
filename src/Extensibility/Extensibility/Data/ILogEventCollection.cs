using System;
using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A read-only collection of information describing one or more log events.
    /// </summary>
    public interface ILogEventCollection : IList<ILogEvent>
    {
        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void Clear();

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void Add(ILogEvent item);

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void Remove(ILogEvent item);

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void Insert(int index, ILogEvent item);

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void RemoveAt(int index);
    }
}
