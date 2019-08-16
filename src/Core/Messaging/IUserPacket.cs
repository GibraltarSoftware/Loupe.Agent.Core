using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using Gibraltar.Monitor.Serialization;
using Gibraltar.Serialization;

namespace Gibraltar.Messaging
{
    /// <summary>
    /// Identifies a packet that can be associated with a specific user
    /// </summary>
    /// <remarks>Implemented by packets that represent user-initiated data where
    /// the user principal should be captured</remarks>
    public interface IUserPacket : IPacket
    {
        /// <summary>
        /// Optional.  The user principal this packet was initiated by.
        /// </summary>
        /// <remarks>This is set when the packet is queued</remarks>
        IPrincipal Principal { get; set; }

        /// <summary>
        /// Optional.  The Application User to attribute this packet to.
        /// </summary>
        /// <remarks>Internal Infrastructure.  This is set during the publish phase</remarks>
        ApplicationUserPacket UserPacket { get; set; }
    }
}
