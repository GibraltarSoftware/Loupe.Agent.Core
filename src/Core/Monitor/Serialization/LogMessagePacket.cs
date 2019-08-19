using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using Gibraltar.Messaging;
using Gibraltar.Serialization;
using Loupe.Extensibility.Data;
using Loupe.Logging;

#pragma warning disable 1591

namespace Gibraltar.Monitor.Serialization
{
    public class LogMessagePacket : GibraltarPacket, IUserPacket, IPacket, ILogMessage, IComparable<LogMessagePacket>, IEquatable<LogMessagePacket>
    {
        private readonly ISessionPacketCache m_SessionPacketCache; //used for rehydration

        private Guid m_ID;
        private LogMessageSeverity m_Severity;
        private string m_LogSystem; // The major log system it comes from, eg. "Log4net", "Trace", "Gibraltar", "ELF"
        private string m_CategoryName; // The subsystem category, eg. the LoggerName from Log4Net
        private string m_UserName;
        private string m_Caption;
        private string m_Description;
        private string m_Details;
        private IExceptionInfo[] m_ExceptionChain;
        private string m_MethodName;
        private string m_ClassName;
        private string m_FileName;
        private int m_LineNumber;
        private int m_ThreadIndex; // The UNIQUE index assigned by Gibraltar.Agent to identify the thread.
        private int m_ThreadId; // The unique-at-any-one-time-but-not-for-the-whole-process-lifetime ManagedThreadId from .NET.
        private readonly bool m_SuppressNotification; // Read only, for now.

        //the following are generated fields and are not persisted
        private string m_Message; //a concatenated caption & description for GLV.
        private IPrincipal m_Principal;

        public LogMessagePacket()
        {
            Id = Guid.NewGuid();
            m_SuppressNotification = Gibraltar.Messaging.Publisher.QueryThreadMustNotNotify();
        }

        internal LogMessagePacket(ISessionPacketCache sessionPacketCache)
        {
            m_SessionPacketCache = sessionPacketCache;
        }

        #region Public Properties and methods

        public void SetSourceInfo(IMessageSourceProvider sourceProvider)
        {
            if (sourceProvider != null)
            {
                MethodName = sourceProvider.MethodName;
                ClassName = sourceProvider.ClassName;
                FileName = sourceProvider.FileName;
                LineNumber = sourceProvider.LineNumber;
            }
        }


        public Guid Id { get { return m_ID; } private set { m_ID = value; } }

        public LogMessageSeverity Severity { get { return m_Severity; } set { m_Severity = value; } }

        public string LogSystem { get { return m_LogSystem; } set { m_LogSystem = value; } }

        public string CategoryName { get { return m_CategoryName; } set { m_CategoryName = value; } }

        public string UserName { get { return m_UserName; } set { m_UserName = value; } }

        /// <summary>
        /// Optional.  Extended user information related to this message
        /// </summary>
        public ApplicationUserPacket UserPacket { get; set; }

        /// <summary>
        /// Optional.  The raw user principal, used for deferred user lookup
        /// </summary>
        public IPrincipal Principal
        {
            get => m_Principal;
            set
            {
                m_Principal = value;
                m_UserName = m_Principal?.Identity?.Name ?? m_UserName; 
            }
        }

        public string MethodName { get { return m_MethodName; } set { m_MethodName = value; } }

        public string ClassName { get { return m_ClassName; } set { m_ClassName = value; } }

        public string FileName { get { return m_FileName; } set { m_FileName = value; } }

        public int LineNumber { get { return m_LineNumber; } set { m_LineNumber = value; } }

        public int ThreadId { get { return m_ThreadId; } set { m_ThreadId = value; } }

        public int ThreadIndex { get { return m_ThreadIndex; } set { m_ThreadIndex = value; } }

        /// <summary>
        /// The thread info packet for our Thread Id. Must be set for the packet to be written to a stream.
        /// </summary>
        public ThreadInfoPacket ThreadInfoPacket { get; set; }

        public IThreadInfo ThreadInfo { get { return ThreadInfoPacket; } }

        /// <summary>
        /// The session this log message refers to
        /// </summary>
        ISession ILogMessage.Session { get { return null; } }

        int ILogMessage.DomainId { get { return ThreadInfo.DomainId; } }

        string ILogMessage.DomainName { get { return ThreadInfo.DomainName; } }

