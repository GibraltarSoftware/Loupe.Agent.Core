using System;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Metrics.AspNetCore
{
    public interface IProxyActionExecutedContext
    {
        Exception Exception { get; }
        HttpContext HttpContext { get; }
    }
}