using System;
using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A read-only collection of information describing one or more log messages.
    /// </summary>
    public interface ILogMessageCollection : IList<ILogMessage>
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
        new void Add(ILogMessage item);

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void Remove(ILogMessage item);

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void Insert(int index, ILogMessage item);

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void RemoveAt(int index);

        /// <summary>
        /// Retrieve a lot message by its unique identifier
        /// </summary>
        /// <param name="key"></param>
        /// <returns>The log message with the specified Id.  If none is found an exception will be thrown.</returns>
        ILogMessage this[Guid key] { get; }
    }
}