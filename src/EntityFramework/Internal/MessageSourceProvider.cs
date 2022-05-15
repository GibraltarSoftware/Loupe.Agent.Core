using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Gibraltar.Agent;

namespace Loupe.Agent.EntityFramework.Internal
{
    /// <summary>
    /// A basic class to determine the source of a log message and act as an IMessageSourceProvider for Gibraltar. 
    /// </summary>
    /// <remarks>This class knows how to acquire information about the source of a log message from the current call stack,
    /// and acts as a IMessageSourceProvider to use when handing off a log message to the central Log.
    /// Thus, this object must be created while still within the same call stack as the origination of the log message.
    /// </remarks>
    [DebuggerNonUserCode]
    internal class MessageSourceProvider : IMessageSourceProvider
    {
        private string _methodName;
        private string _className;
        private string _fileName;
        private int _lineNumber;
        private string _formattedStackTrace;

        /// <summary>
        /// Creates a MessageSourceProvider object to be used as an IMessageSourceProvider.
        /// </summary>
        /// <param name="className">The full name of the class (with namespace) whose method issued the log message.</param>
        /// <param name="methodName">The simple name of the method which issued the log message.</param>
        /// <remarks>This constructor is used only for the convenience of the Log class when it needs to generate
        /// an IMessageSoruceProvider for construction of internally-generated packets without going through the
        /// usual direct PublishToLog() mechanism.</remarks>
        public MessageSourceProvider(string className, string methodName)
        {
            _methodName = methodName;
            _className = className;
            _fileName = null;
            _lineNumber = 0;
        }

        /// <summary>
        /// Creates a MessageSourceProvider object to be used as an IMessageSourceProvider.
        /// </summary>
        /// <param name="className">The full name of the class (with namespace) whose method issued the log message.</param>
        /// <param name="methodName">The simple name of the method which issued the log message.</param>
        /// <param name="fileName">The name of the file containing the method which issued the log message.</param>
        /// <param name="lineNumber">The line within the file at which the log message was issued.</param>
        /// <remarks>This constructor is used only for the convenience of the Log class when it needs to generate
        /// an IMessageSoruceProvider for construction of internally-generated packets without going through the
        /// usual direct PublishToLog() mechanism.</remarks>
        public MessageSourceProvider(string className, string methodName, string fileName, int lineNumber)
        {
            _methodName = methodName;
            _className = className;
            _fileName = fileName;
            _lineNumber = lineNumber;
        }

        /// <summary>
        /// Creates a MessageSourceProvider object to be used as an IMessageSourceProvider.
        /// </summary>
        /// <remarks>Locates the original caller who submitted this log entry.  This only works if invoked on the
        /// same call stack (not across a network socket, for example).</remarks>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public MessageSourceProvider(int skipFrames)
        {
            FindMessageSource(skipFrames + 1);
        }

        /// <summary>
        /// The full adjusted stack trace from the bottom of the stack up through the frame we're attributing this message to
        /// </summary>
        public string StackTrace
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get
            {
                lock(this)
                {
                    if (string.IsNullOrEmpty(_formattedStackTrace))
                    {
                        try
                        {
                            //If we didn't get it, just go with using our caller.
                            _formattedStackTrace = new StackTrace(1, true).ToString();
                        }
                        catch
                        {
                            _formattedStackTrace = null;
                        }
                    }
                }
                return _formattedStackTrace;
            }
        }

