using System;
using System.Diagnostics;
using System.IO;
using Loupe.Agent;
using Loupe.Extensibility.Data;
using Loupe.Logging;
using NUnit.Framework;

namespace Loupe.Agent.Test.LogMessages
{
    [TestFixture]
    public class PerformanceTests
    {
        private const int DefaultMessagesPerTest = 10000;
        private TimeSpan m_TextDumpBaseline;
        private TimeSpan m_TextDumpByLineBaseline;

        public PerformanceTests()
        {
            MessagesPerTest = DefaultMessagesPerTest;
        }

        private int m_MessagesPerTest;
        public int MessagesPerTest { get { return m_MessagesPerTest; } set { m_MessagesPerTest = value; } }

        [OneTimeSetUp]
        public void Setup()
        {
            //calculate a baseline performance
            string tempFileNamePath = Path.GetTempFileName();

        //Time a single file dump (file held open) scenario
            File.Delete(tempFileNamePath);
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            DateTimeOffset messageEndTime, endTime;
            using (TextWriter logWriter = File.CreateText(tempFileNamePath))
            {
                for (int curMessage = 0; curMessage < MessagesPerTest; curMessage++)
                {
                    string caption = string.Format("Test Message {0} Caption", curMessage);
                    string description = string.Format("Test Message {0} Description, with some content added to it's at least the size you'd expect a normal description to be of a message", curMessage);
                    logWriter.WriteLine(caption + ": " + description);
                }
                messageEndTime = DateTimeOffset.UtcNow;

                //flush the buffer...
                logWriter.Flush();

                //and store off our time
                endTime = DateTimeOffset.UtcNow;
            }

            m_TextDumpBaseline = endTime - startTime;
            Trace.TraceInformation("Text Dump Baseline completed in {0}ms.  {1} messages were written at an average duration of {2}ms per message.  The flush took {3}ms.",
                                   m_TextDumpBaseline.TotalMilliseconds, MessagesPerTest, (m_TextDumpBaseline.TotalMilliseconds) / MessagesPerTest, (endTime - messageEndTime).TotalMilliseconds);

        //Time a file write per line scenario
            File.Delete(tempFileNamePath);
            DumbFileLogger fileLogger = new DumbFileLogger(tempFileNamePath);
            startTime = DateTimeOffset.UtcNow;
            for (int curMessage = 0; curMessage < MessagesPerTest; curMessage++)
            {
                string caption = string.Format("Test Message {0} Caption", curMessage);
                string description = string.Format("Test Message {0} Description, with some content added to it's at least the size you'd expect a normal description to be of a message", curMessage);
                fileLogger.WriteToLog(caption + ": " + description);
            }
            messageEndTime = DateTimeOffset.UtcNow;

            //and store off our time
            endTime = DateTimeOffset.UtcNow;

            m_TextDumpByLineBaseline = endTime - startTime;
            Trace.TraceInformation("Text Dump By Line Baseline completed in {0}ms.  {1} messages were written at an average duration of {2}ms per message.",
                                   m_TextDumpByLineBaseline.TotalMilliseconds, MessagesPerTest, (m_TextDumpByLineBaseline.TotalMilliseconds) / MessagesPerTest);


            //finally, don't leave that temp file around...
            File.Delete(tempFileNamePath);
        }

        [Test]
        public void AsyncPassThrough()
        {
            //first, lets get everything to flush so we have our best initial state.
            Log.EndFile("Preparing for Performance Test");
            Log.Verbose(LogWriteMode.WaitForCommit, "Test.Agent.LogMessages.Performance", "Preparing for performance test", null);

            DummyMessageSourceProvider ourMessageSource = new DummyMessageSourceProvider("Gibraltar.Agent.Test.LogMessages.PerformanceTests", "DefaultConfiguration", null, 0);

            //now that we know it's flushed everything, lets do our timed loop.
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            for (int curMessage = 0; curMessage < MessagesPerTest; curMessage++)
            {
                string caption = string.Format("Test Message {0} Caption", curMessage);
                string description = string.Format("Test Message {0} Description, with some content added to it's at least the size you'd expect a normal description to be of a message", curMessage);
                Log.Write(LogMessageSeverity.Verbose, "NUnit", ourMessageSource, null, null, LogWriteMode.Queued, null,
                    "Test.Agent.LogMessages.Performance", caption, description);
            }
            DateTimeOffset messageEndTime = DateTimeOffset.UtcNow;

            //one wait for commit message to force the buffer to flush.
            Log.Write(LogMessageSeverity.Verbose, "NUnit", ourMessageSource, null, null, LogWriteMode.WaitForCommit, null,
                "Test.Agent.LogMessages.Performance", "Committing performance test", null);

            //and store off our time
            DateTimeOffset endTime = DateTimeOffset.UtcNow;

            TimeSpan duration = endTime - startTime;

            Trace.TraceInformation("Async WriteMessage Test Completed in {0}ms ({4:P} of baseline).  {1} messages were written at an average duration of {2}ms per message.  The flush took {3}ms.",
                                   duration.TotalMilliseconds, MessagesPerTest, (duration.TotalMilliseconds) / MessagesPerTest, (endTime - messageEndTime).TotalMilliseconds,
                                   duration.TotalMilliseconds / m_TextDumpByLineBaseline.TotalMilliseconds);
        }

