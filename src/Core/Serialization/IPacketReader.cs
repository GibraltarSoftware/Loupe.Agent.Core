using System;
using System.Reflection;



#pragma warning disable 1591
namespace Loupe.Core.Serialization
{
    public interface IPacketReader
    {
        /// <summary>
        /// Returns the current position within the stream.
        /// </summary>
        long Position { get; }

        /// <summary>
        /// Returns the length of the stream.
        /// </summary>
        long Length { get; }

        /// <summary>
        /// Read and return the next IPacket from the stream
        /// </summary>
        IPacket Read();

        void RegisterType(Type type);

        void RegisterFactory(string typeName, IPacketFactory factory);

        void RegisterAssembly(Assembly assembly);
    }
}