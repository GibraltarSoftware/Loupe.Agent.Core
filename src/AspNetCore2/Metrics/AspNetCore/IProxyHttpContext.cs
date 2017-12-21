using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Loupe.Agent.AspNetCore.Metrics.AspNetCore
{
    public interface IProxyHttpContext
    {
        string TraceIdentifier { get; }
        HttpRequest Request { get; }
        HttpResponse Response { get; }
        IFeatureCollection Features { get; }
    }
}