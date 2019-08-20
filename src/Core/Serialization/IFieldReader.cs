using System;

namespace Loupe.Serialization
{
    /// <summary>
    /// Standard interface for objects that can read individual fields
    /// </summary>
    public interface IFieldReader
    {
        /// <summary>
        /// Returns the cache of unique string values that have been written
        /// </summary>
        UniqueStringList Strings { get; }

        /// <summary>
        /// Returns a UInt64 value from the stream without repositioning the stream
        /// </summary>
        /// <returns>A UInt64 value.</returns>
        UInt64 PeekUInt64();

        /// <summary>
        /// Returns a bool value from the stream.
        /// </summary>
        /// <returns>A bool value.</returns>
        bool ReadBool();

        /// <summary>
        /// Returns an array of bool values from the stream.
        /// </summary>
        /// <returns>An array bool values.</returns>
        bool[] ReadBoolArray();

        /// <summary>
        /// Read a string from the stream.
        /// <remarks>
        /// We optimize strings by maintaining a hash table of each unique string 
        /// we have seen.  Each string is sent with as an integer index into the table.
        /// When a new string is encountered, it's index is followed by the string value.
        /// </remarks>
        /// </summary>
        /// <returns>Returns the string</returns>
        string ReadString();

        /// <summary>
        /// Read an array of strings from the stream.
        /// </summary>
        /// <returns>Returns an array of string values</returns>
        string[] ReadStringArray();

        /// <summary>
        /// Returns an Int32 value from the stream.
        /// </summary>
        /// <returns>An Int32 value.</returns>
        Int32 ReadInt32();

        /// <summary>
        /// Returns an array of Int32 values from the stream.
        /// </summary>
        /// <returns>An array of Int32 values.</returns>
        Int32[] ReadInt32Array();

        /// <summary>
        /// Returns an Int64 value from the stream.
        /// </summary>
        /// <returns>An Int64 value.</returns>
        Int64 ReadInt64();

        /// <summary>
        /// Returns an array of Int64 values from the stream.
        /// </summary>
        /// <returns>An array of Int64 values.</returns>
        Int64[] ReadInt64Array();

        /// <summary>
        /// Returns a UInt32 value from the stream.
        /// </summary>
        /// <returns>A UInt32 value.</returns>
        UInt32 ReadUInt32();

        /// <summary>
        /// Returns an array of UInt32 values from the stream.
        /// </summary>
        /// <returns>An array of UInt32 values.</returns>
        UInt32[] ReadUInt32Array();

        /// <summary>
        /// Returns a UInt64 value from the stream.
        /// </summary>
        /// <returns>A UInt64 value.</returns>
        UInt64 ReadUInt64();

        /// <summary>
        /// Returns an array of UInt64 values from the stream.
        /// </summary>
        /// <returns>An array of UInt64 values.</returns>
        UInt64[] ReadUInt64Array();

        /// <summary>
        /// Returns a double value from the stream.
        /// </summary>
        /// <returns>A double value.</returns>
        double ReadDouble();

        /// <summary>
        /// Returns an array of double values from the stream.
        /// </summary>
        /// <returns>An array of double values.</returns>
        double[] ReadDoubleArray();

        /// <summary>
        /// Returns a TimeSpan value from the stream.
        /// </summary>
        /// <returns>A TimeSpan value.</returns>
        TimeSpan ReadTimeSpan();

        /// <summary>
        /// Returns an array of TimeSpan values from the stream.
        /// </summary>
        /// <returns>An array of TimeSpan values.</returns>
        TimeSpan[] ReadTimeSpanArray();

        /// <summary>
        /// Returns a DateTime value from the stream.
        /// </summary>
        /// <returns>A DateTime value.</returns>
        DateTime ReadDateTime();

        /// <summary>
        /// Returns an array of DateTime values from the stream.
        /// </summary>
        /// <returns>An array of DateTime values.</returns>
        DateTime[] ReadDateTimeArray();

        /// <summary>
        /// Returns a DateTimeOffset value from the stream.
        /// </summary>
        /// <returns>A DateTimeOffset value.</returns>
        DateTimeOffset ReadDateTimeOffset();

        /// <summary>
        /// Returns an array of DateTimeOffset values from the stream.
        /// </summary>
        /// <returns>An array of DateTimeOffset values.</returns>
        DateTimeOffset[] ReadDateTimeOffsetArray();

        /// <summary>
        /// Returns a Guid value from the stream.
        /// </summary>
        /// <returns>A Guid value.</returns>
        Guid ReadGuid();

        /// <summary>
        /// Returns an array of Guid values from the stream.
        /// </summary>
        /// <returns>An array of Guid values.</returns>
        Guid[] ReadGuidArray();

        /// <summary>
        /// Returns a field value from the stream that was written as an object
        /// </summary>
        /// <returns>An object value holding a value (see FieldType).</returns>
        object ReadField();

        /// <summary>
        /// Returns a field value from the stream for the specified field type
        /// </summary>
        /// <param name="fieldType">The field type of the next field</param>
        /// <returns>An object value holding a value (see FieldType).</returns>
        object ReadField(FieldType fieldType);

        /// <summary>
        /// Returns an array of field values from the stream.
        /// </summary>
        /// <returns>An array of objects each holding a value (see FieldType).</returns>
        object[] ReadFieldArray();
    }
}