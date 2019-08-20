using System;

namespace Loupe.Serialization
{
    /// <summary>
    /// This is a base class for any new serialization Exception types we define and for generic exceptions
    /// generated in Serialization.
    /// </summary>
    /// <remarks>Any generation of an ApplicationException in Serialization should probably use this class instead.</remarks>
    public class LoupeSerializationException : LoupeException
    {
        // This is a dummy wrapper around Loupe exceptions (for now)

        /// <summary>
        /// Initializes a new instance of the LoupeSerializationException class.
        /// </summary>
        /// <remarks>This contructor initializes the Message property of the new instance to a system-supplied
        /// message that describes the error and takes into account the current system culture.
        /// For more information, see the base constructor in Exception.</remarks>
        public LoupeSerializationException()
        {
            // Just the base default constructor
        }

        /// <summary>
        /// Initializes a new instance of the LoupeSerializationException class with a specified error message.
        /// </summary>
        /// <param name="message">The error message string.</param>
        /// <remarks>This constructor initializes the Message property of the new instance using the
        /// message parameter.  The InnerException property is left as a null reference.
        /// For more information, see the base contructor in Exception.</remarks>
        public LoupeSerializationException(string message)
            : base(message)
        {
            // Just the base constructor
        }

        /// <summary>
        /// Initializes a new instance of the LoupeSerializationException class with a specified error message.
        /// </summary>
        /// <param name="message">The error message string.</param>
        /// <param name="streamFailed">Indicates if the entire stream is now considered corrupt and no further packets can be retrieved.</param>
        /// <remarks>This constructor initializes the Message property of the new instance using the
        /// message parameter.  The InnerException property is left as a null reference.
        /// For more information, see the base contructor in Exception.</remarks>
        public LoupeSerializationException(string message, bool streamFailed)
            : base(message)
        {
            StreamFailed = streamFailed;
        }

        /// <summary>
        /// Initializes a new instance of the LoupeSerializationException class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message string.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a
        /// null reference if no inner exception is specified.</param>
        /// <remarks>An exception that is thrown as a direct result of a previous exception should include
        /// a reference to the previous exception in the innerException parameter.
        /// For more information, see the base constructor in Exception.</remarks>
        public LoupeSerializationException(string message, Exception innerException)
            : base(message, innerException)
        {
            // Just the base constructor
        }

        /// <summary>
        /// Initializes a new instance of the LoupeSerializationException class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message string.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a
        /// null reference if no inner exception is specified.</param>
        /// <param name="streamFailed">Indicates if the entire stream is now considered corrupt and no further packets can be retrieved.</param>
        /// <remarks>An exception that is thrown as a direct result of a previous exception should include
        /// a reference to the previous exception in the innerException parameter.
        /// For more information, see the base constructor in Exception.</remarks>
        public LoupeSerializationException(string message, Exception innerException, bool streamFailed)
            : base(message, innerException)
        {
            StreamFailed = streamFailed;
        }

        /// <summary>
        /// Indicates if the exception is a stream error, so no further packets can be serialized
        /// </summary>
        public bool StreamFailed { get; private set; }

    }
}
