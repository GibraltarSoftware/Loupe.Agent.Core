using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Loupe.Monitor;
using Loupe.Configuration;
using Loupe.Extensibility.Data;


namespace Loupe.Messaging
{
    /// <summary>
    /// A baseline implementation of a messenger that provides common messenger functionality.
    /// </summary>
    /// <remarks>This implementation is somewhat more elaborate than necessary because it maintains
    /// a design that allows for multiple threads queuing requests, which is why it needs an overflow
    /// queue instead of just blocking as soon as it overflows.  This can be simplified, but lets make sure
    /// we can test this out first.</remarks>
    [DebuggerDisplay("Name={Name} Caption={Caption}")]
    internal abstract class MessengerBase : IMessenger
    {
        /// <summary>
        /// The log category to use for log messages in this class
        /// </summary>
        public const string LogCategory = "Loupe.Messenger";

        private readonly bool m_SupportsWriteThrough;
        private readonly object m_MessageQueueLock = new object();
        private readonly object m_MessageDispatchThreadLock = new object();
        private readonly Queue<PacketEnvelope> m_MessageQueue;
        private readonly Queue<PacketEnvelope> m_MessageOverflowQueue;

        private string m_Caption;
        private string m_Description;
        private string m_Name;

        private Publisher m_Publisher;
        private IMessengerConfiguration m_Configuration;
        private Thread m_MessageDispatchThread; //LOCKED BY THREADLOCK
        volatile private bool m_MessageDispatchThreadFailed; //LOCKED BY THREADLOCK
        private int m_MessageQueueMaxLength = 2000; //LOCKED BY QUEUELOCK
        private bool m_ForceWriteThrough;
        volatile private bool m_Initialized; //designed to enable us to do our initialization in the background. LOCKED BY THREADLOCK
        volatile private bool m_Exiting; //set true once we have an exit-mode or close-messenger command pending LOCKED BY QUEUELOCK
        private bool m_Exited; //set true once we have processed the exit-mode command LOCKED BY QUEUELOCK
        private bool m_Closed; //set true once we have been decommissioned (close-messenger) LOCKED BY QUEUELOCK
        private bool m_InOverflowMode; //a flag to indicate whether we're in overflow mode or not. LOCKED BY QUEUELOCK
        private bool m_MaintenanceMode; //a flag indicating when we're in maintenance mode. LOCKED BY QUEUELOCK
        private bool m_Disposed;
        private DateTime m_NextFlushDue;
        private long m_DroppedPackets; //the number of packets we've dropped if we're in the Drop overflow mode.

        /// <summary>
        /// Create a new dispatch queue
        /// </summary>
        /// <param name="name">A display name for this messenger to differentiate it from other messengers</param>
        /// <param name="supportsWriteThrough">True if the messenger supports synchronous (write through) processing</param>
        protected MessengerBase(string name, bool supportsWriteThrough = true)
        {
            m_Name = name;
            m_SupportsWriteThrough = supportsWriteThrough;

            //create our queue, cache, and messenger objects
            m_MessageQueue = new Queue<PacketEnvelope>(50); //a more or less arbitrary initial queue size.
            m_MessageOverflowQueue = new Queue<PacketEnvelope>(50); //a more or less arbitrary initial queue size.
        }

        #region Private Properties and Methods

