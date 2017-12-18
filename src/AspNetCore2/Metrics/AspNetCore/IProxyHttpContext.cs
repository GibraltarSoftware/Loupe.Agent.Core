using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Metrics.AspNetCore
{
    public interface IProxyHttpContext
    {
        string TraceIdentifier { get; }
        HttpRequest Request { get; }
        HttpResponse Response { get; }
    }
}