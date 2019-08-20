using System;
using System.IO;
using Loupe.Serialization;
using NUnit.Framework;

#pragma warning disable 1591

namespace Loupe.Core.Serialization.UnitTests
{
    [TestFixture]
    public class FieldSerializationTests
    {
        private static void CheckInt32(int expectedValue, int expectedSize)
        {
            IFieldWriter writer = new FieldWriter(new MemoryStream());
            writer.Write(expectedValue);

            IFieldReader reader = new FieldReader(writer.ToArray());
            int actualValue = reader.ReadInt32();

            Assert.AreEqual(expectedValue, actualValue, "Expected deserialized value ");
            if (expectedSize > 0)
                Assert.AreEqual(expectedSize, writer.Position, "Unexpected size for {0} ", expectedValue);
        }

        private static void CheckInt64(long expectedValue, int expectedSize)
        {
            IFieldWriter writer = new FieldWriter();
            writer.Write(expectedValue);

            IFieldReader reader = new FieldReader(writer.ToArray());
            long actualValue = reader.ReadInt64();

            Assert.AreEqual(expectedValue, actualValue, "Expected deserialized value ");
            if (expectedSize > 0)
                Assert.AreEqual(expectedSize, writer.Position, "Unexpected size for {0} ", expectedValue);
        }

        private static void CheckUInt32(uint expectedValue, int expectedSize)
        {
            IFieldWriter writer = new FieldWriter();
            writer.Write(expectedValue);

            IFieldReader reader = new FieldReader(writer.ToArray());
            uint actualValue = reader.ReadUInt32();

            Assert.AreEqual(expectedValue, actualValue, "Expected deserialized value ");
            if (expectedSize > 0)
                Assert.AreEqual(expectedSize, writer.Position, "Unexpected size for {0} ", expectedValue);
        }

        private static void CheckUInt64(ulong expectedValue, int expectedSize)
        {
            IFieldWriter writer = new FieldWriter();
            writer.Write(expectedValue);

            IFieldReader reader = new FieldReader(writer.ToArray());
            ulong actualValue = reader.ReadUInt64();

            Assert.AreEqual(expectedValue, actualValue, "Expected deserialized value ");
            if (expectedSize > 0)
                Assert.AreEqual(expectedSize, writer.Position, "Unexpected size for {0} ", expectedValue);
        }

        private static void CheckDouble(double expectedValue, int expectedSize)
        {
            IFieldWriter writer = new FieldWriter();
            writer.Write(expectedValue);

            IFieldReader reader = new FieldReader(writer.ToArray());
            double actualValue = reader.ReadDouble();

            Assert.AreEqual(expectedValue, actualValue, "Expected deserialized value ");
            Assert.AreEqual(expectedSize, writer.Position, "Unexpected size for {0} ", expectedValue);
        }

        [Test]
        public void CheckString()
        {
            IFieldWriter writer = new FieldWriter();
            writer.Write((string)null);
            Assert.AreEqual(2, writer.Position, "Expected position ");

            writer.Write("");
            Assert.AreEqual(3, writer.Position, "Expected position ");

            writer.Write(" ");
            Assert.AreEqual(5, writer.Position, "Expected position ");

            writer.Write("Hello");
            Assert.AreEqual(11, writer.Position, "Expected position ");

            writer.Write("Hello");
            Assert.AreEqual(17, writer.Position, "Expected position ");

            writer.Write("Hi");
            Assert.AreEqual(20, writer.Position, "Expected position ");

            IFieldReader reader = new FieldReader(writer.ToArray());
            Assert.AreEqual(null, reader.ReadString(), "Expected string ");
            Assert.AreEqual("", reader.ReadString(), "Expected string ");
            Assert.AreEqual(" ", reader.ReadString(), "Expected string ");
            Assert.AreEqual("Hello", reader.ReadString(), "Expected string ");
            Assert.AreEqual("Hello", reader.ReadString(), "Expected string2 ");
            Assert.AreEqual("Hi", reader.ReadString(), "Expected string ");
        }


        [Test]
        public void CheckInts()
        {
            for (int i = 0; i < 1024*1024; i++)
            {
                CheckInt32(i, -1);
                CheckUInt32((uint)i, -1);
                CheckInt64((long)i, -1);
                CheckUInt64((ulong)i, -1);
            }
        }

