using System;

namespace Loupe.Core.Server.Client
{
    /// <summary>
    /// Exceptions related to network operations
    /// </summary>
    public class LoupeNetworkException : LoupeException
    {

        /// <summary>
        /// Initializes a new instance of the LoupeNetworkException class.
        /// </summary>
        /// <remarks>This constructor initializes the Message property of the new instance to a system-supplied
        /// message that describes the error and takes into account the current system culture.
        /// For more information, see the base constructor in Exception.</remarks>
        public LoupeNetworkException()
        {
            // Just the base default constructor
        }

        /// <summary>
        /// Initializes a new instance of the LoupeNetworkException class with a specified error message.
        /// </summary>
        /// <param name="message">The error message string.</param>
        /// <remarks>This constructor initializes the Message property of the new instance using the
        /// message parameter.  The InnerException property is left as a null reference.
        /// For more information, see the base constructor in Exception.</remarks>
        public LoupeNetworkException(string message)
            : base(message)
        {
            // Just the base constructor
        }

        /// <summary>
        /// Initializes a new instance of the LoupeNetworkException class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message string.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a
        /// null reference if no inner exception is specified.</param>
        /// <remarks>An exception that is thrown as a direct result of a previous exception should include
        /// a reference to the previous exception in the innerException parameter.
        /// For more information, see the base constructor in Exception.</remarks>
        public LoupeNetworkException(string message, Exception innerException)
            : base(message, innerException)
        {
            // Just the base constructor
        }
    }
}
