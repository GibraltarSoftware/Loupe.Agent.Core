using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Loupe.Extensibility.Data;

namespace Loupe.Extensibility
{
    /// <summary>
    /// Provides methods for accessing and manipulating a repository of sessions
    /// </summary>
    public interface IRepository
    {
        /// <summary>
        /// Raised every time the sessions collection changes.
        /// </summary>
        event CollectionChangeEventHandler CollectionChanged;

        /// <summary>
        /// A unique id for this repository.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Indicates if there are unsaved changes.
        /// </summary>
        bool IsDirty { get; }

        /// <summary>
        /// Indicates if the repository is read only (sessions can't be added or removed).
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// The unique name for this repository (typically the file name or URI).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// A short end-user caption to display for the repository.
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// An extended end-user description of the repository.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Indicates if the repository supports fragment files or not.  Most do.
        /// </summary>
        bool SupportsFragments { get; }

        /// <summary>
        /// The set of products, applications, and versions loaded into the repository
        /// </summary>
        IList<IRepositoryProduct> Products { get; }

        /// <summary>
        /// The set of all sessions in the repository.
        /// </summary>
        /// <remarks><para>This contains the summary information. To load the full contents of a
        /// a session where local data files are available use the LoadSession method.</para>
        /// <para>The supplied collection is a binding list and supports update events for the 
        /// individual sessions and contents of the repository.</para></remarks>
        ISessionSummaryCollection Sessions { get; }

        /// <summary>
        /// Retrieve the ids of the sessions files known locally for the specified session
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        IList<Guid> GetSessionFileIds(Guid sessionId);

        /// <summary>
        /// Load a session by its Id
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to be loaded.</param>
        /// <returns>A session object representing the specified session.  If no session can be
        /// found with the provided Id an exception will be thrown.</returns>
        ISession LoadSession(Guid sessionId);

        /// <summary>
        /// Get a stream for the contents of an entire session
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to retrieve the stream for.</param>
        /// <exception cref="System.IO.FileNotFoundException" caption="FileNotFoundException">Thrown if no session exists with the specified Id</exception>
        /// <returns>A stream that should be immediately copied and then disposed.  If no session could be found with the provided Id an exception will be thrown.</returns>
        Stream LoadSessionStream(Guid sessionId);

        /// <summary>
        /// Get a stream for the contents of a session file
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to retrieve the stream for.</param>
        /// <param name="fileId">The unique Id of the session file to retrieve the stream for.</param>
        /// <exception cref="System.IO.FileNotFoundException" caption="FileNotFoundException">Thrown if no session exists with the specified Id</exception>
        /// <returns>A stream that should be immediately copied and then disposed.  If no file could be found with the provided Id an exception will be thrown.</returns>
        Stream LoadSessionFileStream(Guid sessionId, Guid fileId);

        /// <summary>
        /// Remove a session from the repository and all folders by its Id
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to be removed</param>
        /// <returns>True if a session existed and was removed, false otherwise.</returns>
        /// <remarks>If no session is found with the specified Id then no exception is thrown.  Instead,
        /// false is returned.  If a session is found and removed True is returned.  If there is a problem
        /// removing the specified session (and it exists) then an exception is thrown.  The session will
        /// be removed from all folders that may reference it as well as user history and preferences.</remarks>
        bool Remove(Guid sessionId);

        /// <summary>
        /// Remove sessions from the repository and all folders by its Id
        /// </summary>
        /// <param name="sessionIds">An array of the unique Ids of the sessions to be removed</param>
        /// <returns>True if a session existed and was removed, false otherwise.</returns>
        /// <remarks>If no sessions are found with the specified Ids then no exception is thrown.  Instead,
        /// false is returned.  If at least one session is found and removed True is returned.  If there is a problem
        /// removing one or more of the specified sessions (and it exists) then an exception is thrown.  The sessions will
        /// be removed from all folders that may reference it as well as user history and preferences.</remarks>
        bool Remove(IList<Guid> sessionIds);

        /// <summary>
        /// Find if session data (more than just the header information) exists for a session with the provided Id
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to be checked.</param>
        /// <returns>True if the repository has at least some session data in the repository, false otherwise.</returns>
        bool SessionDataExists(Guid sessionId);

        /// <summary>
        /// Find if session data (more than just the header information) exists for a session with the provided Id
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to be checked.</param>
        /// <param name="fileId">The file id to look for</param>
        /// <returns>True if the repository has at least some session data in the repository, false otherwise.</returns>
        bool SessionDataExists(Guid sessionId, Guid fileId);

        /// <summary>
        /// Find if a session exists with the provided Id
        /// </summary>
        /// <param name="sessionId">The unique Id of the session to be checked.</param>
        /// <returns>True if the session exists in the repository, false otherwise.</returns>
        bool SessionExists(Guid sessionId);

        /// <summary>
        /// Set or clear the New flag for a list of sessions
        /// </summary>
        /// <param name="sessionIds">The sessions to affect</param>
        /// <param name="isNew">True to mark the sessions as new, false to mark them as not new.</param>
        /// <returns>True if a session was changed, false otherwise.</returns>
        bool SetSessionsNew(IList<Guid> sessionIds, bool isNew);

        /// <summary>
        /// Retrieves all the sessions that match the conditions defined by the specified predicate.
        /// </summary>
        /// <param name="match">The <see cref="System.Predicate{T}">Predicate</see> delegate that defines the conditions of the sessions to search for.</param>
        /// <remarks>
        /// The <see cref="System.Predicate{T}">Predicate</see> is a delegate to a method that returns true if the object passed to it matches the
        /// conditions defined in the delegate. The sessions of the repository are individually passed to the <see cref="System.Predicate{T}">Predicate</see> delegate, moving forward in the List, starting with the first session and ending with the last session.
        /// </remarks>
        /// <returns>A List containing all the sessions that match the conditions defined by the specified predicate, if found; otherwise, an empty List.</returns>
        ISessionSummaryCollection Find(Predicate<ISessionSummary> match);
    }
}