        bool ILogMessage.IsBackground { get { return ThreadInfo.IsBackground; } }

        bool ILogMessage.IsThreadPoolThread { get { return ThreadInfo.IsThreadPoolThread; } }

        /// <summary>
        /// Indicates if the log message has related thread information.  If false, some calls to thread information may throw exceptions.
        /// </summary>
        public bool HasThreadInfo { get { return ThreadInfoPacket != null; } }

        /// <summary>
        ///  Provide TimeStamp as DateTime for GLV (SourceGrid doesn't do DateTimeOffset)
        /// </summary>
        /// <remarks>Added for GLV support</remarks>
        public DateTime TimestampDateTime { get { return Timestamp.DateTime; } }

        /// <summary>A combined caption &amp; description</summary>
        /// <remarks>Added for GLV support</remarks>
        public string Message
        {
            get
            {
                if (m_Message == null) //that's deliberate - null means not calculated, empty string means calculated as empty.
                {
                    bool haveCaption = (string.IsNullOrEmpty(m_Caption) == false);
                    bool haveDescription = (string.IsNullOrEmpty(m_Caption) == false);

                    if (haveCaption && haveDescription)
                    {
                        m_Message = StringReference.GetReference(m_Caption + "\r\n" + m_Description);
                    }
                    else if (haveCaption)
                    {
                        m_Message = m_Caption;
                    }
                    else if (haveDescription)
                    {
                        m_Message = m_Description;
                    }
                    else
                    {
                        //use an empty string - it's empty. then we won't do this property check again.
                        m_Message = string.Empty;
                    }
                }

                return m_Message;
            }
        }

        /// <summary>
        /// A display name for the thread, returning the thread Id if no name is available.
        /// </summary>
        /// <remarks>Added for GLV support</remarks>
        public string ThreadName
        {
            get
            {
                return (ThreadInfoPacket != null) && (string.IsNullOrEmpty(ThreadInfoPacket.ThreadName) == false) ?
                    ThreadInfoPacket.ThreadName : ThreadId.ToString();
            }
        }

        /// <summary>
        /// A display string for the full class and method if available, otherwise an empty string.
        /// </summary>
        /// <remarks>Added for GLV support</remarks>
        public string MethodFullName
        {
            get
            {
                return ((string.IsNullOrEmpty(ClassName) == false) && (string.IsNullOrEmpty(MethodName) == false)) ?
                    StringReference.GetReference(ClassName + "." + MethodName) : string.Empty;
            }
        }

        /// <summary>
        /// A display string for the full file name and line number if available, otherwise an empty string.
        /// </summary>
        /// <remarks>Added for GLV support</remarks>
        public string SourceCodeLocation
        {
            get
            {
                return (string.IsNullOrEmpty(FileName) == false) ?
                    StringReference.GetReference(string.Format("{0} ({1:N0})", FileName, LineNumber))
                    : string.Empty;
            }
        }

        /// <summary>
        /// Captures the provided exception immediately.
        /// </summary>
        /// <param name="newException"></param>
        public void SetException(Exception newException)
        {
            m_ExceptionChain = ExceptionToArray(newException); // this handles a null Exception, never returns null
        }

        /// <summary>
        /// Whether or not this log message includes attached Exception information.
        /// </summary>
        public bool HasException
        {
            get
            {
                var exceptionInfo = Exceptions;
                return ((exceptionInfo != null) && (exceptionInfo.Length > 0));
            }
        }

        /// <summary>
        /// Indicates if the class name and method name are available.
        /// </summary>
        public bool HasMethodInfo { get { return !string.IsNullOrEmpty(ClassName); } }

        /// <summary>
        /// Indicates if the file name and line number are available.
        /// </summary>
        public bool HasSourceLocation { get { return !string.IsNullOrEmpty(FileName); } }

        public IExceptionInfo[] Exceptions
        {
            get
            {
                return m_ExceptionChain;
            }
        }

        public IExceptionInfo Exception
        {
            get
            {
                var exceptionInfo = Exceptions;
                if ((exceptionInfo == null) || (exceptionInfo.Length == 0))
                    return null;

                return exceptionInfo[0];
            }
        }

