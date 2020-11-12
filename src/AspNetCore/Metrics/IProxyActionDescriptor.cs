namespace Loupe.Agent.AspNetCore.Metrics
{
    /// <summary>
    /// An interface that will be implemented dynamically by Microsoft.Extensions.Diagnostics.
    /// </summary>
    public interface IProxyActionDescriptor
    {
        /// <summary>
        /// Gets the display name.
        /// </summary>
        /// <value>
        /// The display name.
        /// </value>
        string DisplayName { get; }
        /// <summary>
        /// Gets the name of the controller.
        /// </summary>
        /// <value>
        /// The name of the controller.
        /// </value>
        string ControllerName { get; }
        /// <summary>
        /// Gets the name of the action.
        /// </summary>
        /// <value>
        /// The name of the action.
        /// </value>
        string ActionName { get; }
    }
}