        private void CreateMessageDispatchThread()
        {
            lock (m_MessageDispatchThreadLock)
            {
                //clear the dispatch thread failed flag so no one else tries to create our thread
                m_MessageDispatchThreadFailed = false;

                m_MessageDispatchThread = new Thread(MessageDispatchMain);
                m_MessageDispatchThread.Name = "Loupe Messenger"; //name our thread so we can isolate it out of metrics and such
                m_MessageDispatchThread.IsBackground = true;
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
                Publisher.ThreadMustNotBlock();

                // Now we need to make sure we're initialized.
                lock (m_MessageDispatchThreadLock)
                {
                    //are we initialized?  
                    EnsureInitialized();

                    System.Threading.Monitor.PulseAll(m_MessageDispatchThreadLock);
                }

                //Enter our main loop - dequeue packets and write them to all of the messengers.
                while ((m_Closed == false) && (m_Initialized)) //when we introduce state-full error handling, remove the initialized check
                {
                    PacketEnvelope currentPacket = null;
                    lock (m_MessageQueueLock)
                    {
                        // If the queue is empty, wait for an item to be added
                        // This is a while loop, as we may be pulsed but not wake up before another thread has come in and
                        // consumed the newly added object or done something to modify the queue. In that case, 
                        // we'll have to wait for another pulse.
                        while ((m_MessageQueue.Count == 0) && (m_Closed == false)
                            && ((AutoFlush == false) || (m_NextFlushDue > DateTime.Now)))
                        {
                            // This releases the message queue lock, only reacquiring it after being woken up by a call to Pulse
                            System.Threading.Monitor.Wait(m_MessageQueueLock, 1000); //we'll stop waiting after a second so we can check for an auto-flush 
                        }

                        if (m_MessageQueue.Count > 0)
                        {
                            //if we got here then there was an item in the queue AND we have the lock.  Dequeue the item and then we want to release our lock.
                            currentPacket = m_MessageQueue.Dequeue();

                            //Odd interlock case:  If we were in maintenance mode then we may have gone over our limit. Can we re-establish the limit?
                            if (m_MessageQueue.Count < m_MessageQueueMaxLength)
                            {
                                //we can just clear it:  Since the same thread in the same loop does maintenance mode, we won't get back here
                                //unless the derived class has already completed its maintenance.
                                m_MaintenanceMode = false;
                            }

                            //and are we now below the maximum packet queue?  if so we can release the pending items.
                            while ((m_MessageOverflowQueue.Count > 0) && (m_MessageQueue.Count < m_MessageQueueMaxLength))
                            {
                                TransferOverflow();
                            }
                        }

                        //now pulse the next waiting thread there are that we've dequeued the packet.
                        System.Threading.Monitor.PulseAll(m_MessageQueueLock);
                    }

                    MaintenanceModeRequest maintenanceRequested = MaintenanceModeRequest.None;

                    //We have a packet and have released the lock (so others can queue more packets while we're dispatching items.
                    if (currentPacket != null)
                    {
                        DispatchPacket(currentPacket, ref maintenanceRequested);
                    }

                    // Do we need to do an auto-flush before we do the next packet?
                    if (AutoFlush && m_NextFlushDue <= DateTime.Now)
                    {
                        ActionOnFlush(ref maintenanceRequested);
                    }

                    // Did they request maintenance mode?  If so we really need to change our behavior.
                    // But ignore regular maintenance requests if we're closing.  No sense in adding unnecessary work.
                    // Unless the client app explicitly requested a maintenance rollover, which we'll do regardless.
                    if (maintenanceRequested != MaintenanceModeRequest.None &&
                        (m_Exiting == false || maintenanceRequested == MaintenanceModeRequest.Explicit))
                    {
                        EnterMaintenanceMode();
                    }
                }

                //clear the dispatch thread variable since we're about to exit.
                m_MessageDispatchThread = null;
            }
            catch (Exception)
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
            }
        }

        private void OnThreadAbort()
        {
            lock (m_MessageQueueLock)
            {
                if (m_Exiting == false) // If we aren't exiting yet then we want to allow a new thread to pick up and work the queue.
                    return;

                // We need to dump the queues and tell everyone to stop waiting, because we'll never process them.
                m_Closed = true; // Consider us closed after this.  The app is really exiting.
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
            }
        }

        private void TransferOverflow()
        {
            //we still have an item in the overflow queue and we have room for it, so lets add it.
            PacketEnvelope currentOverflowEnvelope = m_MessageOverflowQueue.Dequeue();

            m_MessageQueue.Enqueue(currentOverflowEnvelope);

            //and indicate that we've submitted this queue item. This does a thread pulse under the covers,
            //and gets its own lock so we should NOT lock the envelope.
            currentOverflowEnvelope.IsPending = false;
        }

