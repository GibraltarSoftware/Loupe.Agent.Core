namespace Loupe.Agent.Core.Services
{
    public interface ILoupeAgentBuilder
    {
        ILoupeAgentBuilder AddListener<T>() where T : class, ILoupeDiagnosticListener;
    }
}