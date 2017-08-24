using System;
using System.Collections.Generic;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A standard collection for session summaries that provides indexing by session id
    /// </summary>
    public interface ISessionSummaryCollection: IList<ISessionSummary>
    {
        /// <summary>
        /// get the item with the specified key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        ISessionSummary this[Guid key] { get; }

        /// <summary>
        /// Indicates if the collection contains the key
        /// </summary>
        /// <param name="key"></param>
        /// <returns>True if a session summary with the key exists in the collection, false otherwise.</returns>
        bool Contains(Guid key);

        /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the first occurrence within the entire List.</summary>
        /// <param name="match">The <see cref="System.Predicate{T}">Predicate</see> delegate that defines the conditions of the elements to search for.</param>
        /// <remarks>
        /// The <see cref="System.Predicate{T}">Predicate</see> is a delegate to a method that returns true if the object passed to it matches the
        /// conditions defined in the delegate. The elements of the current List are individually passed to the <see cref="System.Predicate{T}">Predicate</see> delegate, moving forward in the List, starting with the first element and ending with the last element. Processing is
        /// stopped when a match is found.
        /// </remarks>
        /// <returns>The first element that matches the conditions defined by the specified predicate, if found; otherwise, null.</returns>
        /// <exception caption="Argument Null Exception" cref="System.ArgumentNullException">match is a null reference (Nothing in Visual Basic)</exception>
        ISessionSummary Find(Predicate<ISessionSummary> match);

        /// <summary>
        /// Retrieves all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The <see cref="System.Predicate{T}">Predicate</see> delegate that defines the conditions of the elements to search for.</param>
        /// <remarks>
        /// The <see cref="System.Predicate{T}">Predicate</see> is a delegate to a method that returns true if the object passed to it matches the
        /// conditions defined in the delegate. The elements of the current List are individually passed to the <see cref="System.Predicate{T}">Predicate</see> delegate, moving forward in the List, starting with the first element and ending with the last element.
        /// </remarks>
        /// <returns>A List containing all the elements that match the conditions defined by the specified predicate, if found; otherwise, an empty List.</returns>
        ISessionSummaryCollection FindAll(Predicate<ISessionSummary> match);

        /// <summary>
        /// Removes the first occurrence of a specified object
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool Remove(Guid key);

        /// <summary>
        /// Attempt to get the item with the specified key, returning true if it could be found
        /// </summary>
        /// <returns>True if the item could be found, false otherwise</returns>
        bool TryGetValue(Guid key, out ISessionSummary item);
    }
}