        /// <summary>
        /// Extracts needed message source information from the current call stack.
        /// </summary>
        /// <remarks>We can't rely on passing a simple skipFrames count to Gibraltar because the design of PostSharp
        /// can reach our GSharp aspects by a variable number of intermediate calls.  So we have to examine the stack
        /// frames ourselves and skip over any that are part of PostSharp in order to find the actual source of the
        /// original call.  If the namespace of the logging framework or this adapter is changed, the corresponding
        /// comparison needs to be changed to match.</remarks>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void FindMessageSource(int skipFrames)
        {
            try
            {
                var stackTrace = new StackTrace(skipFrames + 1, true);
                StackFrame frame = null;
                MethodBase method = null;
                var selectedFrameIndex = 0;

                var frameIndex = 0; // we already accounted for skipFrames in getting the stackTrace
                while (true)
                {
                    //careful:  We may be out of frames, in which case we're going to stop, hopefully without an exception.
                    try
                    {
                        frame = stackTrace.GetFrame(frameIndex);
                        selectedFrameIndex = frameIndex;
                        frameIndex++; // Do this here so any continue statement added below in this loop will be okay.

                        method = frame?.GetMethod();
                        if (method == null) // But the method we found might be null (if the frame is invalid?)
                        {
                            break; // We're presumably off the end of the stack, bail out of the loop!
                        }
                        var frameNamespace = (method.DeclaringType == null) ? null : method.DeclaringType.FullName;

                        if (frameNamespace != null &&
                            frameNamespace.StartsWith("System.") == false &&
                            frameNamespace.StartsWith("Gibraltar.") == false)
                        {
                            // This is the first frame outside of this adapter and the logging framework itself.
                            // So this must be the actual caller!  We can stop looking.
                            break;
                        }
                    }
                    catch
                    {
                        // Hmmm, we got some sort of failure which we didn't know enough to prevent?
                        // We could comment on that... but we can't do logging here, it gets recursive!
                        // So use our safe breakpoint to alert a debugging user.  This is ignored in production.
                        DebugBreak(); // Stop the debugger here (if it's running, otherwise we won't alert on it).

                        // Well, whatever we found - that's where we are.  We have to give up our search.
                        selectedFrameIndex = 0;
                        break;
                    }

                    method = null; // Invalidate it for the next loop.

                    // Remember, frameIndex was already incremented near the top of the loop
                    if (frameIndex > 200) // Note: We're assuming stacks can never be this deep (without finding our target)
                    {
                        // Maybe we messed up our failure-detection, so to prevent an infinite loop from hanging the application...
                        DebugBreak(); // Stop the debugger here (if it's running).  This shouldn't ever be hit.

                        selectedFrameIndex = 0;
                        break; // Okay, it's just not sensible for stack to be so deep, so let's give up.
                    }
                }

                if (frame == null || method == null)
                {
                    frame = stackTrace.GetFrame(0); // If we went off the end, go back to the first frame (after skipFrames).
                    selectedFrameIndex = 0;
                }

                //now store off the whole formatted remainder of the stack, including our selected frame.
                _formattedStackTrace = new StackTrace(selectedFrameIndex + skipFrames + 1, true).ToString();
                method = frame?.GetMethod(); // Make sure these are in sync!

                // Now that we've selected the best possible frame, we need to make sure we really found one.
                if (method == null)
                {
                    // Ack! We got nothing!  Invalidate all of these which depend on it and are thus meaningless.
                    _methodName = null;
                    _className = null;
                    _fileName = null;
                    _lineNumber = 0;
                }
                else
                {
                    // Whew, we found a valid method to attribute this message to.  Get the details safely....
                    try
                    {
                        // MethodBase method = frame.GetMethod();
                        _className = (method.DeclaringType == null) ? null : method.DeclaringType.FullName;
                        _methodName = method.Name;
                    }
                    catch
                    {
                        _methodName = null;
                        _className = null;
                    }

                    try
                    {
                        //now see if we have file information
                        _fileName = frame.GetFileName();
                        if (string.IsNullOrEmpty(_fileName) == false)
                        {
                            _lineNumber = frame.GetFileLineNumber();
                        }
                        else
                        {
                            _lineNumber = 0; // Not meaningful if there's no file name!
                        }
                    }
                    catch
                    {
                        _fileName = null;
                        _lineNumber = 0;
                    }
                }
            }
            catch
            {
                // Bleagh!  We got an unexpected failure (not caught and handled by a lower catch block as being expected).
                DebugBreak(); // Stop the debugger here (if it's running, otherwise we won't alert on it).

                _methodName = null;
                _className = null;
                _fileName = null;
                _lineNumber = 0;
            }
        }
        /// <summary>
        /// Automatically stop debugger like a breakpoint, if enabled.
        /// </summary>
        /// <remarks>This will check whether a debugger is attached, and will breakpoint if one is.  This method is
        /// conditional upon a DEBUG build and will be safely ignored in release builds, so it is not necessary to wrap
        /// calls to this method in #if DEBUG (acts much like Debug class methods).</remarks>
        [Conditional("DEBUG")]
        private static void DebugBreak()
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break(); // Stop here only when debugging
                // ...then Shift-F11 to step out to where it is getting called...
            }
        }

        #region IMessageSourceProvider properties

        /// <summary>
        /// The simple name of the method which issued the log message.
        /// </summary>
        public string MethodName { get { return _methodName; } }

        /// <summary>
        /// The full name of the class (with namespace) whose method issued the log message.
        /// </summary>
        public string ClassName { get { return _className; } }

        /// <summary>
        /// The name of the file containing the method which issued the log message.
        /// </summary>
        public string FileName { get { return _fileName; } }

        /// <summary>
        /// The line within the file at which the log message was issued.
        /// </summary>
        public int LineNumber { get { return _lineNumber; } }

        #endregion
    }
}