        /// <summary>
        /// Send the packet via our messenger and add it to our packet cache, if necessary.
        /// </summary>
        /// <param name="packetEnvelope"></param>
        /// <param name="maintenanceRequested">Specifies whether maintenance mode has been requested after this packet
        /// and the type (source) of that request.</param>
        private void DispatchPacket(PacketEnvelope packetEnvelope, ref MaintenanceModeRequest maintenanceRequested)
        {
            lock (packetEnvelope)
            {
                //We process all commands...
                if (packetEnvelope.IsCommand)
                {
                    //this is a command packet, we process it as a command instead of a data message
                    CommandPacket commandPacket = (CommandPacket)packetEnvelope.Packet;
                    MessagingCommand command = commandPacket.Command;

                    switch (command)
                    {
                        case MessagingCommand.ExitMode:
                            //mark that we're in exit mode.
                            m_Exiting = true; // Make double-sure that this got set.  It's our normal-exit state key.
                            m_Exited = true; // We have now processed the ExitMode command, if anyone cares.

                            ActionOnFlush(ref maintenanceRequested); // Is this really necessary?  Probably can't hurt.

                            //and call our exit event
                            ActionOnExit();
                            break;
                        case MessagingCommand.CloseMessenger:
                            //mark that we're closed.
                            m_Closed = true;

                            //and call our closed event
                            ActionOnClose();
                            break;
                        case MessagingCommand.Flush:
                            ActionOnFlush(ref maintenanceRequested);
                            break;
                        default:
                            // Allow special handling by inheritors
                            ActionOnCommand(command, commandPacket.State, packetEnvelope.WriteThrough, ref maintenanceRequested);
                            break;

                    }
                }
                else
                {
                    //it's a data packet - we send this to our overridden write method for our inheritor to process.

                    //we really don't want to expose the envelope at this time if we don't have to
                    ActionOnWrite(packetEnvelope.Packet, packetEnvelope.WriteThrough, ref maintenanceRequested);
                }

                //if this was a write through packet we need to let the caller know that it was committed.
                packetEnvelope.IsCommitted = true; //under the covers this does a pulse on the threads waiting on this envelope.

                System.Threading.Monitor.PulseAll(packetEnvelope);
            }
        }

        private void EnterMaintenanceMode()
        {
            //set our flag so we know we're in maintenance.  This affects the queuing, so we need a queuelock
            lock(m_MessageQueueLock)
            {
                m_MaintenanceMode = true;

                //Point of order:  We may be overfilling the queue if we're in maintenance mode.  This is because we don't want synchronous
                //overflow behavior while we are in maintenance or when catching back up from maintenance.
                //so, we need to be fair:  Since these packets are going into the main queue, if we have anything in the overflow queue it needs
                //to be rolled into the main queue too.  
                while (m_MessageOverflowQueue.Count > 0)
                {
                    TransferOverflow(); //common functionality
                }


                //for best MT response time we're going to pulse waiting threads
                System.Threading.Monitor.PulseAll(m_MessageQueueLock);
            }

            //and now we let our thread go in and do the maintenance
            ActionOnMaintenance();

            //we do NOT exit maintenance mode here:  that is done by the message dispatch thread when it sees that we've
            //gotten back below the maximum queue size (if we're above it).
        }

        /// <summary>
        /// Wraps calling the OnCommand() method that derived classes use to provide common exception handling.
        /// </summary>
        /// <param name="command">The MessagingCommand enum value of this command.</param>
        /// <param name="state">Optional.  Command arguments</param>
        /// <param name="writeThrough">Whether write-through (synchronous) behavior was requested.</param>
        /// <param name="maintenanceRequested">Specifies whether the handler requested maintenance mode and the type (source) of that request.</param>
        private void ActionOnCommand(MessagingCommand command, object state, bool writeThrough, ref MaintenanceModeRequest maintenanceRequested)
        {
            try
            {
                OnCommand(command, state, writeThrough, ref maintenanceRequested);
            }
            catch(Exception ex)
            {
                GC.KeepAlive(ex);
                Log.DebugBreak(); // Stop in debugger, ignore in production.
            }
        }

        /// <summary>
        /// Wraps calling the OnExit() method that derived classes use to provide common exception handling.
        /// </summary>
        private void ActionOnExit()
        {
            try
            {
                OnExit();
            }
            catch(Exception ex)
            {
                GC.KeepAlive(ex);
                Log.DebugBreak(); // Stop in debugger, ignore in production.
            }
        }

        /// <summary>
        /// Wraps calling the OnClose() method that derived classes use to provide common exception handling.
        /// </summary>
        private void ActionOnClose()
        {
            try
            {
                OnClose();
            }
            catch(Exception ex)
            {
                GC.KeepAlive(ex);
                Log.DebugBreak(); // Stop in debugger, ignore in production.
            }
        }

        /// <summary>
        /// Wraps calling the OnConfigurationUpdate() method that derived classes use to provide common exception handling.
        /// </summary>
        private void ActionOnConfigurationUpdate(IMessengerConfiguration configuration)
        {
            try
            {
                OnConfigurationUpdate(configuration);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                Log.DebugBreak(); // Stop in debugger, ignore in production.
            }
        }