        [Test]
        public void SynchronousPassThrough()
        {
            //first, lets get everything to flush so we have our best initial state.
            Log.EndFile("Preparing for Performance Test");
            Log.Verbose(LogWriteMode.WaitForCommit, "Test.Agent.LogMessages.Performance", "Preparing for performance test", null);

            DummyMessageSourceProvider ourMessageSource = new DummyMessageSourceProvider("Gibraltar.Agent.Test.LogMessages.PerformanceTests", "DefaultConfiguration", null, 0);

            //now that we know it's flushed everything, lets do our timed loop.
            DateTimeOffset startTime = DateTimeOffset.UtcNow;

            for (int curMessage = 0; curMessage < MessagesPerTest; curMessage++)
            {
                string caption = string.Format("Test Message {0} Caption", curMessage);
                string description = string.Format("Test Message {0} Description, with some content added to it's at least the size you'd expect a normal description to be of a message", curMessage);
                Log.Write(LogMessageSeverity.Verbose, "NUnit", ourMessageSource, null, null, LogWriteMode.WaitForCommit, null,
                    "Test.Agent.LogMessages.Performance", caption, description);
            }

            //one more message to match our async case.
            Log.Write(LogMessageSeverity.Verbose, "NUnit", ourMessageSource, null, null, LogWriteMode.WaitForCommit, null,
                "Test.Agent.LogMessages.Performance", "Committing performance test", null);

            //and store off our time
            TimeSpan duration = DateTimeOffset.UtcNow - startTime;

            Trace.TraceInformation("Sync WriteMessage Test Completed in {0}ms ({3:P} of baseline).  {1} messages were written at an average duration of {2}ms per message.",
                                   duration.TotalMilliseconds, MessagesPerTest, (duration.TotalMilliseconds) / MessagesPerTest, duration.TotalMilliseconds / m_TextDumpByLineBaseline.TotalMilliseconds);
        }

        [Test]
        public void AsyncMessage()
        {
            //first, lets get everything to flush so we have our best initial state.
            Log.EndFile("Preparing for Performance Test");
            Log.Verbose(LogWriteMode.WaitForCommit, "Test.Agent.LogMessages.Performance", "Preparing for performance test", null);

            //now that we know it's flushed everything, lets do our timed loop.
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            for (int curMessage = 0; curMessage < MessagesPerTest; curMessage++)
            {
                string caption = string.Format("Test Message {0} Caption", curMessage);
                string description = string.Format("Test Message {0} Description, with some content added to it's at least the size you'd expect a normal description to be of a message", curMessage);
                Log.Verbose(LogWriteMode.Queued, "Test.Agent.LogMessages.Performance", caption, description);
            }
            DateTimeOffset messageEndTime = DateTimeOffset.UtcNow;

            //one wait for commit message to force the buffer to flush.
            Log.Verbose(LogWriteMode.WaitForCommit, "Test.Agent.LogMessages.Performance", "Committing performance test", null);

            //and store off our time
            DateTimeOffset endTime = DateTimeOffset.UtcNow;

            TimeSpan duration = endTime - startTime;

            Trace.TraceInformation("Async Write Test Completed in {0}ms ({4:P} of baseline).  {1} messages were written at an average duration of {2}ms per message.  The flush took {3}ms.",
                                   duration.TotalMilliseconds, MessagesPerTest, (duration.TotalMilliseconds) / MessagesPerTest, (endTime - messageEndTime).TotalMilliseconds,
                                   duration.TotalMilliseconds / m_TextDumpByLineBaseline.TotalMilliseconds);
        }

        [Test]
        public void SyncMessage()
        {
            //first, lets get everything to flush so we have our best initial state.
            Log.EndFile("Preparing for Performance Test");
            Log.Verbose(LogWriteMode.WaitForCommit, "Test.Agent.LogMessages.Performance", "Preparing for performance test", null);

            //now that we know it's flushed everything, lets do our timed loop.
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            for (int curMessage = 0; curMessage < MessagesPerTest; curMessage++)
            {
                string caption = string.Format("Test Message {0} Caption", curMessage);
                string description = string.Format("Test Message {0} Description, with some content added to it's at least the size you'd expect a normal description to be of a message", curMessage);
                Log.Verbose(LogWriteMode.WaitForCommit, "Test.Agent.LogMessages.Performance", caption, description);
            }

            Log.Verbose(LogWriteMode.WaitForCommit, "Test.Agent.LogMessages.Performance", "Committing performance test", null);

            //and store off our time
            TimeSpan duration = DateTimeOffset.UtcNow - startTime;

            Trace.TraceInformation("Sync Write Test Completed in {0}ms ({3:P} of baseline).  {1} messages were written at an average duration of {2}ms per message.",
                                   duration.TotalMilliseconds, MessagesPerTest, (duration.TotalMilliseconds) / MessagesPerTest, duration.TotalMilliseconds / m_TextDumpByLineBaseline.TotalMilliseconds);
        }
 