        /// <summary>
        /// For optimized int32 smaller values take less space
        /// </summary>
        [Test]
        public void CheckInt32()
        {
            // 0x00000000 - 0x0000003F (0 to 63) takes 1 byte
            // 0x00000040 - 0x00001FFF (64 to 8,191) takes 2 bytes
            // 0x00002000 - 0x000FFFFF (8,192 to 1,048,575) takes 3 bytes
            // 0x00100000 - 0x07FFFFFF (1,048,576 to 134,217,727) takes 4 bytes
            // 0x08000000 - 0x7FFFFFFF (134,217,728 to 2,147,483,647) takes 5 bytes
            // 
            // 0x80000000 - 0xF8000000 (-2,147,483,648 to -134,217,728) takes 5 bytes
            // 0xF8000001 - 0xFFF00000 (-134,217,727 to -1,048,576) takes 4 bytes
            // 0xFFF00001 - 0xFFFFE000 (-1,048,575 to -8192) takes 3 bytes
            // 0xFFFFE001 - 0xFFFFFFC0 (-8191 to -64) takes 2 bytes
            // 0xFFFFFFC1 - 0xFFFFFFFF (-63 to -1) takes 1 byte
            CheckInt32(0, 1);
            CheckInt32(63, 1);

            CheckInt32(64, 2);
            CheckInt32(8191, 2);

            CheckInt32(8192, 3);
            CheckInt32(1048575, 3);

            CheckInt32(1048576, 4);
            CheckInt32(134217727, 4);

            CheckInt32(134217728, 5);
            CheckInt32(2147483647, 5); // int.MaxValue
            CheckInt32(-2147483648, 5); // int.MinValue
            CheckInt32(-134217728, 5);

            CheckInt32(-134217727, 4);
            CheckInt32(-1048576, 4);

            CheckInt32(-1048575, 3);
            CheckInt32(-8192, 3);

            CheckInt32(-8191, 2);
            CheckInt32(-64, 2);

            CheckInt32(-63, 1);
            CheckInt32(-1, 1);
        }

        /// <summary>
        /// For optimized Int64 smaller values take less space
        /// </summary>
        [Test]
        public void CheckInt64()
        {
            // 0x00000000 - 0x0000003F (0 to 63) takes 1 byte
            // 0x00000040 - 0x00001FFF (64 to 8,191) takes 2 bytes
            // 0x00002000 - 0x000FFFFF (8,192 to 1,048,575) takes 3 bytes
            // 0x00100000 - 0x07FFFFFF (1,048,576 to 134,217,727) takes 4 bytes
            // 0x08000000 - 0x7FFFFFFF (134,217,728 to 2,147,483,647) takes 5 bytes
            // 
            // 0x80000000 - 0xF8000000 (-2,147,483,648 to -134,217,728) takes 5 bytes
            // 0xF8000001 - 0xFFF00000 (-134,217,727 to -1,048,576) takes 4 bytes
            // 0xFFF00001 - 0xFFFFE000 (-1,048,575 to -8192) takes 3 bytes
            // 0xFFFFE001 - 0xFFFFFFC0 (-8191 to -64) takes 2 bytes
            // 0xFFFFFFC1 - 0xFFFFFFFF (-63 to -1) takes 1 byte
            // 
            CheckInt64(0, 1);
            CheckInt64(63, 1);

            CheckInt64(64, 2);
            CheckInt64(8191, 2);

            CheckInt64(8192, 3);
            CheckInt64(1048575, 3);

            CheckInt64(1048576, 4);
            CheckInt64(134217727, 4);

            CheckInt64(134217728, 5);
            CheckInt64(2147483647, 5); // int.MaxValue
            CheckInt64(-2147483648, 5); // int.MinValue
            CheckInt64(-134217728, 5);

            CheckInt64(-134217727, 4);
            CheckInt64(-1048576, 4);

            CheckInt64(-1048575, 3);
            CheckInt64(-8192, 3);

            CheckInt64(-8191, 2);
            CheckInt64(-64, 2);

            CheckInt64(-63, 1);
            CheckInt64(-1, 1);
            CheckInt64(0x7fffffffffffffff, 10);
            CheckInt64(-0x7fffffffffffffff + 1, 10);
        }