        /// <summary>
        /// Wraps calling the OnFlush() method that derived classes use to provide common exception handling.
        /// </summary>
        private void ActionOnFlush(ref MaintenanceModeRequest maintenanceRequested)
        {
            //since we're starting the flush procedure, we'll assume that this is going to complete and there's no reason to autoflush
            if (AutoFlush)
            {
                UpdateNextFlushDue();
            }

            try
            {
                OnFlush(ref maintenanceRequested);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                Log.DebugBreak(); // Stop in debugger, ignore in production.
            }
        }

        /// <summary>
        /// Wraps calling the OnInitialize() method that derived classes use to provide common exception handling.
        /// </summary>
        private void ActionOnInitialize(IMessengerConfiguration configuration)
        {
            try
            {
                OnInitialize(configuration);

                if (!Log.SilentMode)
                    Log.Write(LogMessageSeverity.Verbose, LogCategory, "Initialized " + Name + " Messenger", "{0}\r\n{1}\r\n\r\nConfiguration:\r\n{2}", Caption, Description, configuration);

                //and since we've never written anything, act like we just flushed.
                if (AutoFlush)
                    UpdateNextFlushDue();
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                // But we do want init failures to propagate up and cause it to stop trying, so re-throw in production.
                throw;
            }
        }


        /// <summary>
        /// Wraps calling the OnMaintenance() method that derived classes use to provide common exception handling.
        /// </summary>
        private void ActionOnMaintenance()
        {
            try
            {
                OnMaintenance();
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                Log.DebugBreak(); // Stop in debugger, ignore in production.
            }
        }

        /// <summary>
        /// Wraps calling the OnOverflow() method that derived classes use to provide common exception handling.
        /// </summary>
        private void ActionOnOverflow()
        {
            try
            {
                OnOverflow();
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                Log.DebugBreak(); // Stop in debugger, ignore in production.
            }
        }

        /// <summary>
        /// Wraps calling the OnOverflowRestore() method that derived classes use to provide common exception handling.
        /// </summary>
        private void ActionOnOverflowRestore()
        {
            try
            {
                OnOverflowRestore();
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                Log.DebugBreak(); // Stop in debugger, ignore in production.
            }
        }

        /// <summary>
        /// Wraps calling the OnWrite() method that derived classes use to provide common exception handling.
        /// </summary>
        private void ActionOnWrite(IMessengerPacket packet, bool writeThrough, ref MaintenanceModeRequest maintenanceRequested)
        {
            try
            {
                OnWrite(packet, writeThrough, ref maintenanceRequested);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                Log.DebugBreak(); // Stop in debugger, ignore in production.
            }
        }

        /// <summary>
        /// Sets the next flush due date &amp; time based on the current time and the auto flush interval.
        /// </summary>
        private void UpdateNextFlushDue()
        {
            m_NextFlushDue = DateTime.Now.AddSeconds(AutoFlushInterval);
        }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Specifies whether maintenance mode has been requested and the type (source) of that request.
        /// </summary>
        protected enum MaintenanceModeRequest
        {
            /// <summary>
            /// No maintenance is being requested.
            /// </summary>
            None,

            /// <summary>
            /// Maintenance has been triggered by size or time thresholds.
            /// </summary>
            Regular,

            /// <summary>
            /// Maintenance has been explicitly requested by the client.
            /// </summary>
            Explicit,
        }


        /// <summary>
        /// Indicates whether the messenger base should automatically flush the derived messenger.
        /// </summary>
        /// <remarks>When true the derived messenger will automatically be asked to flush based on the auto flush interval.
        /// If the messenger is manually flushed due to an external flush request the auto-flush will take that into account
        /// and keep waiting.</remarks>
        protected bool AutoFlush { get; set; }

        /// <summary>
        /// The number of seconds since the last flush to trigger an automatic flush
        /// </summary>
        /// <remarks>This only applies when AutoFlush is true.</remarks>
        protected int AutoFlushInterval { get; set; }

        /// <summary>
        /// The publisher that created this messenger.
        /// </summary>
        /// <remarks>This property does not require any locks.</remarks>
        protected Publisher Publisher { get { return m_Publisher; } }

        /// <summary>
        /// True once the messenger is being closed or application is exiting, false otherwise
        /// </summary>
        /// <remarks>This property is not thread-safe; to guarantee thread safety callers should have the Queue Lock.
        /// This property indicates a normal application exit condition.</remarks>
        protected bool Exiting { get { return m_Exiting; } }

        /// <summary>
        /// True once the messenger is ready for the application to exit, false otherwise
        /// </summary>
        /// <remarks>This property is not thread-safe; to guarantee thread safety callers should have the Queue Lock.</remarks>
        protected bool Exited { get { return m_Exited; } }

