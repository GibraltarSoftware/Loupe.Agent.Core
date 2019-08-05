using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Gibraltar.Monitor;
using Gibraltar.Monitor.Serialization;
using Gibraltar.Serialization;
using Loupe.Configuration;
using Loupe.Extensibility.Data;

namespace Gibraltar.Messaging
{
    /// <summary>
    /// The central publisher for messaging
    /// </summary>
    /// <remarks></remarks>
    public class Publisher
    {
        private readonly SessionSummary m_SessionSummary;
        private readonly object m_MessageQueueLock = new object();
        private readonly object m_MessageDispatchThreadLock = new object();
        private readonly object m_HeaderPacketsLock = new object();
        private readonly object m_ConfigLock = new object();
        private readonly PacketDefinitionList m_CachedTypes;
        private readonly PacketCache m_PacketCache;
        private readonly List<ICachedMessengerPacket> m_HeaderPackets = new List<ICachedMessengerPacket>(); //LOCKED BY HEADERPACKETSLOCK
        private readonly Queue<PacketEnvelope> m_MessageQueue;
        private readonly Queue<PacketEnvelope> m_MessageOverflowQueue;
        private readonly List<IMessenger> m_Messengers; // LOCKED BY CONFIGLOCK
        private readonly List<ILoupeFilter> m_Filters; //LOCKED BY CONFIGLOCK
        private readonly ApplicationUserCollection m_ApplicationUsers = new ApplicationUserCollection(); //lockless

        private readonly string m_SessionName;

        private AgentConfiguration m_Configuration;
        private Thread m_MessageDispatchThread; //LOCKED BY THREADLOCK
        private volatile bool m_MessageDispatchThreadFailed; //LOCKED BY THREADLOCK (and volatile to allow quick reading outside the lock)
        private int m_MessageQueueMaxLength = 2000; //LOCKED BY QUEUELOCK
        private bool m_ForceWriteThrough;
        private bool m_Initialized; //designed to enable us to do our initialization in the background. LOCKED BY THREADLOCK
        private bool m_ExitingMode; //forces writeThrough behavior when application is exiting LOCKED BY QUEUELOCK
        private bool m_ExitMode; //switch to background thread mode when application is exiting LOCKED BY QUEUELOCK
        private volatile bool m_Shutdown; //locks us down when we shut down LOCKED BY QUEUELOCK (and volatile to allow quick reading outside the lock)
        private long m_PacketSequence; //a monotonically increasing sequence number for packets as they get queued. LOCKED BY QUEUELOCK
        private bool m_Disposed;

        private IApplicationUserResolver m_ApplicationUserResolver; //LOCKED BY CONFIGLOCK
        private volatile IPrincipalResolver m_PrincipalResolver; //not locked for performance

        // A thread-specific static flag so we can disable blocking for Publisher and Messenger threads
        [ThreadStatic] private static bool t_ThreadMustNotBlock;
        // A thread-specific static flag so we can disable notification loops for Notifier threads
        [ThreadStatic] private static bool t_ThreadMustNotNotify;
        // A thread-specific static flag so we can prevent recursion in the application user resolver
        [ThreadStatic] private static bool t_InResolveUserEvent;


        internal static event PacketEventHandler MessageDispatching;

        internal static event LogMessageNotifyEventHandler LogMessageNotify;