        /// <summary>
        /// Normalize the exception pointers to a single list.
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public static IList<IExceptionInfo> ExceptionsList(IExceptionInfo exception)
        {
            List<IExceptionInfo> exceptions = new List<IExceptionInfo>();

            IExceptionInfo innerException = exception;
            while (innerException != null)
            {
                exceptions.Add(innerException);
                innerException = innerException.InnerException;
            }

            return exceptions;
        }

        /// <summary>
        /// A single line caption
        /// </summary>
        public string Caption
        {
            get { return m_Caption; }
            set
            {
                m_Caption = value;

                //and clear our message so it'll get recalculated.
                m_Message = null;
            }
        }

        /// <summary>
        /// A multi line description
        /// </summary>
        public string Description
        {
            get { return m_Description; }
            set
            {
                m_Description = value;

                //and clear our message so it'll get recalculated.
                m_Message = null;
            }
        }

        /// <summary>
        /// XML details for this log message
        /// </summary>
        public virtual string Details { get { return m_Details; } set { m_Details = value; } }

        /// <summary>
        /// True if the message was issued from a Notifier thread which needs to suppress notification about this message.
        /// </summary>
        public bool SuppressNotification { get { return m_SuppressNotification; } }

        public override void FixData()
        {
            base.FixData();

            // Swap all strings for their StringReference equivalent
            StringReference.SwapReference(ref m_LogSystem);
            StringReference.SwapReference(ref m_CategoryName);
            StringReference.SwapReference(ref m_UserName);
            StringReference.SwapReference(ref m_Caption);
            StringReference.SwapReference(ref m_Description);
            StringReference.SwapReference(ref m_Details);
            StringReference.SwapReference(ref m_MethodName);
            StringReference.SwapReference(ref m_ClassName);
            StringReference.SwapReference(ref m_FileName);
        }

        public override string ToString()
        {
            string text = string.Format(CultureInfo.CurrentCulture, "{0:d} {0:t}: {1}", Timestamp, Caption);
            return text;
        }

        public int CompareTo(LogMessagePacket other)
        {
            return CompareTo((ILogMessage)other);
        }

