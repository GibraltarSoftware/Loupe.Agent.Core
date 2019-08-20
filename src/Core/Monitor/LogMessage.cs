using System;
using Loupe.Core.Monitor.Serialization;
using Loupe.Extensibility.Data;

namespace Loupe.Core.Monitor
{
    /// <summary>
    /// A Loupe log message.
    /// </summary>
    /// <remarks>This class pulls together references to a Session, ThreadInfo, and DataExtension associated
    /// with a specific LogMessagePacket.</remarks>
    public class LogMessage : IDisplayable, IDataObject, IComparable<LogMessage>, IEquatable<LogMessage>
    {
        private readonly Session m_Session;
        private readonly ThreadInfo m_ThreadInfo;
        private readonly ApplicationUser m_ApplicationUser;
        private readonly LogMessagePacket m_MessagePacket;
        private string[] m_CategoryNames;
        private string[] m_ClassNames;
        private int m_IndexOf = -1; //we rely on this so we know if it has been calculated

        internal LogMessage( Session session, ThreadInfo threadInfo, ApplicationUser applicationUser, LogMessagePacket messagePacket )
        {
            m_Session = session;
            m_ThreadInfo = threadInfo;
            m_ApplicationUser = applicationUser;
            m_MessagePacket = messagePacket;
        }

        #region Public Properties and Methods

        /// <summary>
        /// A globally unique ID for this message.
        /// </summary>
        public Guid Id { get { return m_MessagePacket.Id; } }
        
        /// <summary>
        /// The index of this log message in the messages collection.
        /// </summary>
        public int IndexOf
        {
            get
            {
                if (m_IndexOf == -1)
                {
                    m_IndexOf = m_Session.Messages.IndexOf(this);
                }

                return m_IndexOf;
            }
        }

        /// <summary>
        /// The underlying packet sequence number that ensures absolute, but non-monotonic, ordering of messages.
        /// </summary>
        public long Sequence { get { return m_MessagePacket.Sequence; } }

        /// <summary>
        /// The exact timestamp of this message.
        /// </summary>
        public DateTimeOffset Timestamp { get { return m_MessagePacket.Timestamp; } }

        /// <summary>
        /// The system that logged the message
        /// </summary>
        /// <remarks>Commonly Trace, Loupe, or Log4Net but can also be a user-specified system.</remarks>
        public string LogSystem { get { return m_MessagePacket.LogSystem; } }

        /// <summary>
        /// Our ThreadInfo for the thread which issued this log message.
        /// </summary>
        public ThreadInfo ThreadInfo { get { return m_ThreadInfo; } }

        /// <summary>
        /// Optional.  The application user associated with this message
        /// </summary>
        public ApplicationUser User { get { return m_ApplicationUser; } }

        /// <summary>
        /// The short name of the user that generated this message
        /// </summary>
        public string UserName { get { return m_MessagePacket.UserName; } }

        /// <summary>
        /// The numeric ID of the thread this message was logged on.
        /// </summary>
        public int ThreadId { get { return m_ThreadInfo.ThreadId; } }

        /// <summary>
        /// The display caption of the thread this message was logged on.
        /// </summary>
        public string ThreadName { get { return m_ThreadInfo.Caption; } }

        /// <summary>
        /// Indicates if the class name and method name are available.
        /// </summary>
        public bool HasMethodInfo { get { return !string.IsNullOrEmpty(m_MessagePacket.ClassName); } }

        /// <summary>
        /// If available, the full class name with namespace.  May be null.
        /// </summary>
        public string ClassName { get { return m_MessagePacket.ClassName; } }

        /// <summary>
        /// An array of the individual elements of the class and namespace hierarchy.
        /// </summary>
        public string[] ClassNames
        {
            get
            {
                //have we parsed it yet?  We don't want to do this every time, it ain't cheap.
                if (m_ClassNames == null)
                {
                    //no.
                    m_ClassNames = TextParse.ClassName(ClassName);
                }

                return m_ClassNames;
            }
        }

