using System;
using Loupe.Extensibility.Data;
using IExceptionInfo = Gibraltar.Agent.Data.IExceptionInfo;
using ILogMessage = Gibraltar.Agent.Data.ILogMessage;

namespace Gibraltar.Agent.Internal
{
    /// <summary>
    /// An interface for accessing log message data.
    /// </summary>
    /// <remarks>Most string properties of this interface (except where otherwise noted) will not return null values.</remarks>
    internal class LogMessageInfo : ILogMessage, Loupe.Extensibility.Data.ILogMessage
    {
        private readonly Loupe.Extensibility.Data.ILogMessage m_Message;
        private readonly ExceptionInfo m_Exception;

        internal LogMessageInfo(Loupe.Extensibility.Data.ILogMessage message)
        {
            m_Message = message;
            m_Exception = ConvertExceptionInfo(message);
        }

        /// <summary>
        /// The session this log message refers to
        /// </summary>
        public ISession Session => throw new NotSupportedException("Access the current session summary information from the Gibraltar.Agent.Log object.");

        /// <summary>
        /// A Guid identifying this log message, unique across all sessions.
        /// </summary>
        public Guid Id => m_Message.Id;

        /// <summary>
        /// The sequence number assigned to this log message, unique within this session.
        /// </summary>
        public long Sequence => m_Message.Sequence;

        /// <summary>
        /// The timestamp of this log message.
        /// </summary>
        public DateTimeOffset Timestamp => m_Message.Timestamp;

        /// <summary>
        /// The timestamp of this log message in the time zone the user has requested for display.
        /// </summary>
        public DateTimeOffset DisplayTimestamp => m_Message.DisplayTimestamp;

        /// <summary>
        /// The severity of this log message (from Critical = 1 to Verbose = 16).
        /// </summary>
        public LogMessageSeverity Severity => (LogMessageSeverity)m_Message.Severity;

        /// <summary>
        /// The internal severity of this log message (from Critical = 1 to Verbose = 16).
        /// </summary>
        Loupe.Extensibility.Data.LogMessageSeverity Loupe.Extensibility.Data.ILogMessage.Severity => m_Message.Severity;

        /// <summary>
        /// The log system which issued this log message (e.g. "Trace" or "Gibraltar").
        /// </summary>
        public string LogSystem => m_Message.LogSystem;

        /// <summary>
        /// The dot-delimited hierarchical category for this log message.
        /// </summary>
        public string CategoryName => m_Message.CategoryName;

        /// <summary>
        /// An array of the individual category names within the specified category name which is period delimited.
        /// </summary>
        public string[] CategoryNames => m_Message.CategoryNames;

        /// <summary>
        /// The user name associated with this log message (often just the user who started the process).
        /// </summary>
        public string UserName => m_Message.UserName;

        /// <summary>
        /// The simple caption string for this log message.
        /// </summary>
        public string Caption => m_Message.Caption;

        /// <summary>
        /// The longer description for this log message.
        /// </summary>
        public string Description => m_Message.Description;

        /// <summary>
        /// The optional details XML for this log message (as a string). (Or null if none.)
        /// </summary>
        public string Details => m_Message.Details;

        /// <summary>
        /// The name of the method which originated this log message, unless unavailable.  (Can be null or empty.)
        /// </summary>
        public string MethodName => m_Message.MethodName;

        /// <summary>
        /// The full name of the class containing this method which originated the log message, unless unavailable.  (Can be null or empty.)
        /// </summary>
        public string ClassName => m_Message.ClassName;

        /// <summary>
        /// An array of the individual elements of the class and namespace hierarchy.
        /// </summary>
        public string[] ClassNames => m_Message.ClassNames;

        /// <summary>
        /// The full path to the file containing this definition of the method which originated the log message, if available.  (Can be null or empty.)
        /// </summary>
        public string FileName => m_Message.FileName;

        /// <summary>
        /// The line number in the file at which the this message originated, if available.
        /// </summary>
        public int LineNumber => m_Message.LineNumber;

        /// <summary>
        /// Whether or not this log message includes attached Exception information.
        /// </summary>
        public bool HasException => m_Message.HasException;

        /// <summary>
        /// The information about any Exception attached to this log message.  (Or null if none.)
        /// </summary>
        Loupe.Extensibility.Data.IExceptionInfo Loupe.Extensibility.Data.ILogMessage.Exception => throw new NotSupportedException("Use the agent-specific IExceptionInfo interface to access exception information");

        /// <summary>
        /// The information about any Exception attached to this log message.  (Or null if none.)
        /// </summary>
        public IExceptionInfo Exception => m_Exception;