        /// <summary>
        /// True once the messenger has been closed, false otherwise
        /// </summary>
        /// <remarks>This property is not thread-safe; to guarantee thread safety callers should have the Queue Lock.</remarks>
        protected bool Closed { get { return m_Closed; } }

        /// <summary>
        /// True if the messenger has been initialized.
        /// </summary>
        /// <remarks>This property is not thread-safe; to guarantee thread safety callers should have the Thread Lock.</remarks>
        protected bool Initialized { get { return m_Initialized; } }

        /// <summary>
        /// True if the current configuration forces write through mode.
        /// </summary>
        /// <remarks>This property is not thread-safe; to guarantee thread safety callers should have the Thread Lock.</remarks>
        protected bool ForceWriteThrough { get { return m_ForceWriteThrough; } }

        /// <summary>
        /// Synchronization object for the message queue.
        /// </summary>
        /// <remarks>In general it should not be necessary to do your own locking provided that you work within the locks provided
        /// by overrideable methods.  If you get an object lock, you must use the Monitor.Pulse command to notify other threads 
        /// that you are done with the lock.  Failure to do so may cause your messenger to be unresponsive.</remarks>
        protected object QueueLock { get { return m_MessageQueueLock; } }

        /// <summary>
        /// Synchronization object for the dispatch thread.
        /// </summary>
        /// <remarks>In general it should not be necessary to do your own locking provided that you work within the locks provided
        /// by overrideable methods.  If you get an object lock, you must use the Monitor.Pulse command to notify other threads 
        /// that you are done with the lock.  Failure to do so may cause your messenger to be unresponsive.</remarks>
        protected object ThreadLock { get { return m_MessageDispatchThreadLock; } }

        /// <summary>
        /// The behavior of the messenger when there are too many messages in the queue
        /// </summary>
        /// <remarks>
        /// Changes take effect the next time a message is published to the messenger.
        /// </remarks>
        protected OverflowMode OverflowMode { get; set; }

        /// <summary>
        /// Perform first-time initialization.  Requires the caller have the Thread Lock.
        /// </summary>
        protected void EnsureInitialized()
        {
            if (m_Initialized == false)
            {
                //set the values that are from our configuration
                m_Caption = m_Name;
                m_ForceWriteThrough = m_Configuration.ForceSynchronous;
                m_MessageQueueMaxLength = m_Configuration.MaxQueueLength;

                //then let our inheritors know so they can have their way with things.
                try
                {
                    // Note: Is this silly?  ActionOnInitialize() already does its own try/catch around OnInitialize()
                    // This try/catch seems to be redundant and pointless since we just re-throw it here anyway.
                    ActionOnInitialize(m_Configuration);
                }
                catch (Exception ex)
                {
                    GC.KeepAlive(ex);
                    // But we do want init failures to propagate up and cause it to stop trying, so re-throw in production.
                    throw;
                }

                //and now we're initialized
                m_Initialized = true;
            }
        }

