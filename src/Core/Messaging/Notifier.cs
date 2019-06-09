
using System;
using System.Collections.Generic;
using System.Threading;
using Gibraltar.Data;
using Gibraltar.Monitor;
using Gibraltar.Monitor.Serialization;
using Loupe.Extensibility.Data;



namespace Gibraltar.Messaging
{
    /// <summary>
    /// Generates notifications from scanning log messages
    /// </summary>
    public class Notifier
    {
        private const string NotifierCategoryBase = "Gibraltar.Agent.Notifier";
        private const string NotifierThreadBase = "Gibraltar Notifier";
        private const int BurstMillisecondLatency = 28;
        private readonly object m_MessageQueueLock = new object();
        private readonly object m_MessageDispatchThreadLock = new object();
        private readonly LogMessageSeverity m_MinimumSeverity;
        private readonly bool m_GroupMessages;
        private readonly string m_NotifierName;
        private readonly string m_NotifierCategoryName;
        private readonly Queue<LogMessagePacket> m_MessageQueue; // LOCKED BY QUEUELOCK

        private Thread m_MessageDispatchThread; // LOCKED BY THREADLOCK
        private volatile bool m_MessageDispatchThreadFailed; // LOCKED BY THREADLOCK (and volatile to allow quick reading outside the lock)
        private int m_MessageQueueMaxLength = 2000; // LOCKED BY QUEUELOCK
        private DateTimeOffset m_BurstCollectionWait;  // LOCKED BY QUEUELOCK

        private DateTimeOffset m_LastNotifyCompleted; // Not locked.  Single-threaded use inside the dispatch loop only.
        private TimeSpan m_MinimumWaitNextNotify; // Not locked.  Single-threaded use inside the dispatch loop only.
        private DateTimeOffset m_NextNotifyAfter; // Not locked.  Single-threaded modify inside the dispatch loop only.
        private MessageSourceProvider m_EventErrorSourceProvider; // Not locked.  Single-threaded use inside the dispatch loop only.

        private event NotificationEventHandler m_NotificationEvent; // LOCKED BY QUEUELOCK (subscribed only through property)

        /// <summary>
        /// Create a Notifier looking for a given minimum LogMessageSeverity.
        /// </summary>
        /// <param name="minimumSeverity">The minimum LogMessageSeverity to look for.</param>
        /// <param name="notifierName">A name for this notifier (may be null for generic).</param>
        /// <param name="groupMessages">True to delay and group messages together for more efficient processing</param>
        public Notifier(LogMessageSeverity minimumSeverity, string notifierName, bool groupMessages = true)
        {
            m_MessageQueue = new Queue<LogMessagePacket>(50); // a more or less arbitrary initial size.
            m_MinimumSeverity = minimumSeverity;
            m_GroupMessages = groupMessages;
            m_MinimumWaitNextNotify = TimeSpan.Zero; // No delay by default.
            m_MessageDispatchThreadFailed = true; // We'll need to start one if we get a packet we care about.

            if (string.IsNullOrEmpty(notifierName))
            {
                m_NotifierName = string.Empty;
                m_NotifierCategoryName = NotifierCategoryBase;
            }
            else
            {
                m_NotifierName = notifierName;
                m_NotifierCategoryName = string.Format("{0}.{1}", NotifierCategoryBase, notifierName);
            }
        }

        /// <summary>
        /// Notification event.
        /// </summary>
        public event NotificationEventHandler NotificationEvent
        {
            add
            {
                if (value == null)
                    return;

                lock (m_MessageQueueLock)
                {
                    if (m_NotificationEvent == null)
                    {
                        // This is the first subscriber.  We need to initialize our own subscription to the publisher's event.
                        Publisher.LogMessageNotify -= Publisher_LogMessageNotify; // Ensure no duplicates.
                        Publisher.LogMessageNotify += Publisher_LogMessageNotify; // We need to subscribe.
                    }

                    m_NotificationEvent += value; // Subscribe them to the underlying event.
                }
            }
            remove
            {
                if (value == null)
                    return;

                lock (m_MessageQueueLock)
                {
                    if (m_NotificationEvent == null)
                        return; // Already empty, no subscriptions to remove.

                    m_NotificationEvent -= value; // Unsubscribe them from the underlying event.

                    if (m_NotificationEvent == null)
                    {
                        // That was the last subscriber.  We need to clean out our own subscription to the publisher's event.
                        Publisher.LogMessageNotify -= Publisher_LogMessageNotify; // Unsubscribe for efficiency.

                        m_MessageQueue.Clear(); // No more subscribers, dump the queue.
                        System.Threading.Monitor.PulseAll(m_MessageQueueLock); // Kick out of the wait so it can reset the times.
                    }
                }
            }
        }

