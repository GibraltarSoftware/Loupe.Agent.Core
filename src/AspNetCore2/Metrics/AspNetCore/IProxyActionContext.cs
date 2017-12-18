using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Metrics.AspNetCore
{
    public interface IProxyActionContext
    {
        HttpContext HttpContext { get; }
    }
}