        /// <summary>
        /// Create a new publisher
        /// </summary>
        /// <remarks>The publisher is a very central class; generally there should be only one per process.
        /// More specifically, there should be a one to one relationship between publisher, packet cache, and 
        /// messengers to ensure integrity of the message output.</remarks>
        public Publisher(string sessionName, AgentConfiguration configuration, SessionSummary sessionSummary)
        {
            if (string.IsNullOrEmpty(sessionName))
            {
                throw new ArgumentNullException(nameof(sessionName));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (sessionSummary == null)
            {
                throw new ArgumentNullException(nameof(sessionSummary));
            }

            //store off all our input
            m_SessionName = sessionName;
            m_SessionSummary = sessionSummary;
            m_Configuration = configuration;

            //create our queue, cache, and messenger objects
            m_MessageQueue = new Queue<PacketEnvelope>(50); //a more or less arbitrary initial queue size.
            m_MessageOverflowQueue = new Queue<PacketEnvelope>(50); //a more or less arbitrary initial queue size.
            m_CachedTypes = new PacketDefinitionList();
            m_PacketCache = new PacketCache();
            m_Messengers = new List<IMessenger>();
            m_Filters = new List<ILoupeFilter>();

            m_MessageQueueMaxLength = Math.Max(configuration.Publisher.MaxQueueLength, 1); //make sure there's no way to get it below 1.

            m_PrincipalResolver = new DefaultPrincipalResolver();

            //create the thread we use for dispatching messages
            CreateMessageDispatchThread();
        }

        /// <summary>
        /// Permanently disable blocking when queuing messages from this thread.
        /// </summary>
        /// <remarks>This allows threads to switch on their thread-specific blocking-disabled flag for our queue, to
        /// guard against deadlocks in threads which are responsible for consuming and processing items from our queue.
        /// WARNING: This setting can not be reversed.</remarks>
        internal static void ThreadMustNotBlock()
        {
            t_ThreadMustNotBlock = true;
        }

        /// <summary>
        /// Query whether waiting on our queue items has been permanently disabled for the current thread.
        /// </summary>
        /// <returns>This returns the thread-specific blocking-disabled flag.  This flag is false by default
        /// for each thread, unless Log.ThisThreadCannotLog() is called to set it to true.</returns>
        internal static bool QueryThreadMustNotBlock()
        {
            return t_ThreadMustNotBlock;
        }

        /// <summary>
        /// Permanently disable notification for messages issued from this thread.
        /// </summary>
        /// <remarks>This allows threads to switch on their thread-specific notification-disabled flag for our queue,
        /// to guard against indefinite loops in threads which are responsible for issuing notification events.
        /// WARNING: This setting can not be reversed.</remarks>
        internal static void ThreadMustNotNotify()
        {
            t_ThreadMustNotNotify = true;
        }

        /// <summary>
        /// Query whether notification alerts have been permanently disabled for messages issued by the current thread.
        /// </summary>
        /// <returns>This returns the thread-specific notification-disabled flag.  This flag is false by default
        /// for each thread, unless Log.ThisThreadCannotNotify() is called to set it to true.</returns>
        internal static bool QueryThreadMustNotNotify()
        {
            return t_ThreadMustNotNotify;
        }


        internal void RegisterPrincipalResolver(IPrincipalResolver resolver)
        {
            m_PrincipalResolver = resolver;
        }

        internal void RegisterApplicationUserResolver(IApplicationUserResolver resolver)
        {
            lock (m_ConfigLock)
            {
                m_ApplicationUserResolver = resolver;
            }
        }

        internal void RegisterFilter(ILoupeFilter filter)
        {
            lock (m_ConfigLock)
            {
                m_Filters.Add(filter);
            }
        }

        internal void UnregisterFilter(ILoupeFilter filter)
        {
            lock (m_ConfigLock)
            {
                m_Filters.Remove(filter);
            }
        }

        private void CreateMessageDispatchThread()
        {
            lock (m_MessageDispatchThreadLock)
            {
                //clear the dispatch thread failed flag so no one else tries to create our thread
                m_MessageDispatchThreadFailed = false;

                m_MessageDispatchThread = new Thread(MessageDispatchMain);
                m_MessageDispatchThread.Name = "Loupe Publisher"; //name our thread so we can isolate it out of metrics and such

                // We generally WANT to keep the app alive as a foreground thread so we make sure logs get flushed.
                // But once we have processed the exit command, we want to switch to a background thread
                // to process anything left (which will be forced to use writeThrough blocking), letting the
                // application be kept alive by its own foreground threads (while we continue to process any
                // new log messages they send), but not hold up the application further once they exit.
                m_MessageDispatchThread.IsBackground = m_ExitMode;
                m_MessageDispatchThread.Start();

                System.Threading.Monitor.PulseAll(m_MessageDispatchThreadLock);
            }
        }

        /// <summary>
        /// The main method of the message dispatch thread.  
        /// </summary>
        private void MessageDispatchMain()
        {
            try
            {
                // Before initialization... We must never allow this thread (which processes the queue!) to block
                // when adding items to our queue, or we would deadlock.  (Does not need the lock to set this.)
                ThreadMustNotBlock();

                bool backgroundThread;

                // Now we need to make sure we're initialized.
                lock (m_MessageDispatchThreadLock)
                {
                    //are we initialized?  
                    EnsureInitialized();

                    backgroundThread = m_MessageDispatchThread.IsBackground; // distinguish which we are.

                    System.Threading.Monitor.PulseAll(m_MessageDispatchThreadLock);
                }

                // Enter our main loop - dequeue packets and write them to all of the messengers.
                // Foreground thread should exit when we process exit, but a background thread should continue.
                while (m_Shutdown == false && (!m_ExitMode || backgroundThread))
                {
                    PacketEnvelope currentPacket = null;
                    lock (m_MessageQueueLock)
                    {
                        // Is this check needed?  We check m_ExitMode above outside the lock, so it may not have been
                        // up to date, but now we have the lock it should be.  If we're still on the foreground thread
                        // with m_ExitMode set to true, we want to exit the thread and create a background thread to
                        // continue dispatching, which we do upon exiting the while loop.
                        if (m_ExitMode && !backgroundThread)
                        {
                            break;
                        }

                        // If the queue is empty, wait for an item to be added
                        // This is a while loop, as we may be pulsed but not wake up before another thread has come in and
                        // consumed the newly added object or done something to modify the queue. In that case, we'll have to wait for another pulse.
                        while ((m_MessageQueue.Count == 0) && (m_Shutdown == false))
                        {
                            // This releases the message queue lock, only reacquiring it after being woken up by a call to Pulse
                            System.Threading.Monitor.Wait(m_MessageQueueLock);
                        }

                        if (m_MessageQueue.Count > 0)
                        {
                            //if we got here then there was an item in the queue AND we have the lock.  Dequeue the item and then we want to release our lock.
                            currentPacket = m_MessageQueue.Dequeue();

                            //and are we now below the maximum packet queue?  if so we can release the pending items.
                            while ((m_MessageOverflowQueue.Count > 0) && (m_MessageQueue.Count < m_MessageQueueMaxLength))
                            {
                                //we still have an item in the overflow queue and we have room for it, so lets add it.
                                PacketEnvelope currentOverflowEnvelope = m_MessageOverflowQueue.Dequeue();

                                m_MessageQueue.Enqueue(currentOverflowEnvelope);

                                //and indicate that we've submitted this queue item. This does a thread pulse under the covers,
                                //and gets its own lock so we should NOT lock the envelope.
                                currentOverflowEnvelope.IsPending = false;
                            }
                        }

                        //now pulse the next waiting thread there are that we've dequeued the packet.
                        System.Threading.Monitor.PulseAll(m_MessageQueueLock);
                    }

                    //We have a packet and have released the lock (so others can queue more packets while we're dispatching items.
                    if (currentPacket != null)
                    {
                        lock (m_ConfigLock)
                        {
                            DispatchPacket(currentPacket);
                        }
                    }
                }

                // We only get here if we exited the loop because a foreground thread sees we are in ExitMode,
                // or if we are completely shut down.

                // Clear the dispatch thread variable since we're about to exit it and...
                m_MessageDispatchThread = null;
                if (m_Shutdown == false)
                {
                    CreateMessageDispatchThread(); // Recreate one as a background thread, if we aren't shut down.
                }
            }
            catch (Exception ex)
            {
                lock (m_MessageDispatchThreadLock)
                {
                    //clear the dispatch thread variable since we're about to exit.
                    m_MessageDispatchThread = null;

                    //we want to write out that we had a problem and mark that we're failed so we'll get restarted.
                    m_MessageDispatchThreadFailed = true;

                    System.Threading.Monitor.PulseAll(m_MessageDispatchThreadLock);
                }

                OnThreadAbort();

                GC.KeepAlive(ex);
            }
        }

        private void OnThreadAbort()
        {
            lock (m_MessageQueueLock)
            {
                if (m_ExitMode == false) // If we aren't exiting yet then we want to allow a new thread to pick up and work the queue.
                    return;

                // We need to dump the queues and tell everyone to stop waiting, because we'll never process them.
                m_Shutdown = true; // Consider us shut down after this.  The app is really exiting.
                PacketEnvelope envelope;
                while (m_MessageQueue.Count > 0)
                {
                    envelope = m_MessageQueue.Dequeue();
                    envelope.IsPending = false;
                    envelope.IsCommitted = true;
                }

                while (m_MessageOverflowQueue.Count > 0)
                {
                    envelope = m_MessageOverflowQueue.Dequeue();
                    envelope.IsPending = false;
                    envelope.IsCommitted = true;
                }

                System.Threading.Monitor.PulseAll(m_MessageQueueLock);
            }
        }

        /// <summary>
        /// Send the packet to every current messenger and add it to the packet cache if it's cachable
        /// </summary>
        /// <param name="envelope"></param>
        private void DispatchPacket(PacketEnvelope envelope)
        {
            IMessengerPacket packet;
            lock (envelope)
            {
                packet = envelope.Packet; // rather than dig it out each time
                bool writeThrough = envelope.WriteThrough;

                // Any special handling for this packet?
                if (envelope.IsCommand)
                {
                    //this is a command packet, we process it as a command instead of just a data message
                    CommandPacket commandPacket = (CommandPacket)packet;

                    // Is this our exit or shutdown packet?  We need to handle those here.
                    if (commandPacket.Command == MessagingCommand.ExitMode)
                    {
                        m_ExitMode = true; // Mark us in ExitMode.  We will be by the time this method returns.
                        // Make sure we block until each messenger flushes, even if we weren't already in writeThrough mode.
                        writeThrough = true;
                    }
                    else if (commandPacket.Command == MessagingCommand.CloseMessenger)
                    {
                        m_Shutdown = true; // Mark us as shut down.  We will be by the time this method returns.
                        // Make sure we block until each messenger closes, even if we weren't already in writeThrough mode.
                        writeThrough = true;
                    }
                }
                else
                {
                    // Not a command, so it must be a Gibraltar data packet of some type.  

                    //stamp the packet, and all of its dependent packets (this sets the sequence number)
                    StampPacket(packet, packet.Timestamp);

                    GibraltarPacket gibraltarPacket = packet as GibraltarPacket;
                    if (gibraltarPacket != null)
                    {
                        //this is a gibraltar packet so lets go ahead and fix the data in place now that we're on the background thread.
                        gibraltarPacket.FixData();
                    }

                    //resolve the application user if feasible..
                    if (packet is IUserPacket userPacket && userPacket.Principal != null)
                    {
                        var userResolver = m_ApplicationUserResolver;
                        if (userResolver != null)
                        {
                            ResolveApplicationUser(userResolver, userPacket);
                        }
                    }

                    //and finally run it through our filters..
                    var cancel = false;
                    var filters = m_Filters;
                    if (filters != null)
                    {
                        foreach (var filter in filters)
                        {
                            try
                            {
                                filter.Process(packet, ref cancel);
                                if (cancel) break;
                            }
                            catch (Exception)
                            {
                                Log.DebugBreak(); // Catch this in the debugger, but otherwise swallow any errors.
                            }
                        }
                    }

                    //if a filter canceled then we can't write out this packet.
                    if (cancel)
                    {
                        envelope.IsCommitted = true; //so people waiting on us don't stall..
                        return;
                    }
                }

                //If this is a header packet we want to put it in the header list now - that way
                //if any messenger recycles while we are writing to the messengers it will be there.
                //(Better to pull the packet forward than to risk having it in an older stream but not a newer stream)
                if (envelope.IsHeader)
                {
                    lock (m_HeaderPacketsLock)
                    {
                        m_HeaderPackets.Add((ICachedMessengerPacket)packet);
                        System.Threading.Monitor.PulseAll(m_HeaderPacketsLock);
                    }
                }

                // Data message or Command packet - either way, send it on to each messenger.
                foreach (IMessenger messenger in m_Messengers)
                {
                    //we don't want an exception with one messenger to cause us a problem, so each gets its own try/catch
                    try
                    {
                        messenger.Write(packet, writeThrough);
                    }
                    catch (Exception)
                    {
                        Log.DebugBreak(); // Stop in debugger, ignore in production.
                    }
                }

                //if this was a write through packet we need to let the caller know that it was committed.
                envelope.IsCommitted = true; //under the covers this does a pulse on the threads waiting on this envelope.
            }

            // Now that it's committed, finally send it to any Notifiers that may be subscribed.
            QueueToNotifier(packet);

            //we only need to do this here if the session file writer is disabled; otherwise it's doing it at the best boundary.
            if ((m_Configuration.SessionFile.Enabled == false)
                && (packet.Sequence % 8192 == 0))
            {
                StringReference.Pack();
            }
        }

        private void ResolveApplicationUser(IApplicationUserResolver resolver, IUserPacket packet)
        {
            var userName = packet.Principal?.Identity?.Name;

            //if we don't have a user name at all then there's nothing to do
            if (string.IsNullOrEmpty(userName))
                return;

            //prevent infinite recursion
            if (t_InResolveUserEvent)
                return;

            if (m_ApplicationUsers.TryFindUserName(userName, out var applicationUser) == false)
            {
                //since we have a miss we want to give our resolver a shot..
                try
                {
                    t_InResolveUserEvent = true;
                    applicationUser = resolver.ResolveApplicationUser(packet.Principal, () =>
                    {
                        var userPacket = new ApplicationUserPacket {FullyQualifiedUserName = userName};
                        StampPacket(userPacket, DateTimeOffset.Now);
                        return new ApplicationUser(userPacket);
                    });
                }
                catch (Exception ex)
                {
                    //we can't log this because that would create an infinite loop (ignoring our protection for same)
                    GC.KeepAlive(ex);
                }
                finally
                {
                    t_InResolveUserEvent = false;
                }

                if (applicationUser != null)
                {
                    //cache this so we don't keep going after it.
                    applicationUser = m_ApplicationUsers.TrySetValue(applicationUser);
                }
            }

            if (applicationUser != null)
            {
                packet.UserPacket = applicationUser.Packet;
            }
        }

        /// <summary>
        /// Perform first-time initialization.  We presume we're in a thread-safe lock.
        /// </summary>
        private void EnsureInitialized()
        {
            if (m_Initialized == false)
            {
                m_ForceWriteThrough = m_Configuration.Publisher.ForceSynchronous;

                //We need to load up the messengers in the configuration object.
                try
                {
                    if (m_Configuration.SessionFile.Enabled)
                    {
                        AddMessenger(m_Configuration.SessionFile);
                    }
                }
                catch (Exception)
                {
                }

                try
                {
                    if (m_Configuration.NetworkViewer.Enabled)
                    {
                        AddMessenger(m_Configuration.NetworkViewer);
                    }
                }
                catch (Exception)
                {
                }

                //and now we're initialized
                m_Initialized = true;
            }
        }

        private void AddMessenger(IMessengerConfiguration configuration)
        {
            IMessenger newMessenger = null;
            try
            {
                var messengerType = Type.GetType(configuration.MessengerTypeName);

                if (messengerType != null)
                {
                    newMessenger = (IMessenger)Activator.CreateInstance(messengerType);
                }
            }
            catch (Exception)
            {
            }

            //next step: initialize it
            if (newMessenger != null)
            {
                try
                {
                    newMessenger.Initialize(this, configuration);

                    //now add it to our collection
                    lock (m_ConfigLock)
                    {
                        m_Messengers.Add(newMessenger);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void EnsureMessageDispatchThreadIsValid()
        {
            //see if for some mystical reason our message dispatch thread failed.  But if we're shut down then we don't care.
            if (m_MessageDispatchThreadFailed && m_Shutdown == false) // Check outside the lock for efficiency.  Valid because they're volatile.
            {
                //OK, now - even though the thread was failed in our previous line, we now need to get the thread lock and check it again
                //to make sure it didn't get changed on another thread.
                lock (m_MessageDispatchThreadLock)
                {
                    if (m_MessageDispatchThreadFailed)
                    {
                        //we need to recreate the message thread
                        CreateMessageDispatchThread();
                    }

                    System.Threading.Monitor.PulseAll(m_MessageDispatchThreadLock);
                }
            }
        }

        private void QueueToNotifier(IMessengerPacket packet)
        {
            LogMessageNotifyEventHandler notifyEvent = LogMessageNotify;
            if (notifyEvent != null)
                notifyEvent(this, new LogMessageNotifyEventArgs(packet));
        }

        /// <summary>
        /// Perform the actual package queuing and wait for it to be committed.  
        /// </summary>
        /// <remarks>This must be done within the message queue lock.  This method may return a null envelope if called
        /// on a thread which must not block and the packet had to be discarded due to an overflow condition.</remarks>
        /// <param name="packet">The packet to be queued</param>
        /// <param name="writeThrough">True if the call should block the current thread until the packet has been committed,
        /// false otherwise.</param>
        /// <returns>The packet envelope for the packet that was queued, or null if the packet was discarded.</returns>
        private PacketEnvelope QueuePacket(IMessengerPacket packet, bool writeThrough)
        {
            //even though the packet might already have a timestamp that's preferable to ours, we're deciding we're the judge of order to ensure it aligns with sequence.
            packet.Timestamp = DateTimeOffset.Now; //we convert to UTC during serialization, we want local time.            

            //wrap it in a packet envelope and indicate we're in write through mode.
            PacketEnvelope packetEnvelope = new PacketEnvelope(packet, writeThrough);

            //But what queue do we put the packet in?
            if ((m_MessageOverflowQueue.Count > 0) || (m_MessageQueue.Count > m_MessageQueueMaxLength))
            {
                // We are currently using the overflow queue, so we'll put it there.
                // However, if we were called by a must-not-block thread, we want to discard overflow packets...
                // unless it's a command packet, which is too important to discard (it just won't wait on pending).
                if (t_ThreadMustNotBlock && !packetEnvelope.IsCommand)
                {
                    packetEnvelope = null; // We won't queue this packet, so there's no envelope to hang onto.
                }
                else
                {
                    m_MessageOverflowQueue.Enqueue(packetEnvelope);

                    //and set that it's pending so our caller knows they need to wait for it.
                    packetEnvelope.IsPending = true;
                }
            }
            else
            {
                //just queue the packet, we don't want to wait.
                m_MessageQueue.Enqueue(packetEnvelope);
            }

            return packetEnvelope;
        }

        private void StampPacket(IMessengerPacket packet, DateTimeOffset defaultTimeStamp)
        {
#if DEBUG
            Debug.Assert(defaultTimeStamp.Ticks > 0);
#endif            
            //we don't check dependencies on command packets, it'll fail (and they aren't written out)
            if ((packet is CommandPacket) == false)
            {
                //check our dependent packets to see if they've been stamped.
                Dictionary<IPacket, IPacket> dependentPackets = GetRequiredPackets(packet);

                if ((dependentPackets != null) && (dependentPackets.Count > 0))
                {
                    //we only have to check these packets, not their children because if they've been stamped, their children have.
                    foreach (IPacket dependentPacket in dependentPackets.Values)
                    {
                        IMessengerPacket dependentMessengerPacket = dependentPacket as IMessengerPacket;
                        if ((dependentMessengerPacket != null)
                            && (dependentMessengerPacket.Sequence == 0) //our quickest bail check - if it has a nonzero sequence it's definitely been stamped.
                            && (dependentMessengerPacket.Timestamp.Ticks == 0))
                        {
                            //stamp this guy first, we depend on him and he's not been stamped.
                            StampPacket(dependentMessengerPacket, defaultTimeStamp);
                        }
                    }
                }
            }

            packet.Sequence = m_PacketSequence;
            m_PacketSequence++; //yeah, this could have been on the previous line.  but hey, this is really clear on order.

            //make sure we have a timestamp - if there isn't one use the default (which is the timestamp of the packet that depended on us or earlier)
            if (packet.Timestamp.Ticks == 0)
            {
                packet.Timestamp = defaultTimeStamp;
            }
        }

        /// <summary>
        /// Suspends the calling thread until the provided packet is committed.
        /// </summary>
        /// <remarks>Even if the envelope is not set to write through the method will not return until
        /// the packet has been committed.  This method performs its own synchronization and should not be done within a lock.</remarks>
        /// <param name="packetEnvelope">The packet that must be committed</param>
        private static void WaitOnPacket(PacketEnvelope packetEnvelope)
        {
            //we are monitoring for write through by using object locking, so get the lock...
            lock (packetEnvelope)
            {
                //and now we wait for it to be completed...
                while (packetEnvelope.IsCommitted == false)
                {
                    // This releases the envelope lock, only reacquiring it after being woken up by a call to Pulse
                    System.Threading.Monitor.Wait(packetEnvelope);
                }

                //as we exit, we need to pulse the packet envelope in case there is another thread waiting
                //on it as well.
                System.Threading.Monitor.PulseAll(packetEnvelope);
            }
        }


        /// <summary>
        /// Suspends the calling thread until the provided packet is no longer pending.
        /// </summary>
        /// <remarks>This method performs its own synchronization and should not be done within a lock.</remarks>
        /// <param name="packetEnvelope">The packet that must be submitted</param>
        private static void WaitOnPending(PacketEnvelope packetEnvelope)
        {
            //we are monitoring for pending by using object locking, so get the lock...
            lock (packetEnvelope)
            {
                //and now we wait for it to be submitted...
                while (packetEnvelope.IsPending)
                {
                    // This releases the envelope lock, only reacquiring it after being woken up by a call to Pulse
                    System.Threading.Monitor.Wait(packetEnvelope);
                }

                //as we exit, we need to pulse the packet envelope in case there is another thread waiting
                //on it as well.
                System.Threading.Monitor.PulseAll(packetEnvelope);
            }
        }

        private Dictionary<IPacket, IPacket> GetRequiredPackets(IPacket packet)
        {
            PacketDefinition previewDefinition;
            int previewTypeIndex = m_CachedTypes.IndexOf(packet);
            if (previewTypeIndex < 0)
            {
                previewDefinition = PacketDefinition.CreatePacketDefinition(packet);
                m_CachedTypes.Add(previewDefinition);
            }
            else
            {
                previewDefinition = m_CachedTypes[previewTypeIndex];
            }

            Dictionary<IPacket, IPacket> requiredPackets = previewDefinition.GetRequiredPackets(packet);
            return requiredPackets;
        }

        /// <summary>
        /// The central configuration of the publisher
        /// </summary>
        public PublisherConfiguration Configuration
        {
            get
            {
                //before we return the configuration, we need to have been initialized.
                return m_Configuration.Publisher;
            }
        }

        ///<summary>
        ///Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        ///</summary>
        ///<filterpriority>2</filterpriority>
        public void Dispose()
        {
            // Call the underlying implementation
            Dispose(true);

            // SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.
        /// </summary>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        protected virtual void Dispose(bool releaseManaged)
        {
            if (!m_Disposed)
            {
                if (releaseManaged)
                {
                    // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true)).
                    // Other objects may be referenced in this case.

                    // We need to stall until we are shut down.
                    if (m_Shutdown == false)
                    {
                        // We need to create and queue a close-messenger packet, and wait until it's processed.
                        Publish(new[] { new CommandPacket(MessagingCommand.CloseMessenger) }, true);
                    }
                }
                // Free native resources here (alloc's, etc).
                // May be called from within the finalizer, so don't reference other objects here.

                m_Disposed = true; // Make sure we only do this once
            }
        }

        /// <summary>
        /// The cache of packets that have already been published
        /// </summary>
        /// <remarks></remarks>
        public PacketCache PacketCache { get { return m_PacketCache; } }

        /// <summary>
        /// Publish the provided batch of packets.
        /// </summary>
        /// <param name="packetArray">An array of packets to publish as a batch.</param>
        /// <param name="writeThrough">True if the information contained in packet should be committed synchronously,
        /// false if the publisher can use write caching (when available).</param>
        public void Publish(IMessengerPacket[] packetArray, bool writeThrough)
        {
            // Sanity-check the most likely no-op cases before we bother with the lock
            if (packetArray == null)
                return;

            // Check for nulls from the end to find the last valid packet.
            int count = packetArray.Length;
            int lastIndex = count - 1;
            while (lastIndex >= 0 && packetArray[lastIndex] == null)
                lastIndex--;

            if (lastIndex < 0)
                return; // An array of only null packets (or empty), just quick bail.  Don't bother with the lock.

            //resolve users...
            var resolver = m_PrincipalResolver;
            IPrincipal principal = null;
            if (resolver != null)
            {
                //and set that user to each packet that wants to track the current user
                foreach (var packet in packetArray.AsEnumerable().OfType<IUserPacket>())
                {
                    //we only want to resolve the principal once per block, even if there are multiple messages.
                    if (principal == null)
                    {
                        try
                        {
                            principal = resolver.ResolveCurrentPrincipal();
                        }
                        catch (Exception ex)
                        {
                            Log.DebugBreak();
                            GC.KeepAlive(ex);
                        }

                        if (principal == null)
                            break; //no point in keeping trying if we filed to resolve the principal..
                    }
                    packet.Principal = principal;
                }
            }

            PacketEnvelope lastPacketEnvelope = null;

            bool effectiveWriteThrough;
            bool isPending;
            int queuedCount = 0;

            // Get the queue lock.
            lock (m_MessageQueueLock)
            {
                if (m_Shutdown) // If we're already shut down, just bail.  We'll never process it anyway.
                    return;

                // Check to see if either the overall force write through or the local write through are set...
                // or if we are in ExitingMode.  In those cases, we'll want to block until the packet is committed.
                effectiveWriteThrough = (m_ForceWriteThrough || writeThrough || m_ExitingMode);
                for (int i = 0; i < count; i++)
                {
                    IMessengerPacket packet = packetArray[i];

                    // We have to double-check each element for null, or QueuePacket() would barf on it.
                    if (packet != null)
                    {
                        // We have a real packet, so queue it.  Only WriteThrough for the last packet, to flush the rest.
                        PacketEnvelope packetEnvelope = QueuePacket(packet, effectiveWriteThrough && i >= lastIndex);

                        // If a null is returned, the packet wasn't queued, so don't overwrite lastPacketEnvelope.
                        if (packetEnvelope != null)
                        {
                            queuedCount++;
                            lastPacketEnvelope = packetEnvelope; // Keep track of the last one queued.

                            if (!m_ExitMode && packetEnvelope.IsCommand)
                            {
                                CommandPacket commandPacket = (CommandPacket)packet;
                                if (commandPacket.Command == MessagingCommand.ExitMode)
                                {
                                    // Once we *receive* an ExitMode command, all subsequent messages queued
                                    // need to block, to make sure the process stays alive for any final logging
                                    // foreground threads might have.  We will be switching the Publisher to a
                                    // background thread when we process the ExitMode command so we don't hold
                                    // up the process beyond its own foreground threads.
                                    m_ExitingMode = true; // Force writeThrough blocking from now on.

                                    // Set the ending status, if it needs to be (probably won't).
                                    SessionStatus endingStatus = (SessionStatus)commandPacket.State;
                                    if (m_SessionSummary.Status < endingStatus)
                                        m_SessionSummary.Status = endingStatus;
                                }
                            }
                        }
                    }
                }

                if (effectiveWriteThrough && t_ThreadMustNotBlock == false && queuedCount > 0 &&
                    (lastPacketEnvelope == null || ReferenceEquals(lastPacketEnvelope.Packet, packetArray[lastIndex]) == false))
                {
                    // The expected WriteThrough packet got dropped because of overflow?  But we still need to block until
                    // those queued have completed, so issue a specific Flush command packet, which should not get dropped.
                    CommandPacket flushPacket = new CommandPacket(MessagingCommand.Flush);
                    PacketEnvelope flushEnvelope = QueuePacket(flushPacket, true);
                    if (flushEnvelope != null)
                        lastPacketEnvelope = flushEnvelope;
                }

                // Grab the pending flag before we release the lock so we know we have a consistent view.
                // If we didn't queue any packets then lastPacketEnvelope will be null and there's nothing to be pending.
                isPending = (lastPacketEnvelope == null) ? false : lastPacketEnvelope.IsPending;

                // Now signal our next thread that might be waiting that the lock will be released.
                System.Threading.Monitor.PulseAll(m_MessageQueueLock);
            }

            // Make sure our dispatch thread is still going.  This has its own independent locking (when necessary),
            // so we don't need to hold up other threads that are publishing.
            EnsureMessageDispatchThreadIsValid();

            if (lastPacketEnvelope == null || t_ThreadMustNotBlock)
            {
                // If we had no actual packets queued (e.g. shutdown, or no packets to queue), there's nothing to wait on.
                // Also, special case for must-not-block threads.  Once it's on the queue (or not), don't wait further.
                // We need the thread to get back to processing stuff off the queue or we're deadlocked!
                return;
            }

            // See if we need to wait because we've degraded to synchronous message handling due to a backlog of messages
            if (isPending)
            {
                // This routine does its own locking so we don't need to interfere with the nominal case of 
                // not needing to pend.
                WaitOnPending(lastPacketEnvelope);
            }

            // Finally, if we need to wait on the write to complete now we want to stall.  We had to do this outside of
            // the message queue lock to ensure we don't block other threads.
            if (effectiveWriteThrough)
            {
                WaitOnPacket(lastPacketEnvelope);
            }
        }

        /// <summary>
        /// A generally unique name for this session
        /// </summary>
        /// <remarks>The session name consists of the application name and version and the session start date.  
        /// It will generally be unique except in the case where a user starts two instances of the same application in 
        /// the same second.</remarks>
        public string SessionName
        {
            get
            {
                return m_SessionName;
            }
        }

        /// <summary>
        /// The session summary for the session being published
        /// </summary>
        public SessionSummary SessionSummary
        {
            get { return m_SessionSummary; }
        }

        /// <summary>
        /// The list of cached packets that should be in every stream before any other packet.
        /// </summary>
        public ICachedMessengerPacket[] HeaderPackets
        {
            get
            {
                ICachedMessengerPacket[] returnVal;

                lock (m_HeaderPacketsLock) //MS doc inconclusive on thread safety of ToArray, so we guarantee add/ToArray safety.
                {
                    returnVal = m_HeaderPackets.ToArray();
                    System.Threading.Monitor.PulseAll(m_HeaderPacketsLock);
                }

                return returnVal;
            }
        }
    }

    internal delegate void PacketEventHandler(object sender, PacketEventArgs e);

    internal class PacketEventArgs : EventArgs
    {
        private readonly IMessengerPacket m_Packet;

        internal PacketEventArgs(IMessengerPacket packet)
        {
            m_Packet = packet;
        }

        internal IMessengerPacket Packet { get { return m_Packet; } }
    }
}
