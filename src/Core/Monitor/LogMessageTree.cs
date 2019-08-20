using System;
using System.Collections.Generic;
using Loupe.Extensibility.Data;



namespace Loupe.Core.Monitor
{
    /// <summary>
    /// An acyclic tree graph of log message groups.
    /// </summary>
    /// <remarks>Used to create hierarchies of groups of log messages and collect statistics on them.</remarks>
    public class LogMessageTree
    {
        private readonly Dictionary<string, LogMessageGroup> m_GroupFullNameCache = new Dictionary<string, LogMessageGroup>(StringComparer.OrdinalIgnoreCase);
        private readonly LogMessageGroupDelegate m_MessageGroupDelegate;
        private readonly LogMessageGroupFullNameDelegate m_FullNameDelegate;

        /// <summary>
        /// Delegate that can specify the fully qualified name for a single log message.
        /// </summary>
        /// <param name="message">The message being evaluated</param>
        /// <returns>The fully qualified name for the log message</returns>
        internal delegate string LogMessageGroupFullNameDelegate(LogMessage message);

        /// <summary>
        /// Delegate that can specify the hierarchy for a single log message.
        /// </summary>
        /// <param name="message">The message being evaluated</param>
        /// <returns>A string array with one element for every group in the hierarchy</returns>
        internal delegate string[] LogMessageGroupDelegate(LogMessage message);

        /// <summary>
        /// Create a new log message tree.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="caption"></param>
        /// <param name="description"></param>
        /// <param name="fullNameDelegate"></param>
        /// <param name="messageGroupDelegate"></param>
        internal LogMessageTree(string name, string caption, string description,LogMessageGroupFullNameDelegate fullNameDelegate, LogMessageGroupDelegate messageGroupDelegate)
        {
            if (fullNameDelegate == null)
                throw new ArgumentNullException(nameof(fullNameDelegate));

            if (messageGroupDelegate == null)
                throw new ArgumentNullException(nameof(messageGroupDelegate));

            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            if (string.IsNullOrEmpty(caption))
                throw new ArgumentNullException(nameof(caption));

            //screwy but necessary - we need to have it set to the highest numeric value to start.
            MaxSeverity = LogMessageSeverity.Verbose;

            Name = name;
            Caption = caption;
            Description = description;

            m_FullNameDelegate = fullNameDelegate;
            m_MessageGroupDelegate = messageGroupDelegate;

            Groups = new LogMessageGroupCollection();
        }

        /// <summary>
        /// Add the specified message to its group, calculated based on the data in the message as this call is made.
        /// </summary>
        /// <param name="message">The message being evaluated</param>
        internal void AddMessage(LogMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            string fullyQualifiedName = m_FullNameDelegate(message);

            if (string.IsNullOrEmpty(fullyQualifiedName))
                //nothing to count.
                return;

            // We aren't using a lock on the dictionary because a given instance of LMT should only be on a single thread.

            //do we already have this group?
            if (m_GroupFullNameCache.TryGetValue(fullyQualifiedName, out var leafGroup) == false)
            {
                //nope, we will need to add it.
                string[] groupNames = m_MessageGroupDelegate(message);

                //manually check the first value, it doesn't cleanly fit into the loop.
                string childGroupName = groupNames[0];

                if (Groups.TryGetValue(childGroupName, out var parentGroup) == false)
                {
                    //no dice - we need to add it from here.
                    parentGroup = Groups.Add(childGroupName, message);
                }

                for (int groupNameIndex = 1; groupNameIndex < groupNames.Length; groupNameIndex++)
                {
                    childGroupName = groupNames[groupNameIndex];
                    if ((parentGroup.Groups.Count == 0) || (parentGroup.Groups.TryGetValue(childGroupName, out var childGroup) == false))
                    {
                        //it doesn't exist - we need to add it
                        childGroup = parentGroup.Groups.Add(childGroupName, message);
                    }

                    parentGroup = childGroup;
                }

                //and whatever our parent group now is actually is the leaf group because we've run thorugh all children.
                leafGroup = parentGroup;
                m_GroupFullNameCache.Add(fullyQualifiedName, leafGroup); // Remember it for next time, right? It wasn't doing this.
            }

            //have this leaf group count the new message
            leafGroup.AddMessage(message);

            MaxSeverity = (LogMessageSeverity)Math.Min((int)MaxSeverity, (int)message.Severity);
            MessageCount++;
        }

        /// <summary>
        /// A key name for this log message tree.  Names are file and path name safe.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// An end user display caption for the log message tree.
        /// </summary>
        public string Caption { get; private set; }

        /// <summary>
        /// An end user description for the log message tree.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// The root log message groups.
        /// </summary>
        public LogMessageGroupCollection Groups { get; private set; }

        /// <summary>
        /// The worst-case severity of the log messages in this group (including its child groups)
        /// </summary>
        public LogMessageSeverity MaxSeverity { get; private set; }

        /// <summary>
        /// The number of messages in this group (including its child groups)
        /// </summary>
        public int MessageCount { get; private set; }
    }

}
