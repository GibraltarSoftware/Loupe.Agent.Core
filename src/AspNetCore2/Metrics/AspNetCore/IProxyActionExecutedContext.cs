using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Loupe.Agent.AspNetCore.Metrics.AspNetCore
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