        /// <summary>
        /// If available, the method where this message was logged.  May be null.
        /// </summary>
        public string MethodName { get { return m_MessagePacket.MethodName; } }

        /// <summary>
        /// Indicates if the file name and line number are available.
        /// </summary>
        public bool HasSourceLocation { get { return !string.IsNullOrEmpty(m_MessagePacket.FileName); } }

        /// <summary>
        /// Indicates if the log message has related thread information.  If false, some calls to thread information may throw exceptions.
        /// </summary>
        public bool HasThreadInfo { get { return m_MessagePacket.HasThreadInfo; } }

        /// <summary>
        /// If available, the source file for the class and method of this message.  May be null.
        /// </summary>
        public string FileName { get { return m_MessagePacket.FileName; } }

        /// <summary>
        /// If available, the line number for the class and method of this message.  May be zero.
        /// </summary>
        public int LineNumber { get { return m_MessagePacket.LineNumber; } }

        /// <summary>
        /// A display caption for this message.
        /// </summary>
        public string Caption { get { return m_MessagePacket.Caption; } }

        /// <summary>
        /// A multi-line description included with the log message.
        /// </summary>
        public string Description { get { return m_MessagePacket.Description; } }

        /// <summary>
        /// An XML description document that goes along with the log message.
        /// </summary>
        public string Details { get { return m_MessagePacket.Details; } }

        /// <summary>
        /// The severity level of this message.
        /// </summary>
        public LogMessageSeverity Severity { get { return m_MessagePacket.Severity; } set { m_MessagePacket.Severity = value; } }

        /// <summary>
        /// The subsystem category under which this log message was issued.
        /// </summary>
        /// <remarks>For example, the logger name in log4net.</remarks>
        public string CategoryName { get { return m_MessagePacket.CategoryName; } }

        /// <summary>
        /// An array of the individual category names within the specified category name which is period delimited.
        /// </summary>
        public string[] CategoryNames
        {
            get
            {
                //have we parsed it yet?  We don't want to do this every time, it ain't cheap.
                if (m_CategoryNames == null)
                {
                    //no.
                    m_CategoryNames = TextParse.CategoryName(CategoryName);
                }

                return m_CategoryNames;
            }
        }

        /// <summary>
        ///  Provide TimeStamp as DateTime for GLV (SourceGrid doesn't do DateTimeOffset)
        /// </summary>
        /// <remarks>Added for GLV support</remarks>
        public DateTime TimestampDateTime { get { return Timestamp.DateTime; } }

        /// <summary>A combined caption &amp; description</summary>
        /// <remarks>Added for GLV support</remarks>
        public string Message { get { return m_MessagePacket.Message; } }

        /// <summary>
        /// A display string for the full class and method if available, otherwise an empty string.
        /// </summary>
        /// <remarks>Added for GLV support</remarks>
        public string MethodFullName { get { return m_MessagePacket.MethodFullName; } }

        /// <summary>
        /// A display string for the full file name and line number if available, otherwise an empty string.
        /// </summary>
        /// <remarks>Added for GLV support</remarks>
        public string SourceCodeLocation { get { return m_MessagePacket.SourceCodeLocation; } }

        /// <summary>
        /// The session this message is related to.
        /// </summary>
        public Session Session { get { return m_Session; } }

        /// <summary>
        /// True if there is an exception array associated with this message.
        /// </summary>
        public bool HasException { get { return m_MessagePacket.HasException; } }

        /// <summary>
        /// The outermost exception
        /// </summary>
        public IExceptionInfo Exception
        {
            get
            {
                return m_MessagePacket.Exception;
            }
        }

        /// <summary>
        /// The array of exceptions associated with this log message.
        /// </summary>
        public IExceptionInfo[] Exceptions
        {
            get
            {
                return m_MessagePacket.Exceptions;
            }
        }

