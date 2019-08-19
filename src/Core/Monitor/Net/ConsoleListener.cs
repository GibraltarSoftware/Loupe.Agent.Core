using System;
using System.Globalization;
using System.IO;
using System.Text;
using Loupe.Extensibility.Data;
using Loupe.Logging;


namespace Gibraltar.Monitor.Net
{
    /// <summary>
    /// Listens for standard and error console output and redirects to the session file.
    /// </summary>
    public class ConsoleListener : TextWriter
    {
        private const string OutCategoryName = "Console.Out";
        private const string ErrorCategoryName = "Console.Error";
        private const string ConsoleLogSystem = "Console";

        private readonly string m_OutputCategory;
        private readonly TextWriter m_OriginalWriter;
        private readonly Encoding m_Encoding;
        private readonly StringBuilder m_Buffer;

        /// <summary>
        /// Create a new instance of the console listener.
        /// </summary>
        /// <param name="outputCategory"></param>
        /// <param name="originalWriter"></param>
        public ConsoleListener(string outputCategory, TextWriter originalWriter)
            : base(CultureInfo.InvariantCulture) // For now, tell inherited TextWriter to use InvariantCulture
        {
            m_OutputCategory = outputCategory;
            m_OriginalWriter = originalWriter;
            m_Encoding = originalWriter.Encoding;
            m_Buffer = new StringBuilder();
        }


        /// <summary>
        /// When overridden in a derived class, returns the <see cref="T:System.Text.Encoding"/> in which the output is written.
        /// </summary>
        /// <returns>
        /// The Encoding in which the output is written.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public override Encoding Encoding { get { return m_Encoding; } }


        /// <summary>
        /// Writes a character to the text stream.
        /// </summary>
        /// <param name="value">The character to write to the text stream. 
        ///                 </param><exception cref="T:System.ObjectDisposedException">The <see cref="T:System.IO.TextWriter"/> is closed. 
        ///                 </exception><exception cref="T:System.IO.IOException">An I/O error occurs. 
        ///                 </exception><filterpriority>1</filterpriority>
        /// <remarks>
        /// TODO: At some point we want to overhaul this to override at the Write(string) and WriteLine() level instead
        /// of Write(char) and pass the data in a different, stream-optimized format rather than as log messages.
        /// But for now... Wrap as a log message for each line (as we get a newline)....
        /// </remarks>
        public override void Write(char value)
        {
            m_OriginalWriter.Write(value);
            if (value == '\n')
            {
                if (m_Buffer.Length > 0)
                {
#if STACK_DUMP
                        Exception dumpException = new GibraltarStackInfoException(m_OutputCategory + " - Write()", null);
#else
                    const Exception dumpException = null;
#endif
                    SimpleLogMessage logMessage = new SimpleLogMessage(LogMessageSeverity.Verbose, LogWriteMode.Queued,
                                                                       ConsoleLogSystem, m_OutputCategory, 3,
                                                                       dumpException, m_Buffer.ToString());
                    m_Buffer.Length = 0;
                    logMessage.PublishToLog();
                }
            }
            else if (value != '\r')
                m_Buffer.Append(value);
            /* else
                m_buffer.Append("<CR>"); */
        }

        /// <summary>
        /// Clears all buffers for the current writer and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <filterpriority>1</filterpriority>
        public override void Flush()
        {
            base.Flush(); // Flush our base to push any pending writes to us?
            m_OriginalWriter.Flush(); // Then flush the original writer for whatever we passed on to it.

            //m_buffer.Append("<Flush>");

            // Now should we go ahead and push any partial lines in a log message?
            if (m_Buffer.Length > 0)
            {
#if STACK_DUMP
                    Exception dumpException = new GibraltarStackInfoException(m_OutputCategory + " - Flush()", null);
#else
                const Exception dumpException = null;
#endif
                SimpleLogMessage logMessage = new SimpleLogMessage(LogMessageSeverity.Verbose, LogWriteMode.Queued,
                                                                   ConsoleLogSystem, m_OutputCategory, 1,
                                                                   dumpException, m_Buffer.ToString());
                m_Buffer.Length = 0;
                logMessage.PublishToLog();
            }
        }

        /// <summary>
        /// Registers new ConsoleIntercepter on Console.Out and Console.Error.
        /// </summary>
        /// <remarks>This attempts to get the Console's InternalSyncObject to protect the operations as atomic,
        /// but will make a best-effort to do them even if the lock object could not be obtained.</remarks>
        public static void RegisterConsoleIntercepter()
        {
            Console.SetOut(new ConsoleListener(OutCategoryName, Console.Out));
            Console.SetError(new ConsoleListener(ErrorCategoryName, Console.Error));
        }
    }
}
