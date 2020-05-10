using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Gibraltar.Serialization;
using Loupe.Core.Test;
using NUnit.Framework;


namespace Loupe.Core.Serialization.UnitTests
{
    [TestFixture]
    public class PacketSerializationTests : IDisposable
    {
        private MemoryStream m_MemoryStream;
        private IPacketWriter m_PacketWriter;
        private IPacketReader m_PacketReader;
        private bool m_WeAreDisposed;

        [SetUp]
        public void Setup()
        {
            m_MemoryStream = new MemoryStream();
            m_PacketWriter = new PacketWriter(m_MemoryStream);
            m_PacketReader = new PacketReader(m_MemoryStream, false);
        }

        [Test]
        public void CheckWriting1Packet()
        {
            m_PacketReader.RegisterType(typeof(WrapperPacket));
            WrapperPacket writtenPacket = new WrapperPacket("Test1", 100);
            m_PacketWriter.Write(writtenPacket);
            Assert.AreEqual(108, m_MemoryStream.Position);
            m_MemoryStream.Position = 0;
            IPacket readPacket = m_PacketReader.Read();
            Assert.AreEqual(writtenPacket, readPacket);
        }

        [Test]
        public void CheckWriting2Packets()
        {
            m_PacketReader.RegisterType(typeof(WrapperPacket));
            WrapperPacket packet1 = new WrapperPacket("Test1", 100);
            WrapperPacket packet2 = new WrapperPacket("Test2", 200);
            m_PacketWriter.Write(packet1);
            Assert.AreEqual(108, m_MemoryStream.Position);
            m_PacketWriter.Write(packet2);
            Assert.AreEqual(126, m_MemoryStream.Position);
            m_MemoryStream.Position = 0;
            Assert.AreEqual(packet1, m_PacketReader.Read());
            Assert.AreEqual(packet2, m_PacketReader.Read());
        }

        [Test]
        public void PacketCacheTest()
        {
            m_PacketReader.RegisterType(typeof(WrapperPacket));
            m_PacketReader.RegisterType(typeof(LogPacket));
            m_PacketReader.RegisterType(typeof(ThreadInfo));

            //since timestamp length varies by time zone we have to measure it first (sigh..)
            var timestampLength = DateTimeOffsetSerializedLength(); 

            //The first message has to write out a bunch of stuff - definitions, threads, etc.
            LogPacket.Write("message 1", 101, m_PacketWriter);
            Assert.AreEqual(133 + timestampLength, m_MemoryStream.Position,
                "Serialized value isn't the expected length.  Position is: {1}, expected {0}.\r\nSerialized Value: {2}",
                m_MemoryStream.Position, 133 + timestampLength, m_MemoryStream.ToArray().ToDisplayString());

            Thread.Sleep(50);

            //having now written that these messages will be smaller because we don't have to write out threads and timestamps are smaller.
            var baseline = m_MemoryStream.Position;
            LogPacket.Write("message 2", 101, m_PacketWriter);
            Assert.AreEqual(20, m_MemoryStream.Position - baseline);
            Thread.Sleep(50);

            baseline = m_MemoryStream.Position;
            LogPacket.Write("message 3", 101, m_PacketWriter);
            Assert.AreEqual(20, m_MemoryStream.Position - baseline);

            baseline = m_MemoryStream.Position;
            LogPacket.Write("message 1", 101, m_PacketWriter);
            Assert.AreEqual(20, m_MemoryStream.Position - baseline);

            m_MemoryStream.Position = 0;

            ThreadInfo threadInfo = (ThreadInfo)m_PacketReader.Read();
            LogPacket message1 = (LogPacket)m_PacketReader.Read();
            LogPacket message2 = (LogPacket)m_PacketReader.Read();
            LogPacket message3 = (LogPacket)m_PacketReader.Read();
            LogPacket message4 = (LogPacket)m_PacketReader.Read();

            Assert.AreEqual(101, threadInfo.ThreadId);
            Assert.AreEqual(101, message1.ThreadId);
            Assert.AreEqual("message 1", message1.Caption);
            Assert.LessOrEqual(message1.TimeStamp, DateTime.Now);
            Assert.AreEqual(101, message2.ThreadId);
            Assert.AreEqual("message 2", message2.Caption);
            Assert.LessOrEqual(message1.TimeStamp, message2.TimeStamp);
            Assert.AreEqual(101, message3.ThreadId);
            Assert.AreEqual("message 3", message3.Caption);
            Assert.LessOrEqual(message2.TimeStamp, message3.TimeStamp);
            Assert.AreEqual("message 1", message4.Caption);
            Assert.LessOrEqual(message3.TimeStamp, message4.TimeStamp);
        }

        [Ignore("debugging test only")]
        [Test]
        public void GenericPacketTest()
        {
            // create a couple packets and write them out
            WrapperPacket packet1 = new WrapperPacket("Test1", 100);
            WrapperPacket packet2 = new WrapperPacket("Test2", 200);
            m_PacketWriter.Write(packet1);
            m_PacketWriter.Write(packet2);

            // read the packets back, but without having registered a packet factory for WrapperPacket 
            m_MemoryStream.Position = 0;
            IPacket genericPacket1 = m_PacketReader.Read();
            IPacket genericPacket2 = m_PacketReader.Read();

            // now write the generic packets back out
            Assert.AreEqual(m_MemoryStream.Length, m_MemoryStream.Position);
            m_MemoryStream.Position = 0;
            PacketWriter writer = new PacketWriter(m_MemoryStream);
            writer.Write(genericPacket1);
            writer.Write(genericPacket2);

            // finally, read the generic packets back again, but this time
            // with a PacketFactory defined for WrapperPacket.
            PacketReader reader = new PacketReader(m_MemoryStream, false);
            reader.RegisterType(typeof(WrapperPacket));
            m_MemoryStream.Position = 0;
            Assert.AreEqual(packet1, reader.Read());
            Assert.AreEqual(packet2, reader.Read());
            Assert.AreEqual(m_MemoryStream.Length, m_MemoryStream.Position);
        }

        #region IDisposable Members

        ///<summary>
        ///Performs application-defined tasks associated with freeing, releasing, or resetting managed resources.
        ///</summary>
        ///<filterpriority>2</filterpriority>
        public void Dispose()
        {
            // Call the underlying implementation
            Dispose(true);

            // and SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.
        /// </summary>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (!m_WeAreDisposed)
            {
                m_WeAreDisposed = true; // Only Dispose stuff once

                if (releaseManaged)
                {
                    // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                    // Other objects may be referenced in this case

                    // We create our own MemoryStream in Setup(), so we must release it here if we did
                    if (m_MemoryStream != null)
                    {
                        m_MemoryStream.Dispose();
                        m_MemoryStream = null;
                    }
                }
                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here
            }
        }

        #endregion


        private long DateTimeOffsetSerializedLength()
        {
            MemoryStream buffer = new MemoryStream();
            IFieldWriter writer = new FieldWriter(buffer);
            buffer.Position = 0;
            writer.Write(DateTimeOffset.Now);
            return buffer.Position;
        }
    }
}