        /// <summary>
        /// For optimized int32 smaller values take less space
        /// </summary>
        [Test]
        public void CheckUInt32()
        {
            // 0x00000000 - 0x0000007F (0 to 127) takes 1 byte
            // 0x00000080 - 0x00003FFF (128 to 16,383) takes 2 bytes
            // 0x00004000 - 0x000FFFFF (16,384 to 2,097,151) takes 3 bytes
            // 0x00200000 - 0x07FFFFFF (2,097,152 to 268,435,455) takes 4 bytes
            // 0x08000000 - 0x7FFFFFFF (268,435,456 to 2,147,483,647) takes 5 bytes
            CheckUInt32(0, 1);
            CheckUInt32(63, 1);
            CheckUInt32(64, 1);
            CheckUInt32(127, 1);

            CheckUInt32(128, 2);
            CheckUInt32(8191, 2);
            CheckUInt32(8192, 2);
            CheckUInt32(16383, 2);

            CheckUInt32(16384, 3);
            CheckUInt32(1048575, 3);
            CheckUInt32(1048576, 3);
            CheckUInt32(2097151, 3);

            CheckUInt32(2097152, 4);
            CheckUInt32(134217727, 4);
            CheckUInt32(134217728, 4);
            CheckUInt32(268435455, 4);

            CheckUInt32(268435456, 5);
            CheckUInt32(2147483647, 5); // int.MaxValue
        }

        /// <summary>
        /// For optimized Int64 smaller values take less space
        /// </summary>
        [Test]
        public void CheckUInt64()
        {
            // 0x00000000 - 0x0000007F (0 to 127) takes 1 byte
            // 0x00000080 - 0x00003FFF (128 to 16,383) takes 2 bytes
            // 0x00004000 - 0x000FFFFF (16,384 to 2,097,151) takes 3 bytes
            // 0x00200000 - 0x07FFFFFF (2,097,152 to 268,435,455) takes 4 bytes
            // 0x08000000 - 0x7FFFFFFF (268,435,456 to 2,147,483,647) takes 5 bytes
            // ...
            CheckUInt64(0, 1);
            CheckUInt64(63, 1);
            CheckUInt64(64, 1);
            CheckUInt64(127, 1);

            CheckUInt64(128, 2);
            CheckUInt64(8191, 2);
            CheckUInt64(8192, 2);
            CheckUInt64(16383, 2);

            CheckUInt64(16384, 3);
            CheckUInt64(1048575, 3);
            CheckUInt64(1048576, 3);
            CheckUInt64(2097151, 3);

            CheckUInt64(2097152, 4);
            CheckUInt64(134217727, 4);
            CheckUInt64(134217728, 4);
            CheckUInt64(268435455, 4);

            CheckUInt64(268435456, 5);
            CheckUInt64(2147483647, 5); // int.MaxValue
            CheckUInt64(0x9999999999999999u, 9);
        }

        [Test]
        public void CheckDouble()
        {
            CheckDouble(0, 1);
            CheckDouble(1, 2);
            CheckDouble(2, 1);
            CheckDouble(5, 2);
            CheckDouble(10, 2);
            CheckDouble(100, 3);
            CheckDouble(1234, 3);
            CheckDouble(9876543210, 7);
            CheckDouble(0.9876543210, 9);
            CheckDouble(0.123, 9);
            CheckDouble(0.125, 2);
            CheckDouble(2.5, 2);
            CheckDouble(0.25, 2);
            CheckDouble(3.14, 9);
            CheckDouble(0.99999, 9);
            CheckDouble(double.MaxValue, 9);
            CheckDouble(double.MinValue, 9);
        }

        [Test]
        public void CheckTimeSpan()
        {
            MemoryStream buffer = new MemoryStream();
            IFieldWriter writer = new FieldWriter(buffer);
            IFieldReader reader = new FieldReader(buffer);
            foreach (ulong value in new UInt64[]
                                        {
                                            0x0, 0x1, 0x9999, 0xffff, 0xffffffff, 0xffffffffffff,
                                            0xffffffffffffffff, 0x7fffffffffffffff, 0x9999999999999999
                                        })
            {
                buffer.Position = 0;
                TimeSpan expected = new TimeSpan((long)value);
                writer.Write(expected);
                buffer.Position = 0;
                TimeSpan actual = reader.ReadTimeSpan();
                Assert.AreEqual(expected, actual);
            }
        }