        /// <summary>
        /// Compare this log message with another to determine if they are the same or how they should be sorted relative to each other.
        /// </summary>
        /// <remarks>LogMessage instances are sorted by the Sequence number property of the MessagePacket they encompass.</remarks>
        /// <param name="other"></param>
        /// <returns>0 for an exact match, otherwise the relationship between the two for sorting.</returns>
        public int CompareTo(LogMessage other)
        {
            //look at the metric packets to let them make the call
            return m_MessagePacket.Sequence.CompareTo(other.Sequence);
        }

        /// <summary>
        /// Determines if the provided LogMessage object is identical to this object.
        /// </summary>
        /// <param name="other">The LogMessage object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(LogMessage other)
        {
            // Careful, it could be null; check it without recursion
            if (ReferenceEquals(other, null))
            {
                return false; // Since we're a live object we can't be equal to a null instance.
            }

            //look at the metric packets to let them make the call
            return m_MessagePacket.Id.Equals(other.Id);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="obj">The object to compare this object to</param>
        /// <returns>True if the other object is also a LogMessage and represents the same data.</returns>
        public override bool Equals(object obj)
        {
            LogMessage otherMessage = obj as LogMessage;

            return Equals(otherMessage); // Just have type-specific Equals do the check (it even handles null)
        }

        /// <summary>
        /// Provides a representative hash code for objects of this type to spread out distribution
        /// in hash tables.
        /// </summary>
        /// <remarks>Objects which consider themselves to be Equal (a.Equals(b) returns true) are
        /// expected to have the same hash code.  Objects which are not Equal may have the same
        /// hash code, but minimizing such overlaps helps with efficient operation of hash tables.
        /// </remarks>
        /// <returns>
        /// An int representing the hash code calculated for the contents of this object.
        /// </returns>
        public override int GetHashCode()
        {
            int myHash = 0;
            
            if (m_MessagePacket != null) myHash ^= m_MessagePacket.Id.GetHashCode(); // Packet ID is all that Equals checks!

            return myHash;
        }
        
        /// <summary>
        /// Compares two LogMessage instances for equality.
        /// </summary>
        /// <param name="left">The LogMessage to the left of the operator</param>
        /// <param name="right">The LogMessage to the right of the operator</param>
        /// <returns>True if the two LogMessages are equal.</returns>
        public static bool operator==(LogMessage left, LogMessage right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return ReferenceEquals(right, null);
            }
            return left.Equals(right);
        }
        
        /// <summary>
        /// Compares two LogMessage instances for inequality.
        /// </summary>
        /// <param name="left">The LogMessage to the left of the operator</param>
        /// <param name="right">The LogMessage to the right of the operator</param>
        /// <returns>True if the two LogMessages are not equal.</returns>
        public static bool operator!=(LogMessage left, LogMessage right)
        {
            // We have to check if left is null (right can be checked by Equals itself)
            if (ReferenceEquals(left, null))
            {
                // If right is also null, we're equal; otherwise, we're unequal!
                return ! ReferenceEquals(right, null);
            }
            return ! left.Equals(right);
        }

        /// <summary>
        /// Compares if one LogMessage instance should sort less than another.
        /// </summary>
        /// <param name="left">The LogMessage to the left of the operator</param>
        /// <param name="right">The LogMessage to the right of the operator</param>
        /// <returns>True if the LogMessage to the left should sort less than the LogMessage to the right.</returns>
        public static bool operator<(LogMessage left, LogMessage right)
        {
            return (left.CompareTo(right) < 0);
        }

        /// <summary>
        /// Compares if one LogMessage instance should sort greater than another.
        /// </summary>
        /// <param name="left">The LogMessage to the left of the operator</param>
        /// <param name="right">The LogMessage to the right of the operator</param>
        /// <returns>True if the LogMessage to the left should sort greater than the LogMessage to the right.</returns>
        public static bool operator>(LogMessage left, LogMessage right)
        {
            return (left.CompareTo(right) > 0);
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return m_MessagePacket.Message;
        }

        #endregion

        #region Internal Properties and Methods

        internal LogMessagePacket MessagePacket { get { return m_MessagePacket; } }

        #endregion
    }
}