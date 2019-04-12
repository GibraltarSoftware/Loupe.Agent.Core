using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;

namespace Loupe.Core.Test.Serialization
{
    [Ignore("debugging test only")]
    [TestFixture]
    public class GZipTests
    {
        const int PacketCount = 10000;
        const int SynchronousTail = (int)(PacketCount * 0.9);

        // Test1 is the nominal case of a GZip stream opened and closed nicely
        [Test]
        public void Test1()
        {
            // Temp file will be automatically deleted as part of dispose
            using (var tempFile = new TempFile())
            {
                var gzipWriterStream = new GZipStream(tempFile.Stream, CompressionMode.Compress);

                // Write a bunch of packets to the stream
                for (var i = 0; i < PacketCount; i++)
                {
                    var packet = new Packet(i);
                    gzipWriterStream.Write(packet.Bytes, 0, packet.Bytes.Length);
                }

                // Close the stream
                gzipWriterStream.Dispose();

                // Read back the data and check for expected length
                var fileBytes = File.ReadAllBytes(tempFile.FilePath);
                var byteCount = fileBytes.Length;
                Assert.AreEqual(23466, byteCount);

                // Read back the data and verify that it is correct
                var fileStream = new MemoryStream(fileBytes);
                var gzipReaderStream = new GZipStream(fileStream, CompressionMode.Decompress);

                for (var i = 0; i < PacketCount; i++)
                {
                    var packet = new Packet(gzipReaderStream);
                    Assert.AreEqual(i, packet.Number);
                }

                // Verify that we're at the end of the stream
                Assert.AreEqual(-1, gzipReaderStream.ReadByte());
            }
        }

        // Test2 always writes in Sync mode
        [Test]
        public void Test2()
        {
            // Temp file will be automatically deleted as part of dispose
            using (var tempFile = new TempFile())
            {
                using (var gzipWriterStream = new GZipStream(tempFile.Stream, CompressionMode.Compress))
                {

                    // Write a bunch of packets to the stream
                    for (var i = 0; i < PacketCount; i++)
                    {
                        var packet = new Packet(i);
                        gzipWriterStream.Write(packet.Bytes, 0, packet.Bytes.Length);
                        if ((i + 1) % 1000 == 0)
                            gzipWriterStream.Flush();
                    }
                }

                // Read back the data and check for expected length
                var fileBytes = File.ReadAllBytes(tempFile.FilePath);
                var byteCount = fileBytes.Length;
                Assert.AreEqual(23778, byteCount); // file size is about 4x larger than normal

                // Read back the data and verify that it is correct
                var fileStream = new MemoryStream(fileBytes);
                using (var gzipReaderStream = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    for (var i = 0; i < PacketCount; i++)
                    {
                        var packet = new Packet(gzipReaderStream);
                        Assert.AreEqual(i, packet.Number);
                    }

                    // Verify that we're at the end of the stream
                    Assert.AreEqual(-1, gzipReaderStream.ReadByte());
                }
            }
        }

        // Test3a switches to Sync mode for the last 10% of messages with a flush after each
        [Test]
        [Ignore("debugging test only")] // We only want to run this test manually to prep for a manual run of Test3b
        public void Test3a()
        {
            if (!Debugger.IsAttached)
                Assert.Fail("This test is only valid if the debugger is attached");

            // Temp file will be automatically delated as part of dispose
            using (var tempFile = new TempFile("GzipTest", true))
            {
                using (var gzipWriterStream = new GZipStream(tempFile.Stream, CompressionMode.Compress))
                {
                    // Write a bunch of packets to the stream
                    for (var i = 0; i < PacketCount; i++)
                    {
                        var packet = new Packet(i);
                        gzipWriterStream.Write(packet.Bytes, 0, packet.Bytes.Length);

                        //                    if (i >= SynchronousTail )
                        //                    {
                        //                        if (i % 100 == 0)
                        //                            gzipWriterStream.Flush();
                        //                    }
                    }
                    // Abort test here to simulate ragged end file
                    gzipWriterStream.Flush();
                    Debugger.Break();
                    Assert.Fail("This test is invalid unless you stop the debugger to abort this test at the breakpoint above");
                }
            }
        }

        // Test3a switches to Full mode for the last 10% of messages with a flush after each
        [Test]
        [Ignore("debugging test only")] // We only want to run this test manually to prep for a manual run of Test3b
        public void Test3b()
        {
            if (!Debugger.IsAttached)
                Assert.Fail("This test is only valid if the debugger is attached");

            // Temp file will be automatically delated as part of dispose
            using (var tempFile = new TempFile("GzipTest", true))
            {
                using (var gzipWriterStream = new GZipStream(tempFile.Stream, CompressionMode.Compress))
                {

                    // Write a bunch of packets to the stream
                    for (var i = 0; i < PacketCount; i++)
                    {
                        var packet = new Packet(i);
                        gzipWriterStream.Write(packet.Bytes, 0, packet.Bytes.Length);

                        if (i >= SynchronousTail)
                        {
                            gzipWriterStream.Flush();
                        }
                    }

                    // Abort test here to simulate ragged end file
                    Debugger.Break();
                    Assert.Fail("This test is invalid unless you stop the debugger to abort this test at the breakpoint above");
                }
            }
        }

        // Test4 reads the file produced from a properly aborted run of Test3a or Test3b
        [Test]
        [Ignore("debugging test only")] // We only want to run this test manually after an aborted run of Test3a or Test3b
        public void Test4()
        {
            // Read back the data and check for expected length
            using (var tempFile = new TempFile("GzipTest", false))
            {
                var fileBytes = File.ReadAllBytes(tempFile.FilePath);
                var byteCount = fileBytes.Length;
                Assert.IsTrue(byteCount == 33000 || byteCount == 50864);

                // Read back the data and verify that it is correct
                var fileStream = new MemoryStream(fileBytes);
                var gzipReaderStream = new GZipStream(fileStream, CompressionMode.Decompress);

                for (var i = 0; i < PacketCount; i++)
                {
                    var packet = new Packet(gzipReaderStream);
                    Assert.AreEqual(i, packet.Number);
                }

                // Verify that we're at the end of the stream
                Assert.AreEqual(-1, gzipReaderStream.ReadByte());
            }
        }

        public class TempFile : IDisposable
        {
            public readonly string FilePath;
            public readonly FileStream Stream;

            public TempFile()
            {
                FilePath = Path.GetTempFileName();
                Stream = new FileStream(FilePath, FileMode.Create, FileAccess.ReadWrite);
            }

            public TempFile(string fileName, bool write = true)
            {
                FilePath =   Path.Combine(Path.GetTempPath(), fileName);
                Stream = write
                    ? new FileStream(FilePath, FileMode.Create, FileAccess.ReadWrite)
                    : new FileStream(FilePath, FileMode.Open, FileAccess.Read);
            }

            public void Dispose()
            {
                Stream.Dispose();
                File.Delete(FilePath);
            }
        }

        public class Packet
        {
            public readonly int Number;
            public readonly string Value;
            public Packet(int number)
            {
                Number = number;
                Value = "DCBA >>>> " + number.ToString("D4") + " <<<< ABCD\n";                
            }

            public Packet(Stream stream)
            {
                var buffer = new byte[25];
                stream.Read(buffer, 0, 25);
                Value = Encoding.UTF8.GetString(buffer);
                Number = int.Parse(Value.Substring(10, 4));
            }

            public byte[] Bytes
            {
                get { return Encoding.UTF8.GetBytes(Value); }
            }
        }
    }
}
