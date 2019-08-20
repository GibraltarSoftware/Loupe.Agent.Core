namespace Loupe.Core
{
    /// <summary>
    /// The different possible actions that were performed on a collection
    /// </summary>
    public enum CollectionAction
    {
        /// <summary>
        /// No changes were made.
        /// </summary>
        NoChange = 0,

        /// <summary>
        /// An item was added to the collection.
        /// </summary>
        Added = 1,

        /// <summary>
        /// An item was removed from the collection.
        /// </summary>
        Removed = 2,

        /// <summary>
        /// An item was updated in the collection.
        /// </summary>
        Updated = 3,

        /// <summary>
        /// The entire collection was cleared.
        /// </summary>
        Cleared = 4
    }
}
