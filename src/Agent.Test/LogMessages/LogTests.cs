using System;
using System.Threading;
using Gibraltar.Agent;
using Loupe.Agent.Test.LogMessages.Internal;
using NUnit.Framework;


namespace Loupe.Agent.Test.LogMessages
{
    /// <summary>
    /// Test the Log class's direct logging capabilities.  Metrics are not verified in this class
    /// </summary>
    [TestFixture]
    public class LogTests
    {
        /// <summary>
        /// Write a log message using each different trace log statement on the Log object
        /// </summary>
        [Ignore("Loupe doesn't have Trace support for .NET Core/Standard yet")]
        [Test]
        public void WriteTrace()
        {
            using (var verifier = new MessageTester())
            {
                verifier.Reset();
                Log.TraceVerbose("This is a call to Log.TraceVerbose with no arguments");
                Log.TraceVerbose("This is a call to Log.TraceVerbose with two arguments #1:{0}, #2:{1}", 1, 2);
                Log.TraceVerbose(new AssertionException("This is our test trace exception"),
                    "This is a call to Log.TraceVerbose with no arguments");
                Log.TraceVerbose(new AssertionException("This is our test trace exception"),
                    "This is a call to Log.TraceVerbose with two arguments #1:{0}, #2:{1}", 1, 2);
                verifier.WaitForMessages();
                Assert.That(verifier.VerboseCount, Is.EqualTo(4));

                verifier.Reset();
                Log.TraceInformation("This is a call to Log.TraceInformation with no arguments");
                Log.TraceInformation("This is a call to Log.TraceInformation with two arguments #1:{0}, #2:{1}", 1, 2);
                Log.TraceInformation(new AssertionException("This is our test trace information exception"),
                    "This is a call to Log.TraceInformation with no arguments");
                Log.TraceInformation(new AssertionException("This is our test trace information exception"),
                    "This is a call to Log.TraceInformation with two arguments #1:{0}, #2:{1}", 1, 2);
                verifier.WaitForMessages();
                Assert.That(verifier.InfoCount, Is.EqualTo(4));

                verifier.Reset();
                Log.TraceWarning("This is a call to Log.TraceWarning with no arguments");
                Log.TraceWarning("This is a call to Log.TraceWarning with two arguments #1:{0}, #2:{1}", 1, 2);
                Log.TraceWarning(new AssertionException("This is our test trace warning exception"),
                    "This is a call to Log.TraceWarning with no arguments");
                Log.TraceWarning(new AssertionException("This is our test trace warning exception"),
                    "This is a call to Log.TraceWarning with two arguments #1:{0}, #2:{1}", 1, 2);
                verifier.WaitForMessages();
                Assert.That(verifier.WarningCount, Is.EqualTo(4));

                verifier.Reset();
                Log.TraceError("This is a call to Log.TraceError with no arguments");
                Log.TraceError("This is a call to Log.TraceError with two arguments #1:{0}, #2:{1}", 1, 2);
                Log.TraceError(new AssertionException("This is our test trace error exception"),
                    "This is a call to Log.TraceError with no arguments");
                Log.TraceError(new AssertionException("This is our test trace error exception"),
                    "This is a call to Log.TraceError with two arguments #1:{0}, #2:{1}", 1, 2);
                verifier.WaitForMessages();
                Assert.That(verifier.ErrorCount, Is.EqualTo(4));

                verifier.Reset();
                Log.TraceCritical("This is a call to Log.TraceCritical with no arguments");
                Log.TraceCritical("This is a call to Log.TraceCritical with two arguments #1:{0}, #2:{1}", 1, 2);
                Log.TraceCritical(new AssertionException("This is our test trace critical exception"),
                    "This is a call to Log.TraceCritical with no arguments");
                Log.TraceCritical(new AssertionException("This is our test trace critical exception"),
                    "This is a call to Log.TraceCritical with two arguments #1:{0}, #2:{1}", 1, 2);
                verifier.WaitForMessages();
                Assert.That(verifier.CriticalCount, Is.EqualTo(4));
            }
        }
        
        /// <summary>
        /// Write a log message with an attached exception object.
        /// </summary>
        [Test]
        public void WriteException()
        {
            Log.Warning(new AssertionException("This is our test assertion exception"),
                        "Test.Agent.LogMessages.Write", "Test of logging exception attachment.", null);

            Log.Warning(new AssertionException("This is our top exception", new AssertionException("This is our middle exception",
                                               new AssertionException("This is our bottom exception"))),
                      "Test.Agent.LogMessages.Write", "Test of logging exception attachment with nested exceptions.", null);
        }

        /// <summary>
        /// Write many messages so we can verify order in the viewer.  The goal is to write many messages so that the order has to be
        /// controlled by sequence number, not timestamp.
        /// </summary>
        [Test]
        public void WriteMessagesForOrderTesting()
        {
            for (int curLogMessage = 1; curLogMessage < 3000; curLogMessage++)
            {
                Log.TraceVerbose("This is log message #{0}", curLogMessage);
            }
        }

