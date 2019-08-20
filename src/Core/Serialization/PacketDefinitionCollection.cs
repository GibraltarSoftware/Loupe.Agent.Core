
using System.Collections;
using System.Collections.Generic;



namespace Loupe.Serialization
{
    /// <summary>
    /// Helper class used by PacketRead and PacketWriter to maintain a
    /// cache of PacketDefinition instances for used IPacket types
    /// </summary>
    public sealed class PacketDefinitionList : IEnumerable<PacketDefinition>
    {
        private readonly List<PacketDefinition> m_List;
        private int m_CommittedListSize;

        /// <summary>
        /// Returns an empty list.
        /// </summary>
        public PacketDefinitionList()
        {
            m_List = new List<PacketDefinition>();
        }

        /// <summary>
        /// Gets the number of elements in the list.
        /// </summary>
        public int Count { get { return m_List.Count; } }

        /// <summary>
        /// Gets a particular item from the list.
        /// </summary>
        /// <param name="index">Zero-based index of the desired element</param>
        /// <returns>Returns a PacketDefinition if a valid index is requested, otherwise throws an exception.</returns>
        public PacketDefinition this[int index] { get { return m_List[index]; } }

        /// <summary>
        /// Gets the index of the corresponding PacketDefinition, if cached.
        /// Otherwise, returns -1.
        /// </summary>
        /// <param name="packet">IPacket object for which a PacketDefinition may be cached</param>
        public int IndexOf(IPacket packet)
        {
            // Most packets have a static field structure.  But some packets, such as EventMetrics, have a
            // dynamic set of fields determined at run-time.  In this case, we want to cache a distinct
            // PacketDefinition for each set of field definitions associated with the type.
            // Classes that require this dynamic type capability should implement IDynamicPacket which
            // will define a DynamicTypeName field on the packet.  This field will be automatically
            // serialized in the PacketDefinition and the DynamictTypeName is apppeded to the static
            // type name for purposes of indexing in this collection.
            string typeName = packet.GetType().Name;
            IDynamicPacket dynamicPacket;
            
            if ( packet is GenericPacket )
                typeName = packet.GetPacketDefinition().QualifiedTypeName;
            else if ((dynamicPacket = packet as IDynamicPacket) != null)
                typeName += "+" + dynamicPacket.DynamicTypeName;

            return IndexOf(typeName);
        }

        /// <summary>
        /// Gets the index of the corresponding PacketDefinition, if cached.
        /// Otherwise, returns -1.
        /// </summary>
        /// <param name="qualifiedTypeName">Type name of the corresponding IPacket object for which a PacketDefinition may be cached</param>
        public int IndexOf(string qualifiedTypeName)
        {
            for (int index = 0; index < m_List.Count; index++)
            {
                if (qualifiedTypeName == m_List[index].QualifiedTypeName)
                    return index;
            }
            return -1;
        }

        /// <summary>
        /// Adds a PacketDefinition to the list.
        /// </summary>
        /// <param name="item">PacketDefinition to add</param>
        /// <returns>
        /// Returns the index of the newly added item.
        /// If a PacketDefinition for this type has already been added, an exception is raised.
        /// </returns>
        public int Add(PacketDefinition item)
        {
            // check if this packet definition is already registered
            int index = IndexOf(item.QualifiedTypeName);
            if (index >= 0)
            {
                // Something is wrong, the type should only be defined once
                // But, in release builds, we want to be forgiving, so simply accept the new packet definition.
                m_List[index] = item;
                return index;
            }

            // Add the item to the end of the list and return the index.
            m_List.Add(item);
            return Count - 1;
        }

        /// <summary>
        /// This method is called after a packet is successfully written to "lock-in"
        /// any changes to state data.
        /// </summary>
        public void Commit()
        {
            m_CommittedListSize = m_List.Count;
        }

        /// <summary>
        /// This method is called if a packet write fails to undo any changes to
        /// state data that will not be available to the IPacketReader reading
        /// the stream.
        /// </summary>
        public void Rollback()
        {
            if (m_List.Count > m_CommittedListSize)
            {
                int count = m_List.Count - m_CommittedListSize;
                m_List.RemoveRange(m_CommittedListSize, count);
            }
        }

        #region IEnumerable<PacketDefinition> Members

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        IEnumerator<PacketDefinition> IEnumerable<PacketDefinition>.GetEnumerator()
        {
            return m_List.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable<PacketDefinition>)this).GetEnumerator();
        }

        #endregion
    }
}