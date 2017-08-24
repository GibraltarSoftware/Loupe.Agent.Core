using System;

namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A unique warning, error, or critical event in the log recorded by one or more log messages
    /// </summary>
    public interface ILogEvent
    {
        /// <summary>
        /// The application event for all similar log events
        /// </summary>
        IApplicationEvent ApplicationEvent { get; }

        /// <summary>
        /// The severity of this event.
        /// </summary>
        LogMessageSeverity Severity { get; }

        /// <summary>
        /// The full name of the category where the messages were generated
        /// </summary>
        string CategoryName { get; }

        /// <summary>
        /// The full name of the class where the messages were generated
        /// </summary>
        string ClassName { get; }

        /// <summary>
        /// The calculated caption for this event, which may be shorter or different than the actual log messages
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// The total number of messages in this session that matched this event
        /// </summary>
        int Occurrences { get; }

        /// <summary>
        /// Partial details about the first log message that matches this event
        /// </summary>
        /// <remarks>
        /// 	<para>Not all log message properties are available based on the limits of the LogEvent data structure. The following properties are not supported:</para>
        /// 	<list type="table">
        /// 		<listheader>
        /// 			<term>Property</term>
        /// 			<description>Description</description>
        /// 		</listheader>
        /// 		<item>
        /// 			<term>DomainId</term>
        /// 			<description>The unique identifier of the application domain.</description>
        /// 		</item>
        /// 		<item>
        /// 			<term>DomainName</term>
        /// 			<description>The name of the application domain.</description>
        /// 		</item>
        /// 		<item>
        /// 			<term>Id</term>
        /// 			<description>The Guid of the log message</description>
        /// 		</item>
        /// 		<item>
        /// 			<term>IsBackground</term>
        /// 			<description>Indicates if the thread is a background (non-UI) thread.</description>
        /// 		</item>
        /// 		<item>
        /// 			<term>IsThreadPoolThread</term>
        /// 			<description>Indicates if the thread is a thread pool thread.</description>
        /// 		</item>
        /// 		<item>
        /// 			<term>LogSystem</term>
        /// 			<description>The name of the log system originating the message.</description>
        /// 		</item>
        /// 		<item>
        /// 			<term>Sequence</term>
        /// 			<description>The sequence number of the log message within the session</description>
        /// 		</item>
        /// 		<item>
        /// 			<term>ThreadId</term>
        /// 			<description>The thread Id (a number that is unique at a moment in time within an application domain)</description>
        /// 		</item>
        /// 		<item>
        /// 			<term>ThreadName</term>
        /// 			<description>The name assigned to the thread, if any. </description>
        /// 		</item>
        /// 		<item>
        /// 			<term>UserName</term>
        /// 			<description>The identity associated with the log message.</description>
        /// 		</item>
        /// 	</list>
        /// 	<para>If any of the above properties are referenced a NotSupportedException will be thrown.</para>
        /// 	<para></para>
        /// </remarks>
        /// <seealso cref="ILogMessage"></seealso>
        ILogMessage FirstOccurrence { get; }

        /// <summary>
        /// The timestamp of the last log message in the session matching this event.
        /// </summary>
        /// <remarks><para>When part of a session is being analyzed this will reflect the entire session,
        /// not just the new data.</para></remarks> 
        DateTimeOffset LastOccurrenceTimestamp { get; }

        /// <summary>
        /// The link to this item on the server
        /// </summary>
        Uri Uri { get; }
    }
}
