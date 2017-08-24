using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A single application within a product
    /// </summary>
    public interface IRepositoryApplication
    {
        /// <summary>
        /// The application name (unique within a product)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// A display caption for this application (may not be unique)
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// Optional. An extended description of this application.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The type of application (Asp.Net, Windows, Service, etc.)
        /// </summary>
        ApplicationType Type { get; }

        /// <summary>
        /// The product for this application
        /// </summary>
        IRepositoryProduct Product { get; }

        /// <summary>
        /// The set of versions for this application.
        /// </summary>
        IList<IRepositoryApplicationVersion> Versions { get; }

        /// <summary>
        /// The application user tracking mode for this application
        /// </summary>
        /// <remarks>If the user mode is set to none then no user information will be available for the application.
        /// This is typical for background applications such as console applications and services.</remarks>
        ApplicationUserMode UserMode { get; }

        /// <summary>
        /// An optimized combination of the product and application captions
        /// </summary>
        /// <remarks>Frequently the product and application captions are either identical or the application caption starts with the product caption.
        /// This property provides a combined caption that drops this duplication.</remarks>
        string ProductApplicationCaption { get; }
    }
}
