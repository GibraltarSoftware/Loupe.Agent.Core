using System;




namespace Loupe.Serialization
{
    /// <summary>
    /// Standard interface for objects that can write individual serialized fields
    /// </summary>
    public interface IFieldWriter
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
        /// Returns the cache of unique string values tht have been written
        /// </summary>
        UniqueStringList Strings { get; }

        /// <summary>
        /// Ensure that all pending state changes are committed.
        /// </summary>
        void Commit();

        /// <summary>
        /// Rollback any pending state changes that have not been committed
        /// </summary>
        void Rollback();

        /// <summary>
        /// Write an object to the stream as its serializable type
        /// </summary>
        /// <param name="value">The object (or boxed integral value) to write.</param>
        void Write(object value);

        /// <summary>
        /// Write an object to the stream as its serializable type
        /// </summary>
        /// <param name="value">The object (or boxed integral value) to write.</param>
        /// <param name="fieldType">The field type to write the value out as.</param>
        void Write(object value, FieldType fieldType);

        /// <summary>
        /// Write a bool to the stream.
        /// </summary>
        /// <returns>A bool value.</returns>
        void Write(bool value);

        /// <summary>
        /// Write an array of bool to the stream.
        /// </summary>
        /// <returns>An array of bool values.</returns>
        void Write(bool[] array);

        /// <summary>
        /// Write a string to the stream.
        /// <remarks>
        /// We optimize strings by maintaining a hash table of each unique string 
        /// we have seen.  Each string is sent with as an integer index into the table.
        /// When a new string is encountered, it's index is followed by the string value.
        /// </remarks>
        /// </summary>
        void Write(string value);

        /// <summary>
        /// Write an array of string to the stream.
        /// </summary>
        /// <returns>An array of string values.</returns>
        void Write(string[] array);

        /// <summary>
        /// Stores a 32-bit signed value into the stream using 7-bit encoding.
        /// <remarks>
        /// The value is written 7 bits at a time (starting with the least-significant bits) until there are no more bits to write.
        /// The eighth bit of each byte stored is used to indicate whether there are more bytes following this one.
        /// </remarks>
        /// </summary>
        void Write(Int32 value);

        /// <summary>
        /// Write an array of Int32 to the stream.
        /// </summary>
        /// <returns>An array of Int32 values.</returns>
        void Write(Int32[] array);

        /// <summary>
        /// Stores a 64-bit signed value into the stream using 7-bit encoding.
        /// <remarks>
        /// The value is written 7 bits at a time (starting with the least-significant bits) until there are no more bits to write.
        /// The eighth bit of each byte stored is used to indicate whether there are more bytes following this one.
        /// </remarks>
        /// </summary>
        /// <param name="value">The Int64 value to encode.</param>
        void Write(Int64 value);

        /// <summary>
        /// Write an array of Int64 to the stream.
        /// </summary>
        /// <returns>An array of Int64 values.</returns>
        void Write(Int64[] array);

        /// <summary>
        /// Stores a 32-bit unsigned value into the stream using 7-bit encoding.
        /// <remarks>
        /// The value is written 7 bits at a time (starting with the least-significant bits) until there are no more bits to write.
        /// The eighth bit of each byte stored is used to indicate whether there are more bytes following this one.
        /// </remarks>
        /// </summary>
        /// <param name="value">The UInt32 value to encode.</param>
        void Write(UInt32 value);

        /// <summary>
        /// Write an array of UInt32 to the stream.
        /// </summary>
        /// <returns>An array of UInt32 values.</returns>
        void Write(UInt32[] array);

        /// <summary>
        /// Stores a 64-bit unsigned value into the stream using 7-bit encoding.
        /// <remarks>
        /// The value is written 7 bits at a time (starting with the least-significant bits) until there are no more bits to write.
        /// The eighth bit of each byte stored is used to indicate whether there are more bytes following this one.
        /// 
        /// There is a special optimization for UInt64 because after passing 8 7-bit values we know that
        /// there can only be 8 bits left (since the original data was 64 bits long).  So, for that last byte,
        /// we can use all 8 bits.  This means that the worst case size for a double is 9 bytes versus
        /// 10 which would otherwise sometimes be necessary to pass that very last bit.
        /// </remarks>
        /// </summary>
        /// <param name="value">The UInt64 value to encode.</param>
        void Write(UInt64 value);

        /// <summary>
        /// Write an array of UInt64 to the stream.
        /// </summary>
        /// <returns>An array of UInt64 values.</returns>
        void Write(UInt64[] array);

        /// <summary>
        /// Stores a 64-bit double value int the stream in the fewest bytes possible.
        /// <remarks>
        /// For many common numbers the bit representation of a double includes lots of
        /// trailing zeros.  This creates an opportunity to optimize these values in a
        /// similar way to how we optimize UInt64.  The difference is just that in this case
        /// we are interested in the high-order bits whereas with UInt64 we are interested
        /// in the low order bits.
        /// </remarks>
        /// </summary>
        void Write(double value);

        /// <summary>
        /// Write an array of double to the stream.
        /// </summary>
        /// <returns>An array of double values.</returns>
        void Write(double[] array);

        /// <summary>
        /// Stores a TimeSpan value to the stream
        /// </summary>
        void Write(TimeSpan value);

        /// <summary>
        /// Write an array of TimeSpan to the stream.
        /// </summary>
        /// <returns>An array of TimeSpan values.</returns>
        void Write(TimeSpan[] array);

        /// <summary>
        /// Stores a DateTime value to the stream
        /// </summary>
        void Write(DateTime value);

        /// <summary>
        /// Write an array of DateTime to the stream.
        /// </summary>
        /// <returns>An array of DateTime values.</returns>
        void Write(DateTime[] array);

        /// <summary>
        /// Stores a DateTime value to the stream
        /// </summary>
        void Write(DateTimeOffset value);

        /// <summary>
        /// Write an array of DateTime to the stream.
        /// </summary>
        /// <returns>An array of DateTime values.</returns>
        void Write(DateTimeOffset[] array);

        /// <summary>
        /// Stores a 128-bit Guid value to the stream
        /// </summary>
        void Write(Guid value);

        /// <summary>
        /// Write an array of Guid to the stream.
        /// </summary>
        /// <returns>An array of Guid values.</returns>
        void Write(Guid[] array);

        /// <summary>
        /// This is a helper method for unit testing.  It only works for the case
        /// the underlying stream is a MemoryStream.
        /// </summary>
        /// <returns>The stream data as a byte array</returns>
        byte[] ToArray();
    }
}