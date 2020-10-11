using System.Security.Principal;
using Gibraltar.Agent;
using Loupe.Agent.AspNetCore.Models;

namespace Loupe.Agent.AspNetCore.Infrastructure
{
    public interface ILoupeLog
    {
        void Write(LogMessageSeverity severity, string system, IMessageSourceProvider messageSource, IPrincipal? user, JavaScriptException? jsException, LogWriteMode mode, string detailsBlock, string? category, string? caption, string? description, object[]? parameters);
    }
    
    internal class DefaultLoupeLog : ILoupeLog
    {
        public void Write(LogMessageSeverity severity, string system, IMessageSourceProvider messageSource, IPrincipal? user, JavaScriptException? jsException,
            LogWriteMode mode, string detailsBlock, string? category, string? caption, string? description, object[]? parameters)
        {
            Log.Write(severity, system, messageSource, user, jsException, mode, detailsBlock, category, caption, description, parameters);
        }
    }
}