        [Test]
        public void CheckDateTime()
        {
            MemoryStream buffer = new MemoryStream();
            IFieldWriter writer = new FieldWriter(buffer);
            IFieldReader reader = new FieldReader(buffer);
            var referenceTime = new DateTime(1973, 6, 25, 8, 30, 12, DateTimeKind.Local); //we convert to UTC during serialization, we want local time.
            buffer.Position = 0;
            writer.Write(referenceTime);
            Assert.AreEqual(12, buffer.Position);
            writer.Write(referenceTime.AddTicks(1));
            Assert.AreEqual(16, buffer.Position);
            writer.Write(referenceTime.AddMilliseconds(50));
            Assert.AreEqual(22, buffer.Position);
            writer.Write(referenceTime.AddHours(1));
            Assert.AreEqual(31, buffer.Position);
            writer.Write(referenceTime.AddDays(1));
            Assert.AreEqual(40, buffer.Position);
            buffer.Position = 0;
            Assert.AreEqual(referenceTime, reader.ReadDateTime());
            Assert.AreEqual(referenceTime.AddTicks(1), reader.ReadDateTime());
            Assert.AreEqual(referenceTime.AddMilliseconds(50), reader.ReadDateTime());
            Assert.AreEqual(referenceTime.AddHours(1), reader.ReadDateTime());
            Assert.AreEqual(referenceTime.AddDays(1), reader.ReadDateTime());
        }

        [Test]
        public void CheckGuid()
        {
            MemoryStream buffer = new MemoryStream();
            IFieldWriter writer = new FieldWriter(buffer);
            IFieldReader reader = new FieldReader(buffer);
            foreach (Guid expected in new Guid[]
                                          {
                                              Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
                                          })
            {
                buffer.Position = 0;
                writer.Write(expected);
                buffer.Position = 0;
                Guid actual = reader.ReadGuid();
                Assert.AreEqual(expected, actual);
            }
        }

        [Test]
        public void CheckBoolArray()
        {
            MemoryStream buffer = new MemoryStream();
            IFieldWriter writer = new FieldWriter(buffer);
            IFieldReader reader = new FieldReader(buffer);
            bool[][] list = new bool[][] { new bool[0], new bool[1], new bool[2], new bool[31], new bool[32], new bool[33],
                                           new bool[63], new bool[64], new bool[65], new bool[127], new bool[128], new bool[129]};
            Random rand = new Random(0);
            int bitCount = 0;
            for (int i = 0; i < list.Length; i++ )
            {
                bool[] current = list[i];
                bitCount += current.Length;
                for (int j = 0; j < current.Length; j++)
                    current[j] = rand.NextDouble() >= 0.5;
                writer.Write(current);
                
            }

            Assert.AreEqual(675, bitCount);
            Assert.AreEqual(buffer.Length, buffer.Position);
            buffer.Position = 0;
            for (int i = 0; i < list.Length; i++)
            {
                bool[] current = list[i];
                bool[] actual = reader.ReadBoolArray();
                CompareArray(current, actual);
            }
            Assert.AreEqual(buffer.Length, buffer.Position);
        }

        [Test]
        public void CheckInt32Array()
        {
            MemoryStream buffer = new MemoryStream();
            IFieldWriter writer = new FieldWriter(buffer);
            IFieldReader reader = new FieldReader(buffer);
            Int32[] array1 = new Int32[] {};
            Int32[] array2 = new Int32[] {1};
            Int32[] array3 = new Int32[] {0, 0, 0, 0, 0, 0};
            Int32[] array4 = new Int32[] {0, 1, 2, 3, 4, 5};
            Int32[] array5 = new Int32[] {0, 1, 1, 2, 2, 2};
            Int32[] array6 = new Int32[] {0, 1, 1, 2, 2, 2, 3};
            writer.Write(array1);
            writer.Write(array2);
            writer.Write(array3);
            writer.Write(array4);
            writer.Write(array5);
            writer.Write(array6);
            buffer.Position = 0;
            CompareArray(array1, reader.ReadInt32Array());
            CompareArray(array2, reader.ReadInt32Array());
            CompareArray(array3, reader.ReadInt32Array());
            CompareArray(array4, reader.ReadInt32Array());
            CompareArray(array5, reader.ReadInt32Array());
            CompareArray(array6, reader.ReadInt32Array());
        }

        [Test]
        public void CheckInt64Array()
        {
            MemoryStream buffer = new MemoryStream();
            IFieldWriter writer = new FieldWriter(buffer);
            IFieldReader reader = new FieldReader(buffer);
            Int64[] array1 = new Int64[] {};
            Int64[] array2 = new Int64[] {1};
            Int64[] array3 = new Int64[] {0, 0, 0, 0, 0, 0};
            Int64[] array4 = new Int64[] {0, 1, 2, 3, 4, 5};
            Int64[] array5 = new Int64[] {0, 1, 1, 2, 2, 2};
            Int64[] array6 = new Int64[] {0, 1, 1, 2, 2, 2, 3};
            writer.Write(array1);
            writer.Write(array2);
            writer.Write(array3);
            writer.Write(array4);
            writer.Write(array5);
            writer.Write(array6);
            buffer.Position = 0;
            CompareArray(array1, reader.ReadInt64Array());
            CompareArray(array2, reader.ReadInt64Array());
            CompareArray(array3, reader.ReadInt64Array());
            CompareArray(array4, reader.ReadInt64Array());
            CompareArray(array5, reader.ReadInt64Array());
            CompareArray(array6, reader.ReadInt64Array());
        }

