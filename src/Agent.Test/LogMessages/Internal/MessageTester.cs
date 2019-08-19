using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Gibraltar.Agent;
using Loupe.Extensibility.Data;

namespace Loupe.Agent.Test.LogMessages.Internal
{
    internal class MessageTester : IDisposable
    {
        private int _criticalCount;
        private int _errorCount;
        private int _warningCount;
        private int _infoCount;
        private int _verboseCount;
        private List<ILogMessage> _message;

        public MessageTester()
        {
            Log.MessagePublished += LogOnMessage;
        }

        public int CriticalCount
        {
            get
            {
                lock(this)
                {
                    return _criticalCount;
                }
            }
        }

        public int ErrorCount
        {
            get
            {
                lock (this)
                {
                    return _errorCount;
                }
            }
        }

        public int WarningCount
        {
            get
            {
                lock (this)
                {
                    return _warningCount;
                }
            }
        }

        public int InfoCount
        {
            get
            {
                lock (this)
                {
                    return _infoCount;
                }
            }
        }

        public int VerboseCount
        {
            get
            {
                lock (this)
                {
                    return _verboseCount;
                }
            }
        }

        public List<ILogMessage> Message
        {
            get
            {
                lock (this)
                {
                    return _message;
                }
            }
        }

        /// <summary>
        /// Wait for any messages in the queue to commit.
        /// </summary>
        public void WaitForMessages()
        {
            Gibraltar.Monitor.Log.Flush();

            //since the notifier we use is asynchronous we can't actually bank on the flush
            //delay getting things to us.  Really need to redesign that.
            Thread.Sleep(100);
        }

        /// <summary>
        /// Reset the buffer and counts
        /// </summary>
        public void Reset()
        {
            WaitForMessages();

            lock (this)
            {
                _criticalCount = 0;
                _errorCount = 0;
                _warningCount = 0;
                _infoCount = 0;
                _verboseCount = 0;

                _message = new List<ILogMessage>();
            }
        }

        public void Dispose()
        {
            Log.MessagePublished -= LogOnMessage;
        }

        private void LogOnMessage(object sender, LogMessageEventArgs e)
        {
            lock (this)
            {
                _criticalCount += e.CriticalCount;
                _errorCount += e.ErrorCount;
                _warningCount += e.WarningCount;
                _infoCount += e.Messages.Count(m => m.Severity == LogMessageSeverity.Information);
                _verboseCount += e.Messages.Count(m => m.Severity == LogMessageSeverity.Verbose);
                _message.AddRange(e.Messages);
            }
        }
    }
}
