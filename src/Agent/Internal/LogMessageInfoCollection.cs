
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Gibraltar.Agent.Data;



namespace Gibraltar.Agent.Internal
{
    /// <summary>
    /// A read-only collection of info describing one or more log messages.
    /// </summary>
    internal class LogMessageInfoCollection : ReadOnlyCollection<ILogMessage>, ILogMessageCollection
    {
        internal LogMessageInfoCollection(IList<ILogMessage> messages)
            : base(messages)
        {
        }

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        public void Clear()
        {
            ((IList<ILogMessage>)this).Clear();
        }

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        public void Add(ILogMessage item)
        {
            ((IList<ILogMessage>)this).Add(item);
        }

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        public void Remove(ILogMessage item)
        {
            ((IList<ILogMessage>)this).Remove(item);
        }

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        public void Insert(int index, ILogMessage item)
        {
            ((IList<ILogMessage>)this).Insert(index, item);
        }

        /// <summary>
        /// Not a valid operation.  This is a read-only collection.
        /// </summary>
        [Obsolete("Not a valid operation.  This is a read-only collection.", true)]
        public void RemoveAt(int index)
        {
            ((IList<ILogMessage>)this).RemoveAt(index);
        }
    }
}