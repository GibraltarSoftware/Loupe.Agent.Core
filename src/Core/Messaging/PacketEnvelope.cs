namespace Gibraltar.Messaging
{
    /// <summary>
    /// Wraps a Gibraltar Packet for publishing
    /// </summary>
    /// <remarks>For thread safety, request a lock on this object directly.  This is necessary when accessing updateable properties.</remarks>
    internal class PacketEnvelope
    {
        private readonly IMessengerPacket m_Packet;
        private readonly bool m_IsCommand;
        private readonly bool m_IsHeader;
        private readonly bool m_WriteThrough;
        private bool m_Committed;
        private bool m_Pending;


        //public event EventHandler PacketCommitted;

        public PacketEnvelope(IMessengerPacket packet, bool writeThrough)
        {
            m_Packet = packet;
            m_WriteThrough = writeThrough;

            if (packet is CommandPacket)
            {
                m_IsCommand = true;
            }
            else
            {
                m_IsCommand = false;
            }

            ICachedMessengerPacket cachedPacket = packet as ICachedMessengerPacket;
            if (cachedPacket != null)
            {
                m_IsHeader = cachedPacket.IsHeader;
            }
        }

        /// <summary>
        /// True if the packet is a command packet, false otherwise.
        /// </summary>
        public bool IsCommand { get { return m_IsCommand; } }

        /// <summary>
        /// True if the packet is a header cached packet, false otherwise.
        /// </summary>
        public bool IsHeader { get { return m_IsHeader; } }

        /// <summary>
        /// True if the packet has been commited, false otherwise
        /// </summary>
        /// <remarks>This property is thread safe and will pulse waiting threads when it is set to true.
        /// This property functions as a latch and can't be set false once it has been set to true.</remarks>
        public bool IsCommitted
        {
            get
            {
                return m_Committed;
            }
            set
            {
                lock (this)
                {
                    //we can't set committed to false, only true.
                    if ((value) && (m_Committed == false))
                    {
                        m_Committed = true;
                    }

                    System.Threading.Monitor.PulseAll(this);
                }
            }
        }

        /// <summary>
        /// True if the packet is pending submission to the queue, false otherwise
        /// </summary>
        /// <remarks>This property is thread safe and will pulse waiting threads when changed.</remarks>
        public bool IsPending
        {
            get
            {
                return m_Pending;
            }
            set
            {
                lock (this)
                {
                    //are they changing the value?
                    if (value != m_Pending)
                    {
                        m_Pending = value;
                    }

                    System.Threading.Monitor.PulseAll(this); 
                }
            }
        }
        /// <summary>
        /// The actual Gibraltar Packet
        /// </summary>
        public IMessengerPacket Packet { get { return m_Packet; } }

        /// <summary>
        /// True if the client is waiting for the packet to be written before returning.
        /// </summary>
        public bool WriteThrough { get { return m_WriteThrough; } }

    }
}
