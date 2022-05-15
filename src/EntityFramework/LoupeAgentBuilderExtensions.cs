using Loupe.Agent.Core.Services;

namespace Loupe.Agent.EntityFramework
{
    /// <summary>
    /// Extension method to add the EF Core diagnostic listener to Loupe.
    /// </summary>
    public static class LoupeAgentBuilderExtensions
    {
        /// <summary>
        /// Adds the Entity Framework Diagnostics
        /// </summary>
        /// <param name="builder">The <see cref="ILoupeAgentBuilder"/>.</param>
        /// <param name="configuration">Optional. The configuration for the agent.</param>
        /// <returns>The builder.</returns>
        public static ILoupeAgentBuilder AddEntityFrameworkDiagnostics(this ILoupeAgentBuilder builder, EntityFrameworkConfiguration? configuration = null)
        {
            LoupeCommandInterceptor.Register(configuration);
            return builder;
        }
    }
}