        [Test]
        public void CheckUInt32Array()
        {
            MemoryStream buffer = new MemoryStream();
            IFieldWriter writer = new FieldWriter(buffer);
            IFieldReader reader = new FieldReader(buffer);
            UInt32[] array1 = new UInt32[] {};
            UInt32[] array2 = new UInt32[] {1};
            UInt32[] array3 = new UInt32[] {0, 0, 0, 0, 0, 0};
            UInt32[] array4 = new UInt32[] {0, 1, 2, 3, 4, 5};
            UInt32[] array5 = new UInt32[] {0, 1, 1, 2, 2, 2};
            UInt32[] array6 = new UInt32[] {0, 1, 1, 2, 2, 2, 3};
            writer.Write(array1);
            writer.Write(array2);
            writer.Write(array3);
            writer.Write(array4);
            writer.Write(array5);
            writer.Write(array6);
            buffer.Position = 0;
            CompareArray(array1, reader.ReadUInt32Array());
            CompareArray(array2, reader.ReadUInt32Array());
            CompareArray(array3, reader.ReadUInt32Array());
            CompareArray(array4, reader.ReadUInt32Array());
            CompareArray(array5, reader.ReadUInt32Array());
            CompareArray(array6, reader.ReadUInt32Array());
        }

        [Test]
        public void CheckUInt64Array()
        {
            MemoryStream buffer = new MemoryStream();
            IFieldWriter writer = new FieldWriter(buffer);
            IFieldReader reader = new FieldReader(buffer);
            UInt64[] array1 = new UInt64[] {};
            UInt64[] array2 = new UInt64[] {1};
            UInt64[] array3 = new UInt64[] {0, 0, 0, 0, 0, 0};
            UInt64[] array4 = new UInt64[] {0, 1, 2, 3, 4, 5};
            UInt64[] array5 = new UInt64[] {0, 1, 1, 2, 2, 2};
            UInt64[] array6 = new UInt64[] {0, 1, 1, 2, 2, 2, 3};
            writer.Write(array1);
            writer.Write(array2);
            writer.Write(array3);
            writer.Write(array4);
            writer.Write(array5);
            writer.Write(array6);
            buffer.Position = 0;
            CompareArray(array1, reader.ReadUInt64Array());
            CompareArray(array2, reader.ReadUInt64Array());
            CompareArray(array3, reader.ReadUInt64Array());
            CompareArray(array4, reader.ReadUInt64Array());
            CompareArray(array5, reader.ReadUInt64Array());
            CompareArray(array6, reader.ReadUInt64Array());
        }

        [Test]
        public void CheckDoubleArray()
        {
            MemoryStream buffer = new MemoryStream();
            IFieldWriter writer = new FieldWriter(buffer);
            IFieldReader reader = new FieldReader(buffer);
            Double[] array1 = new Double[] {};
            Double[] array2 = new Double[] {1};
            Double[] array3 = new Double[] {0, 0, 0, 0, 0, 0};
            Double[] array4 = new Double[] {0, 1, 2, 3, 4, 5};
            Double[] array5 = new Double[] {0, 1, 1, 2, 2, 2};
            Double[] array6 = new Double[] {0, 1, 1, 2, 2, 2, 3};
            writer.Write(array1);
            writer.Write(array2);
            writer.Write(array3);
            writer.Write(array4);
            writer.Write(array5);
            writer.Write(array6);
            buffer.Position = 0;
            CompareArray(array1, reader.ReadDoubleArray());
            CompareArray(array2, reader.ReadDoubleArray());
            CompareArray(array3, reader.ReadDoubleArray());
            CompareArray(array4, reader.ReadDoubleArray());
            CompareArray(array5, reader.ReadDoubleArray());
            CompareArray(array6, reader.ReadDoubleArray());
        }

        private static void CompareArray<T>(T[] array, T[] returnValue)
        {
            Assert.AreEqual(array.Length, returnValue.Length);
            for (int i = 0; i < array.Length; i++)
                Assert.AreEqual(array[i], returnValue[i]);
        }
    }
}