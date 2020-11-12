#pragma warning disable 1591

using System.ComponentModel;

namespace Loupe.Agent.AspNetCore.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ClientSession
    {
        public ClientDetails? Client { get; set; }
        public string? CurrentAgentSessionId { get; set; }
    }
}