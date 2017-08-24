#pragma warning disable 1591
namespace Gibraltar.Serialization
{
    /// <summary>
    /// This is the set of data types that can be read/written using
    /// FieldReader/FieldWriter.
    /// </summary>
    /// <remarks>
    /// The enum starts at 1 to allow 0 to be clearly understood as unknown (and therefore fail)
    /// </remarks>
    public enum FieldType
    {
        Unknown = 0,
        Bool = 1,
        BoolArray = 2,
        String = 3,
        StringArray = 4,
        Int32 = 5,
        Int32Array = 6,
        Int64 = 7,
        Int64Array = 8,
        UInt32 = 9,
        UInt32Array = 10,
        UInt64 = 11,
        UInt64Array = 12,
        Double = 13,
        DoubleArray = 14,
        TimeSpan = 15,
        TimeSpanArray = 16,
        DateTime = 17,
        DateTimeArray = 18,
        Guid = 19,
        GuidArray = 20,
        DateTimeOffset = 21,
        DateTimeOffsetArray = 22
    }

    /// <summary>
    /// This is the set of encoding options for DateTime compression
    /// by FieldReader/FieldWriter (for FieldType.DateTime and elements
    /// of FieldType.DateTimeArray).
    /// <remarks>
    /// <para>Once these options are set, they should not be changed.  Extending
    /// with additional options should not be done without serious thought
    /// as to handling compatibility with prior versions of FieldReader!
    /// Enum values should never exceed 63 (encodes as single byte) without a
    /// a good reason.</para>
    /// <para>After the first four special cases, they must be added as
    /// Later/Earlier pairs (indicates sign bit for direction of offset so
    /// that unsigned encoding can be used for longer range before needing
    /// another byte to represent the value).  These encoding options take
    /// advantage of the opportunity to evenly divide the .NET Ticks by a
    /// larger clock resolution (eg. divide by 160,000 for 16ms resolution) used
    /// on typical platforms, to encode a smaller value and thus save bytes.</para>
    /// <para>The generic factor support is provided to allow for cases not
    /// anticipated at rollout, since new enum options can not be added without
    /// breaking older code.  Both SetFactor and SetReference cases expect another
    /// DateTime encoding to follow the value of Factor or Reference given (which
    /// could include the other Set... case and yet another DateTime encoding
    /// after that).</para>
    /// </remarks>
    /// </summary>
    internal enum DateTimeEncoding
    {
        RawTicks=0,             // Timestamp given by absolute Ticks (.NET, which are 100-nanosecond clicks)
        NewReference,           // Set ReferenceTime to this timestamp, by absolute Ticks (.NET)
        SetFactor,              // Set new generic factor for clock resolution (then a DateTime field follows)
        SetReference,           // Set ReferenceTime off this timestamp, by seconds (factor of 10,000,000)...
                                // ...(then a DateTime field follows, using this new ReferenceTime)

        // Read as if the names are in reverse... eg. as how many "16-ms ticks later" than ReferenceTime
        LaterTicksNet,          // Timestamp is this many .NET Ticks later than ReferenceTime
        EarlierTicksNet,        // ...earlier than ReferenceTime (equivalent to factor=1)
        LaterTicksFactor,       // Timestamp is this many generic factor clicks later than ReferenceTime
        EarlierTicksFactor,     // ...earlier than ReferenceTime (...based on most recent SetFactor value)

        // Now for the most common anticipated cases, so we don't have to SetFactor before we use them
        LaterTicks1s,           // Timestamp is this many 1-second clicks later than ReferenceTime
        EarlierTicks1s,         // ...earlier than ReferenceTime (equivalent to factor=10,000,000)
        LaterTicks100ms,        // ...100-millisecond clicks later...
        EarlierTicks100ms,      // ...earlier... (equivalent to factor=1,000,000)
        LaterTicks16ms,         // ...16-millisecond clicks later...
        EarlierTicks16ms,       // ...earlier... (equivalent to factor=160,000)
        LaterTicks10ms,         // ...10-milisecond clicks later...
        EarlierTicks10ms,       // ...earlier... (equivalent to factor=100,000)

        LaterTicks1ms,          // Timestamp is this many 1-millisecond clicks later than ReferenceTime
        EarlierTicks1ms,        // ...earlier than ReferenceTime (equivalent to factor=10,000)
        LaterTicks100us,        // ...100-microsecond clicks later...
        EarlierTicks100us,      // ...earlier... (equivalent to factor=1000)
        LaterTicks10us,         // ...10-microsecond clicks later...
        EarlierTicks10us,       // ...earlier... (equivalent to factor=100)
        LaterTicks1us,          // ...1-microsecond clicks later...
        EarlierTicks1us,        // ...earlier... (equivalent to factor=10)
    }
}
