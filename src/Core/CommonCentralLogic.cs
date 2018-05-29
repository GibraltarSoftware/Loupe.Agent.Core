using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Gibraltar
{
    /// <summary>
    /// A static class to hold central logic for common file and OS operations needed by various projects.
    /// </summary>
    public static class CommonCentralLogic
    {
        private static bool g_MonoRuntime = CheckForMono(); // Are we running in Mono or full .NET CLR?
        private static bool s_SilentMode = false;
        volatile private static bool s_BreakPointEnable = false; // Can be changed in the debugger

        // Basic log implementation.
        volatile private static bool g_SessionEnding; // Session end triggered. False until set to true.
        volatile private static bool g_SessionEnded; // Session end completed. False until set to true.

        /// <summary>
        /// Indicates if the process is running under the Mono runtime or the full .NET CLR.
        /// </summary>
        public static bool IsMonoRuntime { get { return g_MonoRuntime; } }

        /// <summary>
        /// Indicates if the logging system should be running in silent mode (for example when running in the agent).
        /// </summary>
        public static bool SilentMode
        {
            get { return s_SilentMode; }
            set { s_SilentMode = value; }
        }

        /// <summary>
        /// A temporary flag to tell us whether to invoke a Debugger.Break() when Log.DebugBreak() is called.
        /// </summary>
        /// <remarks>True enables breakpointing, false disables.  This should probably be replaced with an enum
        /// to support multiple modes, assuming the basic usage works out.</remarks>
        public static bool BreakPointEnable
        {
            get { return s_BreakPointEnable; }
            set { s_BreakPointEnable = value; }
        }

        /// <summary>
        /// Reports whether EndSession() has been called to formally end the session.
        /// </summary>
        public static bool IsSessionEnding { get { return g_SessionEnding; } }

        /// <summary>
        /// Reports whether EndSession() has completed flushing the end-session command to the log.
        /// </summary>
        public static bool IsSessionEnded { get { return g_SessionEnded; } }

        /// <summary>
        /// Sets the SessionEnding flag to true.  (Can't be reversed once set.)
        /// </summary>
        public static void DeclareSessionIsEnding()
        {
            g_SessionEnding = true;
        }

        /// <summary>
        /// Sets the SessionHasEnded flag to true.  (Can't be reversed once set.)
        /// </summary>
        public static void DeclareSessionHasEnded()
        {
            g_SessionEnding = true; // This must also be set to true before it can be ended.
            g_SessionEnded = true;
        }

        /// <summary>
        /// Automatically stop debugger like a breakpoint, if enabled.
        /// </summary>
        /// <remarks>This will check the state of Log.BreakPointEnable and whether a debugger is attached,
        /// and will breakpoint only if both are true.  This should probably be extended to handle additional
        /// configuration options using an enum, assuming the basic usage works out.  This method is conditional
        /// upon a DEBUG build and will be safely ignored in release builds, so it is not necessary to wrap calls
        /// to this method in #if DEBUG (acts much like Debug class methods).</remarks>
        [Conditional("DEBUG")]
        public static void DebugBreak()
        {
            if (s_BreakPointEnable && Debugger.IsAttached)
            {
                Debugger.Break(); // Stop here only when debugging
                // ...then Shift-F11 to step out to where it is getting called...
            }
        }

        /// <summary>
        /// Check whether we are running in a Mono runtime environment rather than a normal .NET CLR.
        /// </summary>
        /// <returns>True if running in Mono.  False if .NET CLR.</returns>
        private static bool CheckForMono()
        {
            Type monoRuntime = Type.GetType("Mono.Runtime"); // Detect if we're running under Mono runtime.
            bool isMonoRuntime = (monoRuntime != null); // We'll cache the result so we don't have to waste time checking again.

            return isMonoRuntime;
        }

        /// <summary>
        /// Extracts needed message source information from the current call stack.
        /// </summary>
        /// <remarks>This is used internally to perform the actual stack frame walk.  Constructors for derived classes
        /// all call this method.  This constructor also allows the caller to specify a log message as being
        /// of local origin, so Gibraltar stack frames will not be automatically skipped over when determining
        /// the originator for internally-issued log messages.</remarks>
        /// <param name="skipFrames">The number of stack frames to skip over to find the first candidate to be
        /// identified as the source of the log message.  (Should generally use 0 if exception parameter is not null.)</param>
        /// <param name="trustSkipFrames">True if logging a message originating in Gibraltar code (or to just trust skipFrames).
        /// False if logging a message from the client application and Gibraltar frames should be explicitly skipped over.</param>
        /// <param name="exception">An exception declared as the source of this log message (or null for normal call stack source).</param>
        /// <param name="className">The class name of the identified source (usually available).</param>
        /// <param name="methodName">The method name of the identified source (usually available).</param>
        /// <param name="fileName">The file name of the identified source (if available).</param>
        /// <param name="lineNumber">The line number of the identified source (if available).</param>
        /// <returns>The index of the stack frame chosen</returns>
        public static int FindMessageSource(int skipFrames, bool trustSkipFrames, Exception exception, out string className,
                                             out string methodName, out string fileName, out int lineNumber)
        {
#if NETCOREAPP1_0 || NETCOREAPP1_1
            methodName = null;
            className = null;
            fileName = null;
            lineNumber = 0;
            return -1;
#else
            int selectedFrame = -1;

            try
            {
                // We use skipFrames+1 here so that callers can pass in 0 to designate themselves rather than have to know to start with 1.
                // But for an exception stack trace, we didn't get added to the stack, so don't add anything in that case.
                StackTrace stackTrace = (exception == null) ? new StackTrace(skipFrames + 1, true) : new StackTrace(exception, true);
                StackFrame frame = null;
                StackFrame firstSystem = null;
                StackFrame newFrame;
                MethodBase method = null;
                string frameModule;

                int frameIndex = 0; // we already accounted for skip frames in getting the stack trace
                while (true)
                {
                    // Careful:  We may be out of frames, in which case we're going to stop, hopefully without an exception.
                    try
                    {
                        newFrame = stackTrace.GetFrame(frameIndex);
                        frameIndex++; // Do this here so any continue statement below in this loop will be okay.
                        if (newFrame == null) // Not sure if this check is actually needed, but it doesn't hurt.
                            break; // We're presumably off the end of the stack, bail out of the loop!

                        method = newFrame.GetMethod();
                        if (method == null) // The method we found might be null (if the frame is invalid?).
                            break; // We're presumably off the end of the stack, bail out of the loop!

                        frameModule = method.Module.Name;

                        if (frameModule.Equals("System.dll") || frameModule.Equals("mscorlib.dll"))
                        {
                            // Ahhh, a frame in the system libs... Next non-system frame will be our pick!
                            if (firstSystem == null) // ...unless we find no better candidate, so remember the first one.
                            {
                                firstSystem = newFrame;
                            }

                        }
                        else
                        {
                            frame = newFrame; // New one is valid, and not system, so update our chosen frame to use it.
                            // We already got its corresponding method, above, to validate the module.

                            // Okay, it's not in the system libs, so it might be a good candidate,
                            // but do we need to filter out Gibraltar or is this a deliberate local invocation?
                            // And if it's something that called into system libs (e.g. Trace), take that regardless.

                            if (trustSkipFrames || (firstSystem != null))
                                break;

                            if (frameModule.Equals("Loupe.Agent.NETCore.dll") == false &&
                                frameModule.Equals("Loupe.Core.NETCore.dll") == false)
                            {
                                // This is the first frame which is not in our known ecosystem,
                                // so this must be the client code calling us.
                                break; // We found it!  Break out of the loop.
                            }
                        }
                    }
                    catch
                    {
                        // Hmmm, we got some sort of failure which we didn't know enough to prevent?
                        // We could comment on that... but we can't do logging here, it gets recursive!
                        // So use our safe breakpoint to alert a debugging user.  This is ignored in production.
                        DebugBreak(); // Stop the debugger here (if it's running, otherwise we won't alert on it).

                        // Well, whatever we found - that's where we are.  We have to give up our search.
                        break;
                    }

                    method = null; // Invalidate it for the next loop.

                    // Remember, frameIndex was already incremented near the top of the loop
                    if (frameIndex > 200) // Note: We're assuming stacks can never be this deep (without finding our target)
                    {
                        // Maybe we messed up our failure-detection, so to prevent an infinite loop from hanging the application...
                        DebugBreak(); // Stop the debugger here (if it's running).  This shouldn't ever be hit.

                        break; // Okay, it's just not sensible for stack to be so deep, so let's give up.
                    }
                }

                if (frame == null || method == null)
                {
                    frame = stackTrace.GetFrame(0); // If we went off the end, go back to the first frame (after skipFrames).
                    selectedFrame = 0;
                }
                else
                {
                    selectedFrame = frameIndex;
                }

                method = (frame == null) ? null : frame.GetMethod(); // Make sure these are in sync!
                if (method == null)
                {
                    frame = firstSystem; // Use that first system frame we found if no later candidate arose.
                    method = (frame == null) ? null : frame.GetMethod();
                }

                // Now that we've selected the best possible frame, we need to make sure we really found one.
                if (method != null)
                {
                    // Whew, we found a valid method to attribute this message to.  Get the details safely....
                    className = method.DeclaringType == null ? null : method.DeclaringType.FullName;
                    methodName = method.Name;

                    try
                    {
                        //now see if we have file information
                        fileName = frame.GetFileName();
                        if (string.IsNullOrEmpty(fileName) == false)
                        {
                            // m_FileName = Path.GetFileName(m_FileName); // Drops full path... but we want that info!
                            lineNumber = frame.GetFileLineNumber();
                        }
                        else
                            lineNumber = 0; // Not meaningful if there's no file name!
                    }
                    catch
                    {
                        fileName = null;
                        lineNumber = 0;
                    }
                }
                else
                {
                    // Ack! We got nothing!  Invalidate all of these which depend on it and are thus meaningless.
                    methodName = null;
                    className = null;
                    fileName = null;
                    lineNumber = 0;
                }
            }
            catch
            {
                // Bleagh!  We got an unexpected failure (not caught and handled by a lower catch block as being expected).
                DebugBreak(); // Stop the debugger here (if it's running, otherwise we won't alert on it).

                methodName = null;
                className = null;
                fileName = null;
                lineNumber = 0;
            }

            return selectedFrame;
#endif
        }


        /// <summary>
        /// Safely attempt to expand a format string with supplied arguments.
        /// </summary>
        /// <remarks>If the normal call to string.Format() fails, this method does its best to create a string
        /// (intended as a log message) error message containing the original format string and a representation
        /// of the args supplied, to attempt to preserve meaningful information despite the user's mistake.</remarks>
        /// <param name="formatProvider">An IFormatProvider (such as a CultureInfo) to use, where applicable.
        /// (may be null, indicating the current culture)</param>
        /// <param name="format">The desired format string, as used by string.Format().</param>
        /// <param name="args">An array of args, as used by string.Format() after the format string.</param>
        /// <returns>The formatted string, or an error string containing best-effort information.</returns>
        public static string SafeFormat(IFormatProvider formatProvider, string format, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                // No arguments were supplied, so the "format" string is returned without any expansion.
                // Providing null or empty is also legal in this case, and we'll treat them both as empty.
                return format ?? string.Empty; // Protect against a null, always return a valid string.
            }

            string resultString;
            Exception formattingException = null;

            // If format is null, we want to get the exception from string.Format(), but we don't want to pass in
            // an empty format string (which won't fail but will drop all of their arguments).
            // So this is not the usual IsNullOrEmpty() check, it's null-or-not-empty that we want here.
            if (format == null || format.Length > 0)
            {
                try
                {
                    // ReSharper disable AssignNullToNotNullAttribute
                    resultString = string.Format(formatProvider, format, args);
                    // ReSharper restore AssignNullToNotNullAttribute
                }
                catch (Exception ex)
                {
                    // Catch all exceptions.
                    formattingException = ex;
                    resultString = null; // Signal a failure, so we can exit the catch block for further error handling.
                }
            }
            else
            {
                // They supplied arguments with an empty or null format string, so they won't get any info!
                // We'll treat this as an error case, so they get the data from the args in our error handling.
                resultString = null;
            }

            if (resultString == null)
            {
                // There was some formatting error, so we want to format an error string with all the useful info we can.

                StringBuilder supportBuilder = new StringBuilder(format ?? string.Empty); // For support people.
                StringBuilder devBuilder = new StringBuilder(); // For developers.
                string formatString = ReverseEscapes(format);

                // Add a blank line after the format string for support.  We need a second line break if there wasn't one already.
                if (string.IsNullOrEmpty(format))
                {
                    // ToDo: Decide if we actually want the extra one in this case.  It seems unnecessary.
                    supportBuilder.Append("\r\n"); // There wasn't one already, so add the first linebreak...
                }
                else
                {
                    char lastChar = format[format.Length - 1];
                    if (lastChar != '\n' && lastChar != '\r')
                        supportBuilder.Append("\r\n"); // Make sure this case ends with some kind of a linebreak...
                }
                // The second line break will come at the start of the first Value entry.

                if (formattingException != null)
                {
                    devBuilder.AppendFormat("\r\n\r\n\r\nError expanding message format with {0} args supplied:\r\nException = ", args.Length);
                    devBuilder.Append(SafeToString(formatProvider, formattingException, false));

                    // Use formatString here rather than format because it has the quotes around it and handles the null case.
                    devBuilder.AppendFormat(formatProvider, "\r\nFormat string = {0}", formatString);
                }

                // Now loop over the args provided.  We need to add each entry to supportBuilder and devBuilder.

                for (int i = 0; i < args.Length; i++)
                {
                    object argI = args[i];

                    supportBuilder.AppendFormat(formatProvider, "\r\nValue #{0}: {1}", i,
                                                SafeToString(formatProvider, argI, false));
                    // Only doing devBuilder if we have an actual formatting Exception.  Empty format case doesn't bother.
                    if (formattingException != null)
                    {
                        if (argI == null)
                        {
                            // We can't call GetType() from a null, can we?  I think any original cast type for the null is lost
                            // by this point, so we can't report a type for it (other than "object"), so just report it as a null.
                            devBuilder.AppendFormat(formatProvider, "\r\nargs[{0}] {1}", i,
                                                    SafeToString(formatProvider, argI, true));
                        }
                        else
                        {
                            string typeName = argI.GetType().FullName;
                            devBuilder.AppendFormat(formatProvider, "\r\nargs[{0}] ({1}) = {2}", i, typeName,
                                                    SafeToString(formatProvider, argI, true));
                        }
                    }
                }

                supportBuilder.Append(devBuilder); // Append the devBuilder section
                resultString = supportBuilder.ToString();
            }

            return resultString;
        }

        private static readonly char[] ResolvedEscapes = new[] { '\r', '\n', '\t', '\"', '\\', };
        private static readonly string[] LiteralEscapes = new[] { "\\r", "\\n", "\\t", "\\\"", "\\\\", };
        private static readonly Dictionary<char, string> EscapeMap = InitEscapeMap();

        /// <summary>
        /// Initializes the EscapeMap dictionary.
        /// </summary>
        private static Dictionary<char, string> InitEscapeMap()
        {
            // Allocate and initialize our mapping of special resolved-escape characters to corresponding string literals.
            int size = ResolvedEscapes.Length;
            Dictionary<char, string> escapeMap = new Dictionary<char, string>(size);

            for (int i = 0; i < size; i++)
            {
                escapeMap[ResolvedEscapes[i]] = LiteralEscapes[i];
            }
            return escapeMap;
        }

        /// <summary>
        /// Expand (some) special characters back to how they appear in string literals in source code.
        /// </summary>
        /// <remarks>This currently does nothing but return the original string.</remarks>
        /// <param name="format">The string (e.g. a format string) to convert back to its literal appearance.</param>
        /// <returns>A string with embedded backslash escape codes to be displayed as in source code.</returns>
        private static string ReverseEscapes(string format)
        {
            if (format == null)
                return "(null)";

            StringBuilder builder = new StringBuilder("\"");
            int currentIndex = 0;

            while (currentIndex < format.Length)
            {
                string escapeString = null;
                int nextEscapeIndex = format.IndexOfAny(ResolvedEscapes, currentIndex);

                if (nextEscapeIndex < 0)
                {
                    // There aren't any more.  We just need to copy the rest of the string.
                    nextEscapeIndex = format.Length; // Pretend it's just past the end, so the math below works.
                    // Leave escapeString as null, so we won't append anything for it below.
                }
                else
                {
                    // We found one of our ResolvedEscapes.  Which one?
                    char escapeChar = format[nextEscapeIndex];
                    if (EscapeMap.TryGetValue(escapeChar, out escapeString) == false)
                    {
                        // It wasn't found in the map!  Someone screwed up our mapping configuration, so we have to punt.
                        escapeString = new string(escapeChar, 1); // Copy the original char (1 time).
                    }
                }

                int length = nextEscapeIndex - currentIndex; // How long is the substring up to the next escape char?

                if (length >= 0)
                    builder.Append(format, currentIndex, length); // Copy the string up to this point.

                if (string.IsNullOrEmpty(escapeString) == false)
                    builder.Append(escapeString); // Replace the char with the corresponding string.

                currentIndex = nextEscapeIndex + 1;
            }

            builder.Append("\"");
            return builder.ToString();
        }

        /// <summary>
        /// Try to expand an object to a string, handling exceptions which might occur.
        /// </summary>
        /// <param name="formatProvider">An IFormatProvider (such as a CultureInfo).  (may be null to indicate the
        /// current culture)</param>
        /// <param name="forDisplay">The object for display into a string.</param>
        /// <param name="reverseEscapes">Whether to convert null and strings back to appearance as in code.</param>
        /// <returns>The best effort at representing the given object as a string.</returns>
        private static string SafeToString(IFormatProvider formatProvider, object forDisplay, bool reverseEscapes)
        {
            StringBuilder builder = new StringBuilder();

            Exception displayException = forDisplay as Exception;
            Exception expansionException = null;
            try
            {
                if (reverseEscapes && (forDisplay == null || forDisplay.GetType() == typeof(string)))
                {
                    builder.Append(ReverseEscapes((string)forDisplay)); // Special handling of strings and nulls requested.
                }
                else if (displayException == null)
                {
                    // forDisplay was not an exception, so do a generic format.
                    builder.AppendFormat(formatProvider, "{0}", forDisplay); // Try to format the object by formatProvider.
                }
                else
                {
                    // forDisplay was an exception type, use a helpful two-line format.
                    builder.AppendFormat(formatProvider, "{0}\r\nException Message ",
                                         displayException.GetType().FullName);
                    // This is separate so that the text is set up in case Message throws an exception here.
                    builder.AppendFormat(formatProvider, "= {0}", displayException.Message);
                }
            }
            catch (Exception ex)
            {
                // Catch all exceptions.
                expansionException = ex;
            }

            if (expansionException != null)
            {
                try
                {
                    builder.AppendFormat(formatProvider, "<<<{0} error converting to string>>> : ",
                                         expansionException.GetType().FullName);
                    builder.Append(expansionException.Message);
                }
                catch
                {
                    // An exception accessing the exception?  Wow.  That should not be possible.  Well, just punt.
                    builder.Append("<<<Error accessing exception message>>>");
                }
            }

            return builder.ToString();
        }
    }
}
