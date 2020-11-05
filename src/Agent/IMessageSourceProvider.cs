#nullable enable
using System;
using System.Security.Principal;

namespace Gibraltar.Agent
{
    /// <summary>
    /// An interface used to provided the details of the origin (class, method, and
    /// source code file) of a log message when passing messages from an external log system to
    /// the Loupe Agent.
    /// </summary>
    /// <remarks>
    /// 	<para>Any field that isn't available can safely return null.</para>
    /// 	<para>
    ///         This interface is intended for use with the <see cref="Log.Write(LogMessageSeverity, string, IMessageSourceProvider, IPrincipal, Exception, LogWriteMode, string, string, string, string, object[])">
    ///         Log.Write</see> method when forwarding data from another logging
    ///         system that has already determined the correct origin of a logging statement.
    ///         If this information is not available then you can either use the alternate
    ///         <see cref="Log.Write(LogMessageSeverity, string, int, Exception, LogWriteMode, string, string, string, string, object[])">
    ///         Log.Write</see> method that will automatically determine
    ///         the message source or pass Null to indicate that no location information is
    ///         available.
    ///     </para>
    /// 	<para>The Loupe Agent will read all of the properties of this interface during
    ///     the call to Log.Write and will not retain any reference after the call returns.
    ///     Because of this, a range of implementations can be done ranging from creating an
    ///     object that implements this interface being created for every Log call to having
    ///     one object that is passed for every call.</para>
    /// </remarks>
    /// <remarks>
    /// 	<para>Any field that isn't available can safely return null.</para>
    /// 	<para>This interface is intended for use with the Log.Write method when forwarding
    ///     data from another logging system that has already determined the correct origin of
    ///     a logging statement. If this information is not available then you can either use
    ///     the alternate Log.Write method that will automatically determine the message source
    ///     or pass Null to indicate that no location information is available.</para>
    /// 	<para>The Loupe Agent will read all of the properties of this interface during
    ///     the call to Log.Write and will not retain any reference after the call returns.
    ///     Because of this, a range of implementations can be done ranging from creating an
    ///     object that implements this interface being created for every Log call to having
    ///     one object that is passed for every call.</para>
    /// </remarks>
    /// <seealso cref="Log">Log Class</seealso>
    /// <seealso cref="Log.Write(LogMessageSeverity, string, IMessageSourceProvider, IPrincipal, Exception, LogWriteMode, string, string, string, string, object[])">Write Method (Gibraltar.Agent.Log)</seealso>
    public interface IMessageSourceProvider
    {
        // Note: We don't support passing the originating threadId and rely on receiving log messages still on the same thread.

        /// <summary>
        /// Should return the simple name of the method which issued the log message.
        /// </summary>
        string? MethodName { get; }

        /// <summary>
        /// Should return the full name of the class (with namespace) whose method issued the log message.
        /// </summary>
        string? ClassName { get; }

        /// <summary>
        /// Should return the name of the file containing the method which issued the log message.
        /// </summary>
        string? FileName { get; }

        /// <summary>
        /// Should return the line within the file at which the log message was issued.
        /// </summary>
        int LineNumber { get; }

        // ToDo: Assembly and method Signature info?

    }
}