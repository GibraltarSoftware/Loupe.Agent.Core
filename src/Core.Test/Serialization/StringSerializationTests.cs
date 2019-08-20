using System.IO;
using Loupe.Serialization;
using NUnit.Framework;

namespace Loupe.Core.Test.Serialization
{
    [TestFixture]
    public class StringSerializationTests
    {

        [Test]
        public void TestProtocolVersion1()
        {
            // Test with protocol version 1.0
            var writer = new FieldWriter(new MemoryStream(), new UniqueStringList(), 1, 0);

            /*
             * These first few writes are identical to TestStringCompressionDisabled
             */

            // Test that a null string takes only one byte
            writer.Write((string)null);
            Assert.AreEqual(1, writer.Position, "Expected position ");

            // Test that an empty string takes 2 bytes
            writer.Write("");
            Assert.AreEqual(3, writer.Position, "Expected position ");

            // Test that passing a single character string takes 3 bytes
            writer.Write(" ");
            Assert.AreEqual(6, writer.Position, "Expected position ");

            // Test that passing a two character string takes 4 bytes
            writer.Write("Hi");
            Assert.AreEqual(10, writer.Position, "Expected position ");

            // Test that 5 characters takes 7 bytes
            writer.Write("Hello");
            Assert.AreEqual(17, writer.Position, "Expected position ");

            /*
             * Now, let's try some repeated strings
             */

            // Test that a second occurence should only take 1 byte with compression enabled
            writer.Write("Hello");
            Assert.AreEqual(18, writer.Position, "Expected position ");

            writer.Write("Hi");
            Assert.AreEqual(19, writer.Position, "Expected position ");

            // Verify that the strings read back are the same as those written
            IFieldReader reader = new FieldReader(new MemoryStream(writer.ToArray()), new UniqueStringList(), 1, 0);
            Assert.AreEqual(null, reader.ReadString());
            Assert.AreEqual("", reader.ReadString());
            Assert.AreEqual(" ", reader.ReadString());
            Assert.AreEqual("Hi", reader.ReadString());
            Assert.AreEqual("Hello", reader.ReadString());
            Assert.AreEqual("Hello", reader.ReadString());
            Assert.AreEqual("Hi", reader.ReadString());
        }

        [Test]
        public void TestProtocolVersion2()
        {
            // Test with protocol version 2.0
            var writer = new FieldWriter(new MemoryStream(), new UniqueStringList(), 2, 0);

            // Test that a null string takes only one byte
            writer.Write((string) null);
            Assert.AreEqual(2, writer.Position, "Expected position ");

            // Test that an empty string takes 2 bytes
            writer.Write("");
            Assert.AreEqual(3, writer.Position, "Expected position ");

            // Test that passing a single character string takes 2 bytes
            writer.Write(" ");
            Assert.AreEqual(5, writer.Position, "Expected position ");

            // Test that passing a two character string takes 3 bytes
            writer.Write("Hi");
            Assert.AreEqual(8, writer.Position, "Expected position ");

            // Test that 5 characters takes 6 bytes
            writer.Write("Hello");
            Assert.AreEqual(14, writer.Position, "Expected position ");

            // Test that a second occurence still takes 6 bytes
            writer.Write("Hello");
            Assert.AreEqual(20, writer.Position, "Expected position ");

            // Likewise, a second occurence of an earlier string still takes a much space
            writer.Write("Hi");
            Assert.AreEqual(23, writer.Position, "Expected position ");

            // Verify that the strings read back are the same as those written
            IFieldReader reader = new FieldReader(new MemoryStream(writer.ToArray()), null, 2, 0);
            Assert.AreEqual(null, reader.ReadString());
            Assert.AreEqual("", reader.ReadString());
            Assert.AreEqual(" ", reader.ReadString());
            Assert.AreEqual("Hi", reader.ReadString());
            Assert.AreEqual("Hello", reader.ReadString());
            Assert.AreEqual("Hello", reader.ReadString());
            Assert.AreEqual("Hi", reader.ReadString());
        }
    }
}