        /// <summary>
        /// The managed thread ID of the thread which originated this log message.
        /// </summary>
        public int ThreadId => m_Message.ThreadId;

        /// <summary>
        /// The name of the thread which originated this log message.
        /// </summary>
        public string ThreadName => m_Message.ThreadName;

        /// <summary>
        /// The application domain identifier of the app domain which originated this log message.
        /// </summary>
        public int DomainId => m_Message.DomainId;

        /// <summary>
        /// The friendly name of the app domain which originated this log message.
        /// </summary>
        public string DomainName => m_Message.DomainName;

        /// <summary>
        /// Indicates whether the thread which originated this log message is a background thread.
        /// </summary>
        public bool IsBackground => m_Message.IsBackground;

        /// <summary>
        /// Indicates whether the thread which originated this log message is a threadpool thread.
        /// </summary>
        public bool IsThreadPoolThread => m_Message.IsThreadPoolThread;

        /// <summary>
        /// Indicates if the log message has related thread information.  If false, some calls to thread information may throw exceptions.
        /// </summary>
        public bool HasThreadInfo => m_Message.HasThreadInfo;

        /// <summary>
        /// Optional.  Details of the thread that recorded the log message
        /// </summary>
        public IThreadInfo ThreadInfo => m_Message.ThreadInfo;

        /// <summary>
        /// Indicates if the class name and method name are available.
        /// </summary>
        public bool HasMethodInfo => m_Message.HasMethodInfo;

        /// <summary>
        /// Indicates if the file name and line number are available.
        /// </summary>
        public bool HasSourceLocation => m_Message.HasSourceLocation;

        /// <summary>
        /// Returns a value indicating whether this log message is the same as another specified object.
        /// </summary>
        /// <param name="obj">Another object to compare this log message to.</param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
                return false; // We can't be equal to a null.

            ILogMessage other = obj as ILogMessage;
            if (ReferenceEquals(other, null) == false)
                return Equals(other);

            Loupe.Extensibility.Data.ILogMessage otherInternal = obj as Loupe.Extensibility.Data.ILogMessage;
            if (ReferenceEquals(otherInternal, null) == false)
                return Equals(otherInternal);

            return false; // Can't be equal to something that isn't an ILogMessage or a Data.ILogMessage.
        }

        /// <summary>
        /// Returns a value indicating whether this log message is the same as another specified log message.
        /// </summary>
        /// <param name="other">Another log message to compare this log message to.</param>
        /// <returns></returns>
        public bool Equals(ILogMessage other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return Id.Equals(other.Id);
        }

        /// <summary>
        /// Returns a value indicating whether this log message is the same as another specified log message.
        /// </summary>
        /// <param name="other">Another log message to compare this log message to.</param>
        /// <returns></returns>
        public bool Equals(Loupe.Extensibility.Data.ILogMessage other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return Id.Equals(other.Id);
        }

        /// <summary>
        /// Compares this log message to another and returns an indication of their relative order of occurrence.
        /// </summary>
        /// <param name="other">Another log message to compare this log message to.</param>
        /// <returns></returns>
        public int CompareTo(ILogMessage other)
        {
            int compare = Timestamp.CompareTo(other.Timestamp); // Compare by timestamp first, which works across sessions.

            if (compare == 0)
                compare = Sequence.CompareTo(other.Sequence); // Break ties by sequence number.

            if (compare == 0)
                compare = Id.CompareTo(other.Id); // Final tie-break by Guid.

            return compare;
        }

        /// <summary>
        /// Compares the current object with another object of the same type.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that indicates the relative order of the objects being compared. The return value has the following meanings: 
        ///                     Value 
        ///                     Meaning 
        ///                     Less than zero 
        ///                     This object is less than the <paramref name="other"/> parameter.
        ///                     Zero 
        ///                     This object is equal to <paramref name="other"/>. 
        ///                     Greater than zero 
        ///                     This object is greater than <paramref name="other"/>. 
        /// </returns>
        /// <param name="other">An object to compare with this object.
        ///                 </param>
        public int CompareTo(Loupe.Extensibility.Data.ILogMessage other)
        {
            int compare = Timestamp.CompareTo(other.Timestamp); // Compare by timestamp first, which works across sessions.

            if (compare == 0)
                compare = Sequence.CompareTo(other.Sequence); // Break ties by sequence number.

            if (compare == 0)
                compare = Id.CompareTo(other.Id); // Final tie-break by Guid.

            return compare;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        private static ExceptionInfo ConvertExceptionInfo(Loupe.Extensibility.Data.ILogMessage message)
        {
            if (message.HasException == false)
                return null;

            return new ExceptionInfo(message.Exception);
        }
    }
}