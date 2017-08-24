using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A single product
    /// </summary>
    public interface IRepositoryProduct
    {
        /// <summary>
        /// The product name (will be unique)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// A display caption for this product (may not be unique)
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// The set of applications for this product.
        /// </summary>
        IList<IRepositoryApplication> Applications { get; }
    }
}
