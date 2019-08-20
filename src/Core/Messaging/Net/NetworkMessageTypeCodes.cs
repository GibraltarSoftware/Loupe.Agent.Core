#pragma warning disable 1591
namespace Loupe.Core.Messaging.Net
{
    public enum NetworkMessageTypeCode
    {
        Unknown = 0,
        LiveViewStartCommand = 1,
        LiveViewStopCommand = 2,
        SendSession = 3,
        SessionHeader = 4,
        GetSessionHeaders = 5,
        RegisterAnalystCommand = 6,
        RegisterAgentCommand = 7,
        SessionClosed = 8,
        PacketStreamStartCommand = 9,

        /// <summary>
        /// Measures the clock drift and latency between two computers
        /// </summary>
        ClockDrift = 10
    }
}
