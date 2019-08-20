using Loupe.Server.Client.Data;

namespace Loupe.Server.Client
{
    /// <summary>
    /// The current status of a server that is accessible over the network
    /// </summary>
    public enum HubStatus
    {
        /// <summary>
        /// The current status couldn't be determined.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The server is accessible and operational.
        /// </summary>
        Available = HubStatusXml.available,

        /// <summary>
        /// The server has no license and should not be communicated with.
        /// </summary>
        Expired = HubStatusXml.expired,

        /// <summary>
        /// The server is currently undergoing maintenance and is not operational.
        /// </summary>
        Maintenance = HubStatusXml.maintenance,
    }
}
