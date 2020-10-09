namespace Loupe.Agent.AspNetCore.Models
{
    public class ClientSession
    {
        public ClientDetails Client { get; set; }
        public string CurrentAgentSessionId { get; set; }
    }
}