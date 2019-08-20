using Loupe.Core.IO.Serialization;
using Loupe.Core.Messaging;

namespace Loupe.Core
{
    /// <summary>
    /// Inline filter for packets evaluated for each packet
    /// </summary>
    /// <remarks>Implementations can rewrite or suppress packets.</remarks>
    public interface ILoupeFilter
    {
        /// <summary>
        /// Process the packet
        /// </summary>
        /// <param name="packet">The packet to process</param>
        /// <param name="cancel">Set to true to suppress writing this packet</param>
        void Process(IMessengerPacket packet, ref bool cancel);
    }
}