        /// <summary>
        /// Get the CategoryName for this Notifier instance, as determined from the provided notifier name.
        /// </summary>
        public string NotifierCategoryName { get { return m_NotifierCategoryName; } }

        private void Publisher_LogMessageNotify(object sender, LogMessageNotifyEventArgs e)
        {
            QueuePacket(e.Packet);
        }

        private void QueuePacket(IMessengerPacket messengerPacket)
        {
            LogMessagePacket packet = messengerPacket as LogMessagePacket;
            if (packet == null || packet.SuppressNotification)
                return;

            if (packet.Severity > m_MinimumSeverity) // Severity compares in reverse.  Critical = 1, Verbose = 16.
                return; // Bail if this packet doesn't meet the minimum severity we care about.

            lock (m_MessageQueueLock)
            {
                if (m_NotificationEvent == null) // Check for unsubscribe race condition.
                    return; // Don't add it to the queue if there are no subscribers.

                int messageQueueLength = m_MessageQueue.Count;
                if (messageQueueLength < m_MessageQueueMaxLength)
                {
                    if (messageQueueLength <= 0) // First new one:  Wait for a burst to collect.
                        m_BurstCollectionWait = DateTimeOffset.MinValue; // Clear it so we'll reset the wait clock.

                    m_MessageQueue.Enqueue(packet);

                    // If there were already messages in our queue, it's waiting on a timeout, so don't bother pulsing it.
                    // But if there were no messages in the queue, we need to make sure it's not waiting forever!
                    if (messageQueueLength <= 0 || DateTimeOffset.Now >= m_NextNotifyAfter)
                        System.Threading.Monitor.PulseAll(m_MessageQueueLock);
                }
            }

            EnsureNotificationThreadIsValid();
        }

        private void EnsureNotificationThreadIsValid()
        {
            // See if for some mystical reason our notification dispatch thread failed.
            if (m_MessageDispatchThreadFailed) // Check it outside the lock for efficiency.  Valid because it's volatile.
            {
                // OK, now - even though the thread was failed in our previous line, we now need to get the thread lock and
                // check it again to make double-sure it didn't get changed on another thread.
                lock (m_MessageDispatchThreadLock)
                {
                    if (m_MessageDispatchThreadFailed)
                    {
                        // We need to (re)create the notification thread.
                        CreateNotificationDispatchThread();
                    }

                    System.Threading.Monitor.PulseAll(m_MessageDispatchThreadLock);
                }
            }
        }

        private void CreateNotificationDispatchThread()
        {
            lock (m_MessageDispatchThreadLock)
            {
                // Clear the dispatch thread failed flag so no one else tries to create our thread.
                m_MessageDispatchThreadFailed = false;

                // Name our thread so we can isolate it out of metrics and such.
                string threadName = (string.IsNullOrEmpty(m_NotifierName)) ? NotifierThreadBase
                                        : string.Format("{0} {1}", NotifierThreadBase, m_NotifierName);

                m_MessageDispatchThread = new Thread(NotificationDispatchMain);
                m_MessageDispatchThread.Name = threadName;
                m_MessageDispatchThread.IsBackground = true;
                m_MessageDispatchThread.Start();

                System.Threading.Monitor.PulseAll(m_MessageDispatchThreadLock);
            }
        }

