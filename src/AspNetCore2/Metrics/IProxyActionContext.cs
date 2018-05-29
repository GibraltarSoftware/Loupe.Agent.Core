using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Metrics
{
    public interface IProxyActionContext
    {
        HttpContext HttpContext { get; }
    }
}