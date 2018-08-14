namespace Loupe.Agent.AspNetCore.Metrics
{
    public interface IProxyActionDescriptor
    {
        string DisplayName { get; }
        string ControllerName { get; }
        string ActionName { get; }
    }
}