        /// <summary>
        /// Makes sure that there is an active, valid queue dispatching thread
        /// </summary>
        /// <remarks>This is a thread-safe method that acquires the message dispatch thread lock on its own, so
        /// the caller need not have that lock prior to calling this method.  If the message dispatch thread has
        /// failed a new one will be started.</remarks>
        protected void EnsureMessageDispatchThreadIsValid()
        {
            //see if for some mystical reason our message dispatch thread failed.
            if (m_MessageDispatchThreadFailed)
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

        /// <summary>
        /// Throws an exception if the messenger is closed.  Useful on guarding public methods
        /// </summary>
        /// <remarks>This method does not check that the messenger is initialized (since that happens on the messenger's internal thread)</remarks>
        /// <param name="caller">The string name of the calling method to attribute the exception to.</param>
        protected void EnsureOpen(string caller)
        {
            if (m_Closed)
            {
                throw new ArgumentException("The messenger has been closed, so " + caller + " can no longer be called.");
            }
        }


        /// <summary>
        /// Perform the actual package queuing and wait for it to be committed.  
        /// </summary>
        /// <remarks>This will be done within a Queue Lock.</remarks>
        /// <param name="packet">The packet to be queued</param>
        /// <param name="writeThrough">True if the call should block the current thread until the packet has been committed, false otherwise.</param>
        /// <returns>The packet envelope for the packet that was queued.</returns>
        protected PacketEnvelope QueuePacket(IMessengerPacket packet, bool writeThrough)
        {
            lock (m_MessageQueueLock)
            {
                //wrap it in a packet envelope and indicate if we're in write through mode.
                PacketEnvelope packetEnvelope = new PacketEnvelope(packet, writeThrough);

                // First, a little hack.  If we're queuing a flush command, reset our auto-flush time (if applicable).
                // Since PacketEnvelope already checks for a CommandPacket, use that to shortcut around this check normally.
                // Also check for an ExitMode or CloseMessenger command, to mark that one is pending (suppress maintenance).
                if (packetEnvelope.IsCommand)
                {
                    CommandPacket commandPacket = (CommandPacket)packet; // safe if IsCommand
                    MessagingCommand command = commandPacket.Command;
                    if (command == MessagingCommand.Flush)
                    {
                        // Aha!  We have a triggered flush coming down the pipe,
                        // so we don't really need to do an autoflush in the mean time, if it's enabled.
                        if (AutoFlush)
                        {
                            UpdateNextFlushDue(); // Put off the next auto-flush
                        }
                    }
                    else if (command == MessagingCommand.ExitMode || command == MessagingCommand.CloseMessenger)
                    {
                        // Once we receive an exit or close-messenger command packet, we want to suppress maintenance operations.
                        m_Exiting = true; // An operation is pending which makes deferrable maintenance unimportant.
                    }
                }

                //Now, which queue do we put the packet in?

                //We're in overflow if we are NOT in maintenance mode and either there are items in the overflow queue or we reached our max length.
                //Maintenance mode trumps 
                if ((m_MaintenanceMode == false) && ((m_MessageOverflowQueue.Count > 0) || (m_MessageQueue.Count > m_MessageQueueMaxLength)))
                {
                    //we are in an overflow scenario - what should we do?
                    if (OverflowMode == OverflowMode.Drop)
                    {
                        //bah, we aren't supposed to record it.
                        m_DroppedPackets++;
                        packetEnvelope.IsCommitted = true; //because it never will be written.  Just in case anyone else is checking that.

#if DEBUG
                        Log.Write(LogMessageSeverity.Warning, LogCategory, "Messenger Overflow: Dropping Packet", "The current messenger is set to drop packets instead of overflowing and has a full queue so this and subsequent packets will be dropped until it can get back on its feet\r\n{0}", m_Name);
#endif
                    }
                    else
                    {
                        //we are currently using the overflow queue, put it there.
                        m_MessageOverflowQueue.Enqueue(packetEnvelope);

                        //and set that it's pending so our caller knows they need to wait for it.
                        packetEnvelope.IsPending = true;

                        //do we need to call our On Overflow override?
                        if (m_InOverflowMode == false)
                        {
                            m_InOverflowMode = true;

                            //yep, that was the first.
                            ActionOnOverflow();
                        }
                    }
                }
                else
                {
                    //just queue the packet, we don't want to wait.
                    m_MessageQueue.Enqueue(packetEnvelope);

                    if (m_InOverflowMode)
                    {
                        m_InOverflowMode = false;

                        //we need to let them know that we're no longer in overflow.
                        ActionOnOverflowRestore();
                    }
                }

                return packetEnvelope;
            }
        }

        /// <summary>
        /// Suspends the calling thread until the provided packet is committed.
        /// </summary>
        /// <remarks>Even if the envelope is not set to write through the method will not return until
        /// the packet has been committed.  This method performs its own synchronization and should not be done within a lock.</remarks>
        /// <param name="packetEnvelope">The packet that must be committed</param>
        protected static void WaitOnPacket(PacketEnvelope packetEnvelope)
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
        protected static void WaitOnPending(PacketEnvelope packetEnvelope)
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

        /// <summary>
        /// Inheritors should override this method to implement custom Command handling functionality
        /// </summary>
        /// <remarks>Code in this method is protected by a Queue Lock.
        /// This method is called with the Message Dispatch thread exclusively.
        /// Some commands (Shutdown, Flush) are handled by MessengerBase and redirected into specific
        /// method calls.</remarks>
        /// <param name="command">The MessagingCommand enum value of this command.</param>
        /// <param name="state">Optional.  Command arguments</param>
        /// <param name="writeThrough">Whether write-through (synchronous) behavior was requested.</param>
        /// <param name="maintenanceRequested">Specifies whether maintenance mode has been requested and the type (source) of that request.</param>
        protected virtual void OnCommand(MessagingCommand command, object state, bool writeThrough, ref MaintenanceModeRequest maintenanceRequested)
        {
            //we do nothing by default
        }

        /// <summary>
        /// Inheritors should override this method to implement custom Exit functionality
        /// </summary>
        /// <remarks>Code in this method is protected by a Queue Lock.
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected virtual void OnExit()
        {
            //we do nothing by default
        }

        /// <summary>
        /// Inheritors should override this method to implement custom Close functionality
        /// </summary>
        /// <remarks>Code in this method is protected by a Queue Lock.
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected virtual void OnClose()
        {
            //we do nothing by default
        }

        /// <summary>
        /// Inheritors should override this method to implement custom Configuration Update functionality.
        /// </summary>
        /// <remarks>Code in this method is protected by a Thread Lock.
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected virtual void OnConfigurationUpdate(IMessengerConfiguration configuration)
        {
            //we do nothing by default
        }

        /// <summary>
        /// Inheritors should override this method to implement custom flush functionality.
        /// </summary>
        /// <param name="maintenanceRequested">Specifies whether maintenance mode has been requested and the type (source) of that request.</param>
        /// <remarks>Code in this method is protected by a Queue Lock.        
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected virtual void OnFlush(ref MaintenanceModeRequest maintenanceRequested)
        {
            //we do nothing by default
        }

        /// <summary>
        /// Inheritors should override this method to implement custom initialize functionality.
        /// </summary>
        /// <remarks>This method will be called exactly once before any call to OnFlush or OnWrite is made.  
        /// Code in this method is protected by a Thread Lock.
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected virtual void OnInitialize(IMessengerConfiguration configuration)
        {
            //we do nothing by default
        }

        /// <summary>
        /// Inheritors should override this to implement a periodic maintenance capability
        /// </summary>
        /// <remarks>Maintenance is invoked by a return value from the OnWrite method.  When invoked,
        /// this method is called and all log messages are buffered for the duration of the maintenance period.
        /// Once this method completes, normal log writing will resume.  During maintenance, any queue size limit is ignored.
        /// This method is not called with any active locks to allow messages to continue to queue during maintenance.  
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected virtual void OnMaintenance()
        {
            //we do nothing by default
        }

        /// <summary>
        /// Inheritors can override this method to implement custom functionality when the main queue over flows.
        /// </summary>
        /// <remarks>This method is called when the first packet is placed in the overflow queue.  It will not be
        /// called again unless there is a call to OnOverflowRestore, indicating that the overflow has been resolved.
        /// Code in this method is protected by a Queue Lock.
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected virtual void OnOverflow()
        {
            //we do nothing by default
        }

        /// <summary>
        /// Inheritors can override this method to implement custom functionality when the main queue is no longer in overflow.
        /// </summary>
        /// <remarks>This method is called when there are no more packets in the overflow queue  It will not be
        /// called again unless there is a call to OnOverflowRestore, indicating that the overflow has been resolved.
        /// Code in this method is protected by a Queue Lock.
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected virtual void OnOverflowRestore()
        {
            //we do nothing by default
        }


        /// <summary>
        /// Inheritors must override this method to implement their custom message writing functionality.
        /// </summary>
        /// <remarks>Code in this method is protected by a Queue Lock
        /// This method is called with the Message Dispatch thread exclusively.</remarks>
        protected abstract void OnWrite(IMessengerPacket packet, bool writeThrough, ref MaintenanceModeRequest maintenanceRequested);

        #endregion

        #region Public Properties and Methods

        /// <summary>
        /// A display caption for this messenger
        /// </summary>
        /// <remarks>End-user display caption for this messenger.  Captions are typically
        /// not unique to a given instance of a messenger.</remarks>
        public string Caption
        {
            get { return m_Caption; }
            protected set { m_Caption = value; }
        }

        /// <summary>
        /// A display description for this messenger
        /// </summary>
        /// <remarks></remarks>
        public string Description
        {
            get { return m_Description; }
            protected set { m_Description = value; }
        }

        /// <summary>
        /// Called by the publisher every time the configuration has been updated.
        /// </summary>
        /// <param name="configuration">The unique name for this messenger.</param>
        public void ConfigurationUpdated(IMessengerConfiguration configuration)
        {
            EnsureOpen("ConfigurationUpdated");

            //we only process this event if we've already initialized.
            lock (m_MessageDispatchThreadLock)
            {
                //we need to go get a new configuration object for ourself.
                m_Configuration = configuration;

                if (m_Initialized)
                {
                    ActionOnConfigurationUpdate(m_Configuration);
                }

                System.Threading.Monitor.PulseAll(m_MessageDispatchThreadLock);
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
                    // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                    // Other objects may be referenced in this case

                    // We need to stall until we are shut down.
                    if (m_Closed == false)
                    {
                        // We need to create and queue a CloseMessenger command, and block until its done.
                        Write(new CommandPacket(MessagingCommand.CloseMessenger), true);
                    }
                }
                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here

                m_Disposed = true; // Make sure we only do this once
            }
        }

