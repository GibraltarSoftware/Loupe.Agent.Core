using System;
using Loupe.Core.Messaging;
using Loupe.Core.Serialization;

namespace Loupe.Core.IO.Serialization
{
    /// <summary>
    /// A command to be processed by the messaging system.
    /// </summary>
    internal class CommandPacket : IMessengerPacket
    {
        /// <summary>
        /// Create a new command packet for the provided command.
        /// </summary>
        /// <param name="command"></param>
        public CommandPacket(MessagingCommand command)
        {
            Command = command;
            State = null;
        }

        /// <summary>
        /// Create a new command packet for the provided command, with state.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="state"></param>
        public CommandPacket(MessagingCommand command, object state)
        {
            Command = command;
            State = state;
        }

        /// <summary>
        /// The command to execute
        /// </summary>
        public MessagingCommand Command { get; private set; }

        /// <summary>
        /// Optional.  State arguments for the command
        /// </summary>
        public object State { get; private set; }

        public DateTimeOffset Timestamp { get; set; }
        public long Sequence { get; set; }

        //While we have to implement all of this serialization stuff, command packets should never
        //actually be serialized, so they are all about throwing exceptions.
        IPacket[] IPacket.GetRequiredPackets()
        {
            throw new NotSupportedException();
        }

        PacketDefinition IPacket.GetPacketDefinition()
        {
            throw new NotSupportedException();
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            throw new NotSupportedException();
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            throw new NotSupportedException();
        }
    }

}
