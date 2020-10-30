using Loupe.Agent.AspNetCore.Models;

namespace Loupe.Agent.AspNetCore.DetailBuilders
{
    public class ClientDetailsBuilder:DetailsBuilderBase
    {
        public string? Build(LogRequest logRequest)
        {
            return logRequest.Session?.Client != null
                ? ObjectToXmlString(logRequest.Session.Client)
                : null;
        }         
    }
}