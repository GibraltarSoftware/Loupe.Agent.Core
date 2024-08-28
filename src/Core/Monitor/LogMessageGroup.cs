using System;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// A group of log messages, typically 
    /// </summary>
    public class LogMessageGroup : IComparable<LogMessageGroup>
    {
        private LogMessageGroupCollection m_ChildGroups;

        internal LogMessageGroup(string name, LogMessageGroup parent, ILogMessage message)
        {
            //screwy but necessary - we need to have it set to the highest numeric value to start.
            MaxSeverity = LogMessageSeverity.Verbose;
            MaxSeverityWithChildren = LogMessageSeverity.Verbose;

            Name = name;
            Parent = parent;
            FullName = (parent == null) ? name : parent.FullName + "." + name;

            if (message != null)
            {
                AddMessage(message);
            }
        }

        #region Public Properties and Methods

        /// <summary>
        /// The name of this group (not fully qualified)
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Optional. The parent of this group (null for top level groups)
        /// </summary>
        public LogMessageGroup Parent { get; private set; }

        /// <summary>
        /// The worst-case severity of the log messages in this group or any group underneath it.
        /// </summary>
        public LogMessageSeverity MaxSeverityWithChildren { get; private set; }

        /// <summary>
        /// The worst-case severity of the log messages in this group
        /// </summary>
        public LogMessageSeverity MaxSeverity { get; private set; }

        /// <summary>
        /// The number of messages contained in this group including its child groups
        /// </summary>
        public int MessageCountWithChildren { get; private set; }

        /// <summary>
        /// The number of messages contained in this group
        /// </summary>
        public int MessageCount { get; private set; }

        /// <summary>
        /// The fully qualified name of this group.
        /// </summary>
        public string FullName { get; private set; }

        /// <summary>
        /// The collection of log message groups that are a child of this group.
        /// </summary>
        public LogMessageGroupCollection Groups
        {
            get
            {
                if (m_ChildGroups == null)
                {
                    m_ChildGroups = new LogMessageGroupCollection(this);
                }

                return m_ChildGroups;
            }
        }

        /// <summary>
        /// True if this is a leaf node, false if it has child groups.
        /// </summary>
        public bool IsLeaf
        {
            get
            {
                return ((m_ChildGroups == null) || (Groups.Count == 0));
            }
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Record the message as part of this group (and its parent groups)
        /// </summary>
        /// <param name="message"></param>
        internal void AddMessage(ILogMessage message)
        {
            //we need to count this message and roll up this count to our parents.
            MessageCount++;
            MaxSeverity = (LogMessageSeverity)Math.Min((int)message.Severity, (int)MaxSeverity);

            AddChildMessage(message); //this gets it propagating to parent and handles things we do for our child messages as well.
        }

        /// <summary>
        /// Record the message as part of a child of this group.
        /// </summary>
        /// <param name="message"></param>
        internal void AddChildMessage(ILogMessage message)
        {
            //we need to count this message and roll up this count to our parents.
            MessageCountWithChildren++;
            MaxSeverityWithChildren = (LogMessageSeverity)Math.Min((int)message.Severity, (int)MaxSeverityWithChildren);

            if (Parent != null)
            {
                Parent.AddChildMessage(message);
            }
        }

        #endregion

        /// <summary>
        /// Compares the current object with another object of the same type.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that indicates the relative order of the objects being compared. The return value has the following meanings: Value Meaning Less than zero This object is less than the <paramref name="other" /> parameter.Zero This object is equal to <paramref name="other" />. Greater than zero This object is greater than <paramref name="other" />. 
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public int CompareTo(LogMessageGroup other)
        {
            return Name.CompareTo(other.Name);
        }
    }
}