        private void NotificationDispatchMain()
        {
            try
            {
                Publisher.ThreadMustNotNotify(); // Suppress notification about any messages issued on this thread so we don't loop!

                while (true)
                {
                    ILogMessage[] messages = null;
                    NotificationEventHandler notificationEvent;

                    // Wait until it is time to fire a notification event.
                    lock (m_MessageQueueLock)
                    {
                        while (m_MessageQueue.Count <= 0)
                        {
                            System.Threading.Monitor.Wait(m_MessageQueueLock); // Wait indefinitely until we get a message.

                            if (m_NotificationEvent == null)
                            {
                                m_MinimumWaitNextNotify = TimeSpan.Zero; // Reset default wait time.
                                m_NextNotifyAfter = DateTimeOffset.UtcNow; // Reset any forced wait pending.
                                m_MessageQueue.Clear(); // And dump the queue since we have no subscribers.  Loop again to wait.
                            }
                        }

                        if (m_GroupMessages)
                        {
                            DateTimeOffset now = DateTimeOffset.UtcNow;
                            if (m_BurstCollectionWait == DateTimeOffset.MinValue)
                            {
                                // We know there must be a positive Count to exit the wait loop above, so Peek() is safe.
                                DateTimeOffset firstTime = m_MessageQueue.Peek().Timestamp;
                                m_BurstCollectionWait = firstTime.AddMilliseconds(BurstMillisecondLatency);
                                if (m_BurstCollectionWait < now) // Are we somehow already past this burst wait period?
                                    m_BurstCollectionWait =
                                        now.AddMilliseconds(10); // Then allow a minimum wait in case of lag.
                            }

                            if (m_NextNotifyAfter < m_BurstCollectionWait && m_BurstCollectionWait > now)
                                m_NextNotifyAfter = m_BurstCollectionWait; // Wait for a burst to collect.

                            while (m_NextNotifyAfter > now && m_MessageQueue.Count > 0)
                            {
                                TimeSpan waitTime = m_NextNotifyAfter - now; // How long must we wait to notify again?
                                System.Threading.Monitor.Wait(m_MessageQueueLock, waitTime); // Wait the timeout.
                                now = DateTimeOffset.UtcNow;
                            }
                        }

                        // The wait has ended.  Get our subscriber(s) and messages, if any.
                        notificationEvent = m_NotificationEvent;
                        if (notificationEvent == null) // Have we lost all of our subscribers while waiting?
                        {
                            m_MinimumWaitNextNotify = TimeSpan.Zero; // Reset default wait time.
                            m_NextNotifyAfter = DateTimeOffset.UtcNow; // Reset any forced wait pending.
                        }
                        else if (m_MessageQueue.Count > 0) // Just to double-check; usually true here.
                        {
                            messages = m_MessageQueue.ToArray();
                        }

                        m_MessageQueue.Clear(); // If no subscribers, we can clear it anyway.
                    }

                    // Now it's time to fire a notification event.
                    if (messages != null)
                    {
                        // Fire the event from outside the lock.
                        NotificationEventArgs eventArgs = new NotificationEventArgs(messages, m_MinimumWaitNextNotify);

                        // see if we should default to automatically sending data using the rules we typically recommend.
                        var serverConfig = Log.Configuration.Server;
                        if ((eventArgs.TopSeverity <= LogMessageSeverity.Error) 
                            && (serverConfig.AutoSendOnError && serverConfig.AutoSendSessions && serverConfig.Enabled))
                        {
                            eventArgs.SendSession = true;
                            eventArgs.MinimumNotificationDelay = new TimeSpan(0, 5, 0);
                        }

                        try
                        {
                            notificationEvent(this, eventArgs); // Call our subscriber(s) (should just be the Agent layer).
                        }
                        catch (Exception ex)
                        {
                            Log.RecordException(EventErrorSourceProvider, ex, null, m_NotifierCategoryName, true);
                        }
                        finally
                        {
                            m_MinimumWaitNextNotify = eventArgs.MinimumNotificationDelay;
                            if (m_MinimumWaitNextNotify < TimeSpan.Zero) // Sanity-check that the wait value is non-negative.
                                m_MinimumWaitNextNotify = TimeSpan.Zero;

                            if (eventArgs.SendSession) // Did they signal us to send the current session now?
#pragma warning disable 4014
                                Log.SendSessions(SessionCriteria.ActiveSession, null, true); // Then let's send it before we start the wait time.
#pragma warning restore 4014

                            m_LastNotifyCompleted = DateTimeOffset.UtcNow;
                            m_NextNotifyAfter = m_LastNotifyCompleted + m_MinimumWaitNextNotify;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                lock (m_MessageDispatchThreadLock)
                {
                    //clear the dispatch thread variable since we're about to exit.
                    m_MessageDispatchThread = null;

                    //we want to write out that we had a problem and mark that we're failed so we'll get restarted.
                    m_MessageDispatchThreadFailed = true;

                    System.Threading.Monitor.PulseAll(m_MessageDispatchThreadLock);
                }
            }
        }


        private MessageSourceProvider EventErrorSourceProvider
        {
            get
            {
                if (m_EventErrorSourceProvider == null)
                {
                    m_EventErrorSourceProvider = new MessageSourceProvider("Gibraltar.Messaging.Notifier", "NotificationDispatchMain");
                }

                return m_EventErrorSourceProvider;
            }
        }
    }

    /// <summary>
    /// Handler type for a notification event.
    /// </summary>
    /// <param name="sender">The sender of this notification event.</param>
    /// <param name="e">The NotificationEventArgs.</param>
    public delegate void NotificationEventHandler(object sender, NotificationEventArgs e);

    /// <summary>
    /// Handler type for a LogMessage notify event.
    /// </summary>
    /// <param name="sender">The sender of this LogMessage notify event.</param>
    /// <param name="e">The LogMessageNotifyEventArgs.</param>
    internal delegate void LogMessageNotifyEventHandler(object sender, LogMessageNotifyEventArgs e);
}
