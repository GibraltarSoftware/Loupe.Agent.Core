using System.Security.Principal;
using Gibraltar.Agent;
using Loupe.Agent.AspNetCore.Models;

namespace Loupe.Agent.AspNetCore.Infrastructure
{
    public interface IJavaScriptLog
    {
        void Write(LogMessageSeverity severity, string system, IMessageSourceProvider messageSource, IPrincipal? user, JavaScriptException? jsException, LogWriteMode mode, string detailsBlock, string? category, string? caption, string? description, object[]? parameters);
    }
}