using System;
using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.Metrics
{
    public interface IProxyActionExecutedContext
    {
        Exception Exception { get; }
        HttpContext HttpContext { get; }
    }
    
    public interface IProxyActionExecutingContext
    {
        Exception Exception { get; }
        HttpContext HttpContext { get; }
    }
}