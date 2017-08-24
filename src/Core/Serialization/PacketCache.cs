
using System;
using System.Collections;
using System.Collections.Generic;



#pragma warning disable 1591
namespace Gibraltar.Serialization
{
    public class PacketCache : IEnumerable<ICachedPacket>
    {
        private readonly List<ICachedPacket> m_Cache;
        private readonly Dictionary<Guid, int> m_Index;

        public PacketCache()
        {
            m_Cache = new List<ICachedPacket>();
            m_Index = new Dictionary<Guid, int>();
        }

        public int AddOrGet(ICachedPacket packet)
        {
            int index;
            if (m_Index.TryGetValue(packet.ID, out index))
                return index;

            index = m_Cache.Count;
            m_Cache.Add(packet);
            m_Index.Add(packet.ID, index);
            return index;
        }

        public bool Contains(ICachedPacket packet)
        {
            return m_Index.ContainsKey(packet.ID);
        }

        public int Count { get { return m_Cache.Count; } }

        public void Clear()
        {
            m_Cache.Clear();
            m_Index.Clear();
        }

        public ICachedPacket this[int index] { get { return index >= 0 && index < m_Cache.Count ? m_Cache[index] : null; } }

        public ICachedPacket this[Guid id]
        {
            get
            {
                int index;
                if (m_Index.TryGetValue(id, out index))
                    return m_Cache[index];
                else
                    return null;
            }
        }

        #region IEnumerable<ICachedPacket> Members

        IEnumerator<ICachedPacket> IEnumerable<ICachedPacket>.GetEnumerator()
        {
            return m_Cache.GetEnumerator();
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable<ICachedPacket>)this).GetEnumerator();
        }

        #endregion
    }
}