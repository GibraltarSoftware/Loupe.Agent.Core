using System;

namespace Loupe.Core.Monitor.Serialization
{
    /// <summary>
    /// Implemented by any extensible data object to connect to its unique Id which it shares with its extension object.
    /// </summary>
    public interface IDataObject
    {
        /// <summary>
        /// The unique Id of the data object which it shares with its extension object.
        /// </summary>
        Guid Id { get; }
    }
}
