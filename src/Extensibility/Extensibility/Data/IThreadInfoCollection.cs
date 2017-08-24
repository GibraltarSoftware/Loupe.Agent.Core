using System;
using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A read-only collection of information describing the threads loaded by the session.
    /// </summary>
    public interface IThreadInfoCollection : IList<IThreadInfo>
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
        new void Add(IThreadInfo item);

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void Remove(IThreadInfo item);

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void Insert(int index, IThreadInfo item);

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void RemoveAt(int index);
    }
}