        public int CompareTo(ILogMessage other)
        {
            //First do a quick match on Guid.  this is the only case we want to return zero (an exact match)
            if (Id == other.Id)
                return 0;

            //now we want to sort by our nice increasing sequence #
            int compareResult = Sequence.CompareTo(other.Sequence);

#if DEBUG
            Debug.Assert(compareResult != 0); //no way we should ever get an equal at this point.
#endif

            return compareResult;
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public override bool Equals(object other)
        {
            //use our type-specific override
            return Equals(other as LogMessagePacket);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(LogMessagePacket other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return Equals((ILogMessage)other);
        }

        /// <summary>
        /// Determines if the provided object is identical to this object.
        /// </summary>
        /// <param name="other">The object to compare this object to</param>
        /// <returns>True if the objects represent the same data.</returns>
        public bool Equals(ILogMessage other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            return ((Id == other.Id)
                 && (Caption == other.Caption)
                 && (Description == other.Description)
                 && (Details == other.Details)
                 && (Severity == other.Severity)
                 && (LogSystem == other.LogSystem)
                 && (CategoryName == other.CategoryName)
                 && (UserName == other.UserName)
                 && (MethodName == other.MethodName)
                 && (ClassName == other.ClassName)
                 && (FileName == other.FileName)
                 && (LineNumber == other.LineNumber)
                 && (ThreadId == other.ThreadId)
                 && (base.Equals(other)));
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
        /// an int representing the hash code calculated for the contents of this object
        /// </returns>
        public override int GetHashCode()
        {
            int myHash = base.GetHashCode(); // Fold in hash code for inherited base type

            myHash ^= (int)Severity; // Fold in Severity (enum) as an int as its own hash code
            myHash ^= Id.GetHashCode(); // Fold in hash code for GUID
            if (Caption != null) myHash ^= Caption.GetHashCode(); // Fold in hash code for string Caption
            if (Description != null) myHash ^= Description.GetHashCode(); // Fold in hash code for string Caption
            if (Details != null) myHash ^= Details.GetHashCode(); // Fold in hash code for string Caption
            if (LogSystem != null) myHash ^= LogSystem.GetHashCode(); // Fold in hash code for string LogSystem
            if (CategoryName != null) myHash ^= CategoryName.GetHashCode(); // Fold in hash code for string CategoryName
            if (UserName != null) myHash ^= UserName.GetHashCode(); // Fold in hash code for string UserName
            if (MethodName != null) myHash ^= MethodName.GetHashCode(); // Fold in hash code for string MethodName
            if (ClassName != null) myHash ^= ClassName.GetHashCode(); // Fold in hash code for string ClassName
            if (FileName != null) myHash ^= FileName.GetHashCode(); // Fold in hash code for string FileName
            myHash ^= LineNumber; // Fold in LineNumber int as its own hash code
            myHash ^= ThreadId; // Fold in ThreadId int as its own hash code

            // Session member is not used in Equals, so we can't use it in hash calculation!

            return myHash;
        }

        #endregion

        #region IPacket Members

        /// <summary>
        /// The current serialization version
        /// </summary>
        /// <remarks>
        /// <para>Version 2: Added Description and Details string fields.</para>
        /// <para>Added ThreadIndex field without bumping the version because old code would simply fail to accept data
        /// from new code.</para>
        /// </remarks>
        private const int SerializationVersion = 3;

        /// <summary>
        /// The list of packets that this packet depends on.
        /// </summary>
        /// <returns>An array of IPackets, or null if there are no dependencies.</returns>
        IPacket[] IPacket.GetRequiredPackets()
        {
            // We now hold the ThreadId ourselves, so we depend on the ThreadInfoPacket
#if DEBUG
            if (ReferenceEquals(ThreadInfoPacket, null))
            {
                //There is no thread info packet set in the log message.
                if (Debugger.IsAttached)
                    Debugger.Break();
                return null;
            }
#endif

            //we always depend on the Thread Info Packet; we depend on the Application User packet if it's set (may not be)
            return (UserPacket == null) ? new IPacket[] { ThreadInfoPacket } : new IPacket[] { ThreadInfoPacket, UserPacket };
        }

        PacketDefinition IPacket.GetPacketDefinition()
        {
            const string typeName = nameof(LogMessagePacket);
            var definition = new PacketDefinition(typeName, SerializationVersion, true);

            definition.Fields.Add("ID", FieldType.Guid);
            definition.Fields.Add("Caption", FieldType.String);
            definition.Fields.Add("Severity", FieldType.Int32);
            definition.Fields.Add("LogSystem", FieldType.String);
            definition.Fields.Add("CategoryName", FieldType.String);
            definition.Fields.Add("UserName", FieldType.String);
            definition.Fields.Add("Description", FieldType.String); // Added in version 2.
            definition.Fields.Add("Details", FieldType.String); // Added in version 2.

            // ManagedThreadId isn't unique, so we need to add one that actually is (but not bumping version).
            definition.Fields.Add("ThreadIndex", FieldType.Int32);

            definition.Fields.Add("ThreadId", FieldType.Int32);
            definition.Fields.Add("MethodName", FieldType.String);
            definition.Fields.Add("ClassName", FieldType.String);
            definition.Fields.Add("FileName", FieldType.String);
            definition.Fields.Add("LineNumber", FieldType.Int32);


            // Now the Exception info, split into four arrays of strings to serialize better.
            definition.Fields.Add("TypeNames", FieldType.StringArray);
            definition.Fields.Add("Messages", FieldType.StringArray);
            definition.Fields.Add("Sources", FieldType.StringArray);
            definition.Fields.Add("StackTraces", FieldType.StringArray);

            // Added in version 3
            definition.Fields.Add("ApplicationUserId", FieldType.Guid); 

            return definition;
        }

        void IPacket.WriteFields(PacketDefinition definition, SerializedPacket packet)
        {
            // We depend on the ThreadInfoPacket!
#if DEBUG
            Debug.Assert(ThreadInfoPacket != null);
            Debug.Assert(ThreadInfoPacket.ThreadId == ThreadId);
#endif

            packet.SetField("ID", m_ID);
            packet.SetField("Caption", m_Caption);
            packet.SetField("Severity", (int)m_Severity);
            packet.SetField("LogSystem", m_LogSystem);
            packet.SetField("CategoryName", m_CategoryName);
            packet.SetField("UserName", m_UserName);
            packet.SetField("Description", m_Description);
            packet.SetField("Details", m_Details);

            packet.SetField("ThreadIndex", m_ThreadIndex);

            // These have been fully integrated here from the former CallInfoPacket
            packet.SetField("ThreadId", m_ThreadId);
            packet.SetField("MethodName", m_MethodName);
            packet.SetField("ClassName", m_ClassName);
            packet.SetField("FileName", m_FileName);
            packet.SetField("LineNumber", m_LineNumber);

            // Now the Exception info...

            // Because serialization supports single type arrays, it's most convenient
            // to reorganize our exceptions into parallel arrays of their base types
            IExceptionInfo[] exceptions = Exceptions; // Get the array of ExceptionInfo
            int arrayLength = exceptions == null ? 0 : exceptions.Length;
            string[] typeNames = new string[arrayLength];
            string[] messages = new string[arrayLength];
            string[] sources = new string[arrayLength];
            string[] stackTraces = new string[arrayLength];

            if (exceptions != null)
            {
                for (int i = 0; i < arrayLength; i++)
                {
                    typeNames[i] = exceptions[i].TypeName;
                    messages[i] = exceptions[i].Message;
                    sources[i] = exceptions[i].Source;
                    stackTraces[i] = exceptions[i].StackTrace;
                }
            }

            packet.SetField("TypeNames", typeNames);
            packet.SetField("Messages", messages);
            packet.SetField("Sources", sources);
            packet.SetField("StackTraces", stackTraces);
            
            packet.SetField("ApplicationUserId", (UserPacket == null) ? Guid.Empty : UserPacket.ID);
        }

        void IPacket.ReadFields(PacketDefinition definition, SerializedPacket packet)
        {
            switch (definition.Version)
            {
                case 1:
                case 2: //two just adds fields.
                case 3: //three just adds one field.
                    packet.GetField("ID", out m_ID);
                    packet.GetField("Caption", out m_Caption);

                    // Hmmm, it's tricky to handle the enum with an out parameter; use a temporary int and cast it.
                    packet.GetField("Severity", out int severity);
                    m_Severity = (LogMessageSeverity)severity;

                    packet.GetField("LogSystem", out m_LogSystem);
                    packet.GetField("CategoryName", out m_CategoryName);
                    packet.GetField("UserName", out m_UserName);

                    if (definition.Version >= 2)
                    {
                        packet.GetField("Description", out m_Description);
                        packet.GetField("Details", out m_Details);
                    }

                    // These have now been fully integrated here from the former CallInfoPacket
                    packet.GetField("ThreadId", out m_ThreadId);
                    packet.GetField("MethodName", out m_MethodName);
                    packet.GetField("ClassName", out m_ClassName);
                    packet.GetField("FileName", out m_FileName);
                    packet.GetField("LineNumber", out m_LineNumber);

                    if (definition.Fields.ContainsKey("ThreadIndex"))
                    {
                        packet.GetField("ThreadIndex", out m_ThreadIndex);
                        if (m_ThreadIndex == 0)
                            m_ThreadIndex = m_ThreadId; // Zero isn't legal, so it must not have had it.  Fall back to ThreadId.
                    }
                    else
                    {
                        m_ThreadIndex = m_ThreadId; // Oops, older code doesn't have it, so use the ThreadId to fake it.
                    }

                    //we now know enough to get our thread info packet (if we don't, we can't re-serialize ourselves)
                    if (m_SessionPacketCache.Threads.TryGetValue(m_ThreadIndex, out var threadInfo))
                    {
                        ThreadInfoPacket = threadInfo.Packet;
                    }

                    // Now the Exception info...

                    packet.GetField("TypeNames", out string[] typeNames);
                    packet.GetField("Messages", out string[] messages);
                    packet.GetField("Sources", out string[] sources);
                    packet.GetField("StackTraces", out string[] stackTraces);

                    //these are supposed to be parallel arrays - assume they're all the same size.
                    int arrayLength = typeNames.GetLength(0);
                    IExceptionInfo[] exceptions = new IExceptionInfo[arrayLength]; // local holder to build it up

                    IExceptionInfo lastException = null;
                    for (int i = 0; i < arrayLength; i++)
                    {
                        IExceptionInfo exception = new ExceptionInfoPacket()
                                                       {
                                                           TypeName = typeNames[i],
                                                           Message = messages[i],
                                                           Source = sources[i],
                                                           StackTrace = stackTraces[i]
                                                       };
                        exceptions[i] = exception;
                        if (lastException != null)
                            ((ExceptionInfoPacket)lastException).InnerException = exception; //we are the inner exception to our parent.

                        lastException = exception;
                    }

                    m_ExceptionChain = exceptions; // Set the rehydrated ExceptionInfo[] array property

                    if (definition.Version >= 3)
                    {
                        packet.GetField("ApplicationUserId", out Guid applicationUserId);

                        //we now know enough to get our user packet now if it was specified..
                        if (m_SessionPacketCache.Users.TryGetValue(applicationUserId, out var applicationUser))
                        {
                            UserPacket = applicationUser.Packet;
                        }
                    }

                    break;
            }
        }

        /// <summary>
        /// Optimized deserialization of a LogMessagePacket based on the current packet definition
        /// </summary>
        public new void ReadFieldsFast(IFieldReader reader)
        {
            base.ReadFieldsFast(reader);

            m_ID = reader.ReadGuid();
            m_Caption = reader.ReadString();
            m_Severity = (LogMessageSeverity)reader.ReadInt32();
            m_LogSystem = reader.ReadString();
            m_CategoryName = reader.ReadString();
            m_UserName = reader.ReadString();
            m_Description = reader.ReadString();
            m_Details = reader.ReadString();
            m_ThreadIndex = reader.ReadInt32();
            m_ThreadId = reader.ReadInt32();
            m_MethodName = reader.ReadString();
            m_ClassName = reader.ReadString();
            m_FileName = reader.ReadString();
            m_LineNumber = reader.ReadInt32();
            string[] typeNames = reader.ReadStringArray();
            string[] messages = reader.ReadStringArray();
            string[] sources = reader.ReadStringArray();
            string[] stackTraces = reader.ReadStringArray();
            Guid applicationUserId = reader.ReadGuid();

            if (m_ThreadIndex == 0)
                m_ThreadIndex = m_ThreadId; // Zero isn't legal, so it must not have had it.  Fall back to ThreadId.

            //we now know enough to get our thread info packet (if we don't, we can't re-serialize ourselves)
            if (m_SessionPacketCache.Threads.TryGetValue(m_ThreadIndex, out var threadInfo))
            {
                ThreadInfoPacket = threadInfo.Packet;
            }

            if (applicationUserId != Guid.Empty)
            {
                m_SessionPacketCache.Users.TryGetValue(applicationUserId, out var applicationUser);
                UserPacket = applicationUser.Packet;
            }

            //these are supposed to be parallel arrays - assume they're all the same size.
            int arrayLength = typeNames.GetLength(0);
            var exceptions = new IExceptionInfo[arrayLength]; // local holder to build it up

            IExceptionInfo lastException = null;
            for (int i = 0; i < arrayLength; i++)
            {
                IExceptionInfo exception = new ExceptionInfoPacket()
                {
                    TypeName = typeNames[i],
                    Message = messages[i],
                    Source = sources[i],
                    StackTrace = stackTraces[i],
                };
                exceptions[i] = exception;
                if (lastException != null)
                    ((ExceptionInfoPacket)lastException).InnerException = exception; //we are the inner exception to our parent.
                lastException = exception;
            }

            m_ExceptionChain = exceptions; // Set the rehydrated ExceptionInfo[] array property
        }
        #endregion

        #region Private Properties and Methods

        private static IExceptionInfo[] ExceptionToArray(Exception exception)
        {
            // This must accept a null Exception and never return a null, use empty array;
            if (exception == null)
            {
                return new IExceptionInfo[0];
            }

            int count = 1; // Otherwise, we have at least one
            Exception innerException = exception.InnerException;
            while (innerException != null) // Count up how big to make the array
            {
                count++;
                innerException = innerException.InnerException;
            }

            IExceptionInfo[] exceptions = new IExceptionInfo[count];

            //now start serializing them into the array...
            exceptions[0] = new ExceptionInfoPacket(exception);

            innerException = exception.InnerException;
            int index = 0;
            while (innerException != null)
            {
                index++;
                exceptions[index] = new ExceptionInfoPacket(innerException);
                ((ExceptionInfoPacket)exceptions[index - 1]).InnerException = exceptions[index]; //we are the inner exception to the previous one.
                innerException = innerException.InnerException;
            }
            return exceptions;
        }

        #endregion
    }
}