        ///<summary>
        ///Indicates whether the current object is equal to another object of the same type.
        ///</summary>
        ///<returns>
        ///true if the current object is equal to the other parameter; otherwise, false.
        ///</returns>
        ///<param name="other">An object to compare with this object.</param>
        public bool Equals(IMessenger other)
        {
            //Careful - can be null
            if (other == null)
            {
                return false; // since we're a live object we can't be equal.
            }

            //compare based on name
            return m_Name.Equals(other.Name);
        }

        /// <summary>
        /// Initialize the messenger so it is ready to accept packets.
        /// </summary>
        /// <param name="publisher">The publisher that owns the messenger</param>
        /// <param name="configuration">The unique name for this messenger.</param>
        public void Initialize(Publisher publisher, IMessengerConfiguration configuration)
        {
            EnsureOpen("Initialize");

            m_Publisher = publisher;
            m_Configuration = configuration;

            //create the thread we use for dispatching messages
            CreateMessageDispatchThread();
        }

        /// <summary>
        /// A name for this messenger
        /// </summary>
        /// <remarks>The name is unique and specified by the publisher during initialization.</remarks>
        public string Name { get { return m_Name; } }

        /// <summary>
        /// Write the provided packet to this messenger.
        /// </summary>
        /// <remarks>The packet may depend on other packets.  If the messenger needs those packets they are available from the publisher's packet cache.</remarks>
        /// <param name="packet">The packet to write through the messenger.</param>
        /// <param name="writeThrough">True if the information contained in packet should be committed synchronously, false if the messenger should use write caching (if available).</param>
        public void Write(IMessengerPacket packet, bool writeThrough)
        {
            if (packet == null)
                return; //just rapid bail.  don't bother with the lock.

            //EnsureOpen("Write"); // Don't throw an exception; if we're closed just return (below).

            PacketEnvelope packetEnvelope;

            bool effectiveWriteThrough;
            bool isPending;

            //get the queue lock
            lock (m_MessageQueueLock)
            {
                //now that we're in the exclusive lock, check to see if we're actually closed.
                if (m_Closed)
                    return; //it wouldn't get logged anyway.

                // Check whether this should writeThrough, either by request, by configuration, or because we have
                // received an ExitMode or CloseMessenger command (pending) and need to flush after each packet.
                effectiveWriteThrough = m_SupportsWriteThrough && (m_ForceWriteThrough || writeThrough || m_Exiting);

                //and queue the packet.
                packetEnvelope = QueuePacket(packet, effectiveWriteThrough);

                //grab the pending flag before we release the lock so we know we have a consistent view.
                isPending = packetEnvelope.IsPending;

                //now signal our next thread that might be waiting that the lock will be released.
                System.Threading.Monitor.PulseAll(m_MessageQueueLock);
            }

            //make sure our dispatch thread is still going.  This has its own independent locking (when necessary),
            //so we don't need to hold up other threads that are publishing.
            EnsureMessageDispatchThreadIsValid();

            //See if we need to wait because we've degraded to synchronous message handling due to a backlog of messages
            if (isPending)
            {
                //this routine does its own locking so we don't need to interfere with the nominal case of 
                //not needing to pend.
                WaitOnPending(packetEnvelope);
            }

            //Finally, if we need to wait on the write to complete now we want to stall.  We had to do this outside of
            //the message queue lock to ensure we don't block other threads.
            if (effectiveWriteThrough)
            {
                WaitOnPacket(packetEnvelope);
            }
        }

        #endregion
    }

    /// <summary>
    /// The behavior of the messenger when there are too many messages in the queue.
    /// </summary>
    public enum OverflowMode
    {
        /// <summary>
        /// Do the default overflow behavior (OverflowQueueThenBlock)
        /// </summary>
        Default,

        /// <summary>
        /// Use the overflow queue then block if there are too many messages
        /// </summary>
        OverflowQueueThenBlock,

        /// <summary>
        /// Drop the newest messages instead of using the overflow queue
        /// </summary>
        Drop
    }
}