        [Test]
        public void TraceDirectCaptionDescription()
        {
            //first, lets get everything to flush so we have our best initial state.
            Log.EndFile("Preparing for Performance Test");
            Log.Verbose(LogWriteMode.WaitForCommit, "Test.Agent.LogMessages.Performance", "Preparing for performance test", null);

            //now that we know it's flushed everything, lets do our timed loop.
            DateTimeOffset startTime = DateTimeOffset.UtcNow;

            for (int curMessage = 0; curMessage < MessagesPerTest; curMessage++)
            {
                Log.TraceInformation("Test Message {0} Caption\r\nTest Message {0} Description, with some content added to it's at least the size you'd expect a normal description to be of a message", curMessage);
            }

            //and store off our time
            TimeSpan duration = DateTimeOffset.UtcNow - startTime;

            Trace.TraceInformation("Trace Direct Caption Description Test Completed in {0}ms ({3:P} of baseline).  {1} messages were written at an average duration of {2}ms per message.",
                                   duration.TotalMilliseconds, MessagesPerTest, (duration.TotalMilliseconds) / MessagesPerTest, duration.TotalMilliseconds / m_TextDumpByLineBaseline.TotalMilliseconds);
        }

        [Test]
        public void TraceDirectCaption()
        {
            //first, lets get everything to flush so we have our best initial state.
            Log.EndFile("Preparing for Performance Test");
            Log.Verbose(LogWriteMode.WaitForCommit, "Test.Agent.LogMessages.Performance", "Preparing for performance test", null);

            //now that we know it's flushed everything, lets do our timed loop.
            DateTimeOffset startTime = DateTimeOffset.UtcNow;

            for (int curMessage = 0; curMessage < MessagesPerTest; curMessage++)
            {
                Log.TraceInformation("Test Message {0} Caption and Description, with some content added to it's at least the size you'd expect a normal description to be of a message", curMessage);
            }

            //and store off our time
            TimeSpan duration = DateTimeOffset.UtcNow - startTime;

            Trace.TraceInformation("Trace Direct Message Test Completed in {0}ms ({3:P} of baseline).  {1} messages were written at an average duration of {2}ms per message.",
                                   duration.TotalMilliseconds, MessagesPerTest, (duration.TotalMilliseconds) / MessagesPerTest, duration.TotalMilliseconds / m_TextDumpByLineBaseline.TotalMilliseconds);
        }
    }

    public class DummyMessageSourceProvider : IMessageSourceProvider
    {
        public DummyMessageSourceProvider(string className, string methodName, string fileName, int lineNumber)
        {
            ClassName = className;
            MethodName = methodName;
            FileName = fileName;
            LineNumber = lineNumber;
        }

        /// <summary>
        /// Should return the simple name of the method which issued the log message.
        /// </summary>
        public string MethodName { get; private set; }

        /// <summary>
        /// Should return the full name of the class (with namespace) whose method issued the log message.
        /// </summary>
        public string ClassName { get; private set; }

        /// <summary>
        /// Should return the name of the file containing the method which issued the log message.
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// Should return the line within the file at which the log message was issued.
        /// </summary>
        public int LineNumber { get; private set; }
    }

    /// <summary>
    /// Summary description for FileLogger.
    /// </summary>
    public class DumbFileLogger
    {
        /// <summary>
        /// The name of the file to which this Logger is writing.
        /// </summary>
        private String _fileName;

        /// <summary>
        /// Gets and sets the file name.
        /// </summary>
        public String FileName
        {
            get { return _fileName; }
            set { _fileName = value; }
        }

        /// <summary>
        /// Create a new instance of FileLogger.
        /// </summary>
        /// <param name="aFileName">The name of the file to which this Logger should write.</param>
        public DumbFileLogger(String aFileName)
        {
            FileName = aFileName;
        }

        /// <summary>
        /// Create a new FileStream.
        /// </summary>
        /// <returns>The newly created FileStream.</returns>
        private FileStream CreateFileStream()
        {
            return new FileStream(FileName, FileMode.Append);
        }

        /// <summary>
        /// Get the FileStream.
        /// Create the directory structure if necessary.
        /// </summary>
        /// <returns>The FileStream.</returns>
        private FileStream GetFileStream()
        {
            try
            {
                return CreateFileStream();
            }
            catch (DirectoryNotFoundException)
            {
                Directory.CreateDirectory((new FileInfo(FileName)).DirectoryName);
                return CreateFileStream();
            }
        }

        /// <summary>
        /// Create a new StreamWriter.
        /// </summary>
        /// <returns>A new StreamWriter.</returns>
        private StreamWriter GetStreamWriter()
        {
            return new StreamWriter(GetFileStream());
        }

        /// <summary>
        /// Write the String to the file.
        /// </summary>
        /// <param name="s">The String representing the LogEntry being logged.</param>
        /// <returns>true upon success, false upon failure.</returns>
        public bool WriteToLog(String s)
        {
            StreamWriter writer = null;
            try
            {
                writer = GetStreamWriter();
                writer.WriteLine(s);
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    writer?.Dispose();
                }
                catch
                {
                }
            }
            return true;
        }

    }
}