        /// <summary>
        /// Write a log message using the full trace message entrance point.
        /// </summary>
        [Test]
        public void WriteMessageFullFormat()
        {
            //do one that should be pinned on US
            Log.Write(LogMessageSeverity.Verbose, "GibraltarTest", 0, null, LogWriteMode.Queued, null,
                "Test.Agent.LogMessages.WriteMessage", "This message should be verbose and ascribed to the LogTests class.", null);
            Log.Write(LogMessageSeverity.Critical, "GibraltarTest", 1, null, LogWriteMode.Queued, null,
                "Test.Agent.LogMessages.WriteMessage", "This message should be critical and ascribed to whatever is calling our test class.", null);
            Log.Write(LogMessageSeverity.Error, "GibraltarTest", -1, null, LogWriteMode.Queued, null,
                "Test.Agent.LogMessages.WriteMessage", "This message should be error and also ascribed to the LogTests class.", null);
        }

        /// <summary>
        /// Write log messages including XML details.
        /// </summary>
        [Test]
        public void WriteMessageWithDetails()
        {
            Log.VerboseDetail("<test>of<simple/>XML</test>", "Test.Agent.LogMessages.WriteDetail", "Simple XML details", "XML details:\r\n{0}",
                "<test>of<simple/>XML</test>");

            Log.VerboseDetail("<?xml version=\"1.0\" encoding=\"utf-16\" ?><test><data index=\"1\" value=\"4\" /><data index=\"2\" value=\"8\" /></test>",
                "Test.Agent.LogMessages.WriteDetail", "Test data details", "Values:\r\n{0}: {1}\r\n{2}: {3}\r\n", 1, 4, 2, 8);
            Log.InformationDetail("<?xml version=\"1.0\" encoding=\"utf-8\" ?><test><data index=\"3\" value=\"15\" /><data index=\"4\" value=\"16\" /></test>",
                "Test.Agent.LogMessages.WriteDetail", "Test data details", "Values:\r\n{0}: {1}\r\n{2}: {3}\r\n", 3, 15, 4, 16);
            Log.WarningDetail("<?xml version=\"1.0\" encoding=\"utf-8\" ?><test><data index=\"5\" value=\"23\" /><data index=\"6\" value=\"42\" /></test>",
                "Test.Agent.LogMessages.WriteDetail", "Test data details", "Values:\r\n{0}: {1}\r\n{2}: {3}\r\n", 5, 23, 6, 42);

            Log.Write(LogMessageSeverity.Error, "GibraltarTest", 0, null, LogWriteMode.Queued,
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n" +
                "<test complete=\"true\"><data index=\"1\" value=\"4\" /><data index=\"2\" value=\"8\" />\r\n" +
                "<data index=\"3\" value=\"15\" /><data index=\"4\" value=\"16\" />\r\n" +
                "<data index=\"5\" value=\"23\" /><data index=\"6\" value=\"42\" /></test>",
                "Test.Agent.LogMessages.WriteDetail", "Test data complete", "Test sequence data: {0}, {1}, {2}, {3}, {4}, {5}\r\n",
                4, 8, 15, 16, 23, 42);

            Log.ErrorDetail(@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <system.diagnostics>
    <trace autoflush=""false"" indentsize=""4"">
      <listeners>
        <add name=""Gibraltar"" type=""Gibraltar.Agent.Net.LogListener, Gibraltar"" />
      </listeners>
    </trace>
  </system.diagnostics>
</configuration>",
                 "Test.Agent.LogMessages.WriteDetail", "Sample app.config",
@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <system.diagnostics>
    <trace autoflush=""false"" indentsize=""4"">
      <listeners>
        <add name=""Gibraltar"" type=""Gibraltar.Agent.Net.LogListener, Gibraltar"" />
      </listeners>
    </trace>
  </system.diagnostics>
</configuration>");

            Log.CriticalDetail("<?xml version=\"1.0\" encoding=\"utf-8\" ?><test><data this=\"okay\" /><data this=\"malformed XML\"><data this=\"also okay\" /></test>",
                "Test.Agent.LogMessages.WriteDetail", "Test data bad details", "The details contains mis-matched XML:\r\n{0}",
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?><test><data this=\"okay\" /><data this=\"malformed XML\"><data this=\"also okay\" /></test>");
        }

        /// <summary>
        /// Test our handling of errors in formatted log message calls.
        /// </summary>
        [Test]
        public void WriteBadFormat()
        {
            Log.TraceVerbose("This is a test\tof a bad format call to Log.TraceVerbose()\n{0}, {1}, {2}", "zero", 1);
            Log.TraceVerbose("This is a test\r\nof a legal format call to Log.TraceVerbose()\t{0},\t{1},\t{2}", 0, null, "two");
            Log.TraceVerbose("This is a test\n\rof a bad format call to Log.TraceVerbose()\t{0},\t{1},\n{2}\t{3}", null, "one", "\"two\"");
            Log.TraceVerbose((string)null, 0, "null format test", 2);
            Log.TraceVerbose(string.Empty, "empty format test", 1);

            Thread.Sleep(2000); // Give it time to put stuff in the log.

            return;
        }

        [Test]
        public void WriteExceptionAttributedMessages()
        {
            try
            {
                var innerTest = new BadlyBehavedClass();
                innerTest.MethodThatThrowsException();
            }
            catch (Exception ex)
            {
                Log.Error(ex, true, "Test.Agent.LogMessages.Exception Attribution", "This should be attributed to the exception's call stack", "Not to the WriteExceptionAttributedMessages method.");
                Log.Critical(ex, true, "Test.Agent.LogMessages.Exception Attribution", "This should be attributed to the exception's call stack", "Not to the WriteExceptionAttributedMessages method.");
                Log.Error(ex, true, LogWriteMode.Queued, "Test.Agent.LogMessages.Exception Attribution", "This should be attributed to the exception's call stack", "Not to the WriteExceptionAttributedMessages method.");
                Log.Critical(ex, true, LogWriteMode.Queued, "Test.Agent.LogMessages.Exception Attribution", "This should be attributed to the exception's call stack", "Not to the WriteExceptionAttributedMessages method.");

                Log.Error(ex, false, "Test.Agent.LogMessages.Exception Attribution", "This should NOT be attributed to the exception's call stack", "It should be attributed to the WriteExceptionAttributedMessages method.");
                Log.Critical(ex, false, "Test.Agent.LogMessages.Exception Attribution", "This should NOT be attributed to the exception's call stack", "It should be attributed to the WriteExceptionAttributedMessages method.");
                Log.Error(ex, false, LogWriteMode.Queued, "Test.Agent.LogMessages.Exception Attribution", "This should NOT be attributed to the exception's call stack", "It should be attributed to the WriteExceptionAttributedMessages method.");
                Log.Critical(ex, false, LogWriteMode.Queued, "Test.Agent.LogMessages.Exception Attribution", "This should NOT be attributed to the exception's call stack", "It should be attributed to the WriteExceptionAttributedMessages method.");

                Log.Error(null, true, "Test.Agent.LogMessages.Exception Attribution", "This should NOT be attributed to the exception's call stack due to missing exception", "It should be attributed to the WriteExceptionAttributedMessages method.");
                Log.Critical(null, true, "Test.Agent.LogMessages.Exception Attribution", "This should NOT be attributed to the exception's call stack due to missing exception", "It should be attributed to the WriteExceptionAttributedMessages method.");
                Log.Error(null, true, LogWriteMode.Queued, "Test.Agent.LogMessages.Exception Attribution", "This should NOT be attributed to the exception's call stack due to missing exception", "It should be attributed to the WriteExceptionAttributedMessages method.");
                Log.Critical(null, true, LogWriteMode.Queued, "Test.Agent.LogMessages.Exception Attribution", "This should NOT be attributed to the exception's call stack due to missing exception", "It should be attributed to the WriteExceptionAttributedMessages method.");

                var exWithoutCallStack = new InvalidOperationException();
                Log.Error(exWithoutCallStack, true, "Test.Agent.LogMessages.Exception Attribution", "This should NOT be attributed to the exception's call stack due to exception lacking call stack", "It should be attributed to the WriteExceptionAttributedMessages method.");
                Log.Critical(exWithoutCallStack, true, "Test.Agent.LogMessages.Exception Attribution", "This should NOT be attributed to the exception's call stack due to exception lacking call stack", "It should be attributed to the WriteExceptionAttributedMessages method.");
                Log.Error(exWithoutCallStack, true, LogWriteMode.Queued, "Test.Agent.LogMessages.Exception Attribution", "This should NOT be attributed to the exception's call stack due to exception lacking call stack", "It should be attributed to the WriteExceptionAttributedMessages method.");
                Log.Critical(exWithoutCallStack, true, LogWriteMode.Queued, "Test.Agent.LogMessages.Exception Attribution", "This should NOT be attributed to the exception's call stack due to exception lacking call stack", "It should be attributed to the WriteExceptionAttributedMessages method.");
            }
        }


        public void BetterLogSample()
        {
            Log.Critical("Period.Delimited.Category", "This is a critical problem",
                "We are writing a test message with multiple insertion strings: {0} {1} {2}",
                "string", 124, DateTime.Now);

            Log.Warning("Period.Delimited.Category", "This might be a problem problem",
                "You don't have to provide insertion strings if you don't want to");

            //Any of the different severities can include details of an exception.  Don't bother 
            //dumping it in the message area; it'll all be showing in the Analyst under the Exception.
            Exception ex = new InvalidOperationException("This is an example invalid operation exception");
            Log.Error(ex, "Your Application.Exceptions", "We had an odd exception but managed to recover",
                "Here's a description of what we were doing.  We don't need to provide exception data.");

            //If you think the application might crash immediately after you call to record the message
            //you might want to make just this message synchronous.
            Log.Critical(LogWriteMode.WaitForCommit, "Your.Category", "We had a problem and may crash",
                "Just like our first method above we can now provide extended detail with insertion strings");

            Log.Verbose("Your.Category", "This is our lowest severity message",
                "Verbose is like a debug or success message - below all other severities.");
        }

    }
}
