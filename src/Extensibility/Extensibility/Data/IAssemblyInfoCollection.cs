using System;
using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A read-only collection of information describing the assemblies loaded by the session.
    /// </summary>
    public interface IAssemblyInfoCollection : IList<IAssemblyInfo>
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
        new void Add(IAssemblyInfo item);

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void Remove(IAssemblyInfo item);

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void Insert(int index, IAssemblyInfo item);

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        new void RemoveAt(int index);
    }
}