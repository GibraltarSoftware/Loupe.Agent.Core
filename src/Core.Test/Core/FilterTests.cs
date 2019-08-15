using System;
using System.Linq;
using System.Threading;
using Gibraltar.Messaging;
using Gibraltar.Monitor;
using Gibraltar.Monitor.Serialization;
using Loupe.Extensibility.Data;
using NUnit.Framework;

namespace Loupe.Core.Test.Core
{
    [TestFixture]
    public class FilterTests
    {
        [Test]
        public void Exception_When_Registering_Null_Filter()
        {
            Assert.Throws<ArgumentNullException>(() => Log.RegisterFilter(null));
        }

        [Test]
        public void Exception_When_Removing_Null_Filter()
        {
            Assert.Throws<ArgumentNullException>(() => Log.UnregisterFilter(null));
        }

        [Test]
        public void No_Exception_Removing_Nonexistant_Filter()
        {
            Log.UnregisterFilter(new NothingFilter());
        }


        [Test]
        public void Can_Add_Remove_Filter()
        {
            var filter = new NothingFilter();

            //the first message is done with wait for commit so we know we've written everything through the publisher before we connect up our event handler.
            Log.WriteMessage(LogMessageSeverity.Information, LogWriteMode.WaitForCommit, "Loupe", "LogTests.Filter.Add/Remove Filter",
                null, null, null, null, "Flushing message queue prior to doing filter test",
                "We should get no filtering as we haven't added the filter yet.");
            try
            {
                Log.RegisterFilter(filter);

                Log.WriteMessage(LogMessageSeverity.Information, LogWriteMode.WaitForCommit, "Loupe", "LogTests.Filter.Add/Remove Filter",
                    null, null, null, null, "Sending message that should be filtered",
                    "We just registered the filter so it should see this.");
                Assert.That(filter.FilterRequests, Is.GreaterThan(0));

                Log.UnregisterFilter(filter);
                var filterUseCount = filter.FilterRequests;

                Log.WriteMessage(LogMessageSeverity.Information, LogWriteMode.WaitForCommit, "Loupe", "LogTests.Filter.Add/Remove Filter",
                    null, null, null, null, "Sending message that should not be filtered",
                    "We should get no filtering as we removed the filter.");

                Assert.That(filter.FilterRequests, Is.EqualTo(filterUseCount));
            }
            finally
            {
                //extra remove should not cause problem and will ensure we remove.
                Log.UnregisterFilter(filter);
            }
        }

        [Test]
        public void Can_Rewrite_Log_Message()
        {
            string findValue = "the";
            string replaceValue = "{REPLACED}";

            var filter = new ReplaceWordFilter(findValue, replaceValue);

            try
            {
                Log.RegisterFilter(filter);

                Log.WriteMessage(LogMessageSeverity.Information, LogWriteMode.WaitForCommit, "Loupe", "LogTests.Filter.Replace Word Filter",
                    null, null, null, null, "This caption should show " + replaceValue + " here: " + findValue,
                    "This description should show {0} here: {1}\r\nAnd here: {1}\r\n{1} <-- And here", replaceValue, findValue);
            }
            finally
            {
                Log.UnregisterFilter(filter);                
            }
        }

        #region Private Class NothingFilter()

        private class NothingFilter : ILoupeFilter
        {
            private volatile int _FilterRequests;

            public void Process(IMessengerPacket packet, ref bool cancel)
            {
                Interlocked.Increment(ref _FilterRequests);
            }

            public int FilterRequests => _FilterRequests;
        }

        #endregion

        #region Private Class ReplaceWordFilter

        private class ReplaceWordFilter : ILoupeFilter
        {
            private readonly string _FindWord;
            private readonly string _ReplaceWord;

            public ReplaceWordFilter(string find, string replace)
            {
                _FindWord = find;
                _ReplaceWord = replace;
            }

            public void Process(IMessengerPacket packet, ref bool cancel)
            {
                if (packet is LogMessagePacket logMessagePacket)
                {
                    logMessagePacket.Caption = logMessagePacket.Caption?.Replace(_FindWord, _ReplaceWord);
                    logMessagePacket.Description = logMessagePacket.Description?.Replace(_FindWord, _ReplaceWord);
                    logMessagePacket.Details = logMessagePacket.Details?.Replace(_FindWord, _ReplaceWord);
                    if (logMessagePacket.HasException)
                    {
                        foreach (var exception in logMessagePacket.Exceptions.Cast<ExceptionInfoPacket>())
                        {
                            exception.Message = exception.Message?.Replace(_FindWord, _ReplaceWord);
                        }
                    }
                }
            }
        }

        #endregion

        [Test]
        public void Can_Rewrite_Log_Message_With_Lambda()
        {
            var findValue = "the";
            var replaceValue = "{REPLACED}";

            var filter = new DelegateLoupeFilter((packet) =>
            {
                if (packet is LogMessagePacket logMessagePacket)
                {
                    logMessagePacket.Caption = logMessagePacket.Caption?.Replace(findValue, replaceValue);
                    logMessagePacket.Description = logMessagePacket.Description?.Replace(findValue, replaceValue);
                    logMessagePacket.Details = logMessagePacket.Details?.Replace(findValue, replaceValue);
                    if (logMessagePacket.HasException)
                    {
                        foreach (var exception in logMessagePacket.Exceptions.Cast<ExceptionInfoPacket>())
                        {
                            exception.Message = exception.Message?.Replace(findValue, replaceValue);
                        }
                    }
                }
            });

            try
            {
                Log.RegisterFilter(filter);

                Log.WriteMessage(LogMessageSeverity.Information, LogWriteMode.WaitForCommit, "Loupe", "LogTests.Filter.Lambda Filter",
                    null, null, null, null, "This caption should show " + replaceValue + " here: " + findValue,
                    "This description should show {0} here: {1}\r\nAnd here: {1}\r\n{1} <-- And here", replaceValue, findValue);
            }
            finally
            {
                Log.UnregisterFilter(filter);
            }
        }

        [Test]
        public void Can_Filter_Log_Message_With_Lambda()
        {
            var filter = new DelegateLoupeFilter((packet) =>
            {
                if (packet is LogMessagePacket logMessagePacket)
                {
                    logMessagePacket.Caption = "This message should not be in the log";
                    return false;
                }

                return true;
            });

            try
            {
                Log.RegisterFilter(filter);

                Log.WriteMessage(LogMessageSeverity.Information, LogWriteMode.WaitForCommit, "Loupe", "LogTests.Filter.Lambda Filter",
                    null, null, null, null, "This message should be suppressed by the filter",
                    null);
            }
            finally
            {
                Log.UnregisterFilter(filter);
            }
        }

    }
}
