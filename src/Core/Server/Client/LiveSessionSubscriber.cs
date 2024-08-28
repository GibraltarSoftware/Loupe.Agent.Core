#region File Header
// /********************************************************************
//  * COPYRIGHT:
//  *    This software program is furnished to the user under license
//  *    by Gibraltar Software Inc, and use thereof is subject to applicable 
//  *    U.S. and international law. This software program may not be 
//  *    reproduced, transmitted, or disclosed to third parties, in 
//  *    whole or in part, in any form or by any manner, electronic or
//  *    mechanical, without the express written consent of Gibraltar Software Inc,
//  *    except to the extent provided for by applicable license.
//  *
//  *    Copyright © 2008 - 2015 by Gibraltar Software, Inc.  
//  *    All rights reserved.
//  *******************************************************************/
#endregion
#region File Header

// /********************************************************************
//  * COPYRIGHT:
//  *    This software program is furnished to the user under license
//  *    by Gibraltar Software, Inc, and use thereof is subject to applicable 
//  *    U.S. and international law. This software program may not be 
//  *    reproduced, transmitted, or disclosed to third parties, in 
//  *    whole or in part, in any form or by any manner, electronic or
//  *    mechanical, without the express written consent of Gibraltar Software, Inc,
//  *    except to the extent provided for by applicable license.
//  *
//  *    Copyright © 2008 by Gibraltar Software, Inc.  All rights reserved.
//  *******************************************************************/
using System;
using Gibraltar.Data;
using Gibraltar.Messaging.Net;
using Gibraltar.Monitor;
using Gibraltar.Monitor.Serialization;
using Gibraltar.Serialization;
using Loupe.Extensibility.Data;

#endregion

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Used by viewers to read sessions from a TCP socket
    /// </summary>
    public class LiveSessionSubscriber : NetworkClient
    {
        private readonly ISessionPacketCache m_PacketCache;
        private readonly Guid m_SessionId;

        private readonly object m_Lock = new object();

        private SessionSummary m_SessionSummary;
        private SessionClosePacket m_EndInfo;
        private long m_LastSequence;
        private TimeSpan m_ClockDrift;
        private readonly Guid m_RepositoryId;
        private readonly Guid m_ChannelId;

        /// <summary>
        /// Raised when a new message is available from the server
        /// </summary>
        public event MessageAvailableEventHandler MessageAvailable;

        /// <summary>
        /// Create a new network reader for the specified session on the designated server
        /// </summary>
        /// <param name="options">The definition of the network endpoint</param>
        /// <param name="repositoryId">The unique Id of the client for all related activities</param>
        /// <param name="sessionId">The session to be subscribed to</param>
        /// <param name="channelId">A unique id assigned by the original requester for this conversation</param>
        /// <param name="lastSequence">The sequence number of the last message that was received on the connection</param>
        /// <param name="packetCache"></param>
        public LiveSessionSubscriber(NetworkConnectionOptions options, Guid repositoryId, Guid sessionId, Guid channelId, long lastSequence, ISessionPacketCache packetCache)
            : this(options, repositoryId, sessionId, channelId, lastSequence, FileHeader.DefaultMajorVersion, FileHeader.DefaultMajorVersion, packetCache)
        {
        }

        /// <summary>
        /// Create a new network reader for the specified session on the designated server
        /// </summary>
        /// <param name="options">The definition of the network endpoint</param>
        /// <param name="repositoryId">The unique Id of the client for all related activities</param>
        /// <param name="sessionId">The session to be subscribed to</param>
        /// <param name="channelId">A unique id assigned by the original requester for this conversation</param>
        /// <param name="lastSequence">The sequence number of the last message that was received on the connection</param>
        /// <param name="majorVersion">Major version of the serialization protocol</param>
        /// <param name="minorVersion">Minor version of the serialization protocol</param>
        /// <param name="packetCache"></param>
        public LiveSessionSubscriber(NetworkConnectionOptions options, Guid repositoryId, Guid sessionId, Guid channelId, 
                                     long lastSequence, int majorVersion, int minorVersion, ISessionPacketCache packetCache)
            : base(options, false, majorVersion, minorVersion) //we don't retry connects at this level.
        {
            m_PacketCache = packetCache;

            lock(m_Lock) //since we promptly access these variables from another thread, I'm adding this as paranoia to ensure they get synchronized.
            {
                m_RepositoryId = repositoryId;
                m_ChannelId = channelId;
                m_SessionId = sessionId;
                m_LastSequence = lastSequence;
            }
        }

        /// <summary>
        /// The unique id of the end-to-end relationship
        /// </summary>
        public Guid ChannelId => m_ChannelId;

        /// <summary>
        /// The session id
        /// </summary>
        public Guid SessionId => m_SessionId;

        /// <summary>
        /// The summary properties for this session.
        /// </summary>
        public SessionSummary Summary => m_SessionSummary;

        /// <summary>
        /// The last sequence number in the session data.
        /// </summary>
        public long LastSequence => m_LastSequence;

        /// <summary>
        /// Send a request to the agent to submit its session data to the server
        /// </summary>
        /// <param name="criteria"></param>
        public void SendToServer(SessionCriteria criteria)
        {
            SendSessionCommandMessage command = new SendSessionCommandMessage(m_SessionId, criteria);
            SendMessage(command);
        }

        /// <summary>
        /// The current clock drift between the remote computer and this computer on this session
        /// </summary>
        public TimeSpan ClockDrift { get => m_ClockDrift; set => m_ClockDrift = value; }

        #region Protected Properties and Methods

        /// <summary>
        /// Implemented to complete the protocol connection
        /// </summary>
        /// <returns>True if a connection was successfully established, false otherwise.</returns>
        protected override bool Connect()
        {
            bool connnected = false;

            //before we can jump in and actually read data we need to send a start packet.
            LiveViewStartCommandMessage startCommandPacket = new LiveViewStartCommandMessage(m_RepositoryId, m_SessionId, m_ChannelId, LastSequence);
            startCommandPacket.Validate(); //we've been having trouble...
            SendMessage(startCommandPacket);

            NetworkMessage nextPacket;
            do
            {
                nextPacket = ReadNetworkMessage();
                if (nextPacket != null)
                {
                    if (nextPacket is LiveViewStopCommandMessage)
                    {
                        //we are going to shut down the connection
                        RemoteClose();
                    }
                    else if (nextPacket is PacketStreamStartCommandMessage)
                    {
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Information, LogCategory, "Packet Stream start command received from server", "Received the start command, now we will switch over to the Loupe session stream data.\r\n{0}", this);
                        connnected = true;
                    }
                }
            } while ((nextPacket != null) && (connnected == false));

            return connnected;
        }

        /// <summary>
        /// Implemented to transfer data on an established connection
        /// </summary>
        protected override void TransferData()
        {
            bool threadAdded = false;
            IPacket nextPacket;
            IPacket previousPacket; //just for debugging...
            do
            {
                nextPacket = ReadSerializedPacket();

                if (nextPacket != null)
                {
                    try
                    {
                        SessionSummaryPacket sessionSummaryPacket;
                        SessionClosePacket sessionClosePacket;
                        ThreadInfoPacket threadInfoPacket;
                        ApplicationUserPacket applicationUserPacket;
                        LogMessagePacket logMessagePacket;

                        if ((logMessagePacket = nextPacket as LogMessagePacket) != null) //this covers trace packet too
                        {
                            // The new serialized version of LMP adds a ThreadIndex field.  Older versions will set this
                            // field from the ThreadId field, so we can rely on ThreadIndex in any case.
                            ThreadInfo threadInfo;
                            if (m_PacketCache.Threads.TryGetValue(logMessagePacket.ThreadIndex, out threadInfo))
                            {
                                logMessagePacket.ThreadInfoPacket = threadInfo.Packet;
                            }

                            EnqueueMessage(logMessagePacket);
                        }
                        else if ((applicationUserPacket = nextPacket as ApplicationUserPacket) != null)
                        {
                            AddUser(applicationUserPacket);
                        }
                        else if ((threadInfoPacket = nextPacket as ThreadInfoPacket) != null)
                        {
                            AddThread(threadInfoPacket);
                            threadAdded = true;
                        }
                        else if ((sessionSummaryPacket = nextPacket as SessionSummaryPacket) != null)
                        {
                            SetSessionSummary(sessionSummaryPacket);
                        }
                        else if ((sessionClosePacket = nextPacket as SessionClosePacket) != null)
                        {
                            SetSessionClose(sessionClosePacket);
                        }

                        //if this is a sequence #'d packet (and they really all should be) then we need to check if it's our new top sequence.
                        GibraltarPacket gibraltarPacket;
                        if ((gibraltarPacket = nextPacket as GibraltarPacket) != null)
                        {
                            m_LastSequence = Math.Max(m_LastSequence, gibraltarPacket.Sequence);
                        }
                    }
                    catch (Exception ex)
                    {
                        GC.KeepAlive(ex);
#if DEBUG
                        throw;
#else
                        if (!Log.SilentMode)
                            Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Unexpected exception while loading packet stream", "{0}", ex.Message);
#endif
                    }
                }

                previousPacket = nextPacket;
                //we only want previous packet for debugging purposes, but if we don't
                //at least try to do something with it then the compiler eliminates it.
                if (previousPacket == null)
                {
                    GC.KeepAlive(previousPacket);
                }

                if (threadAdded)
                {
                    lock(m_Lock)
                    {
                        m_PacketCache.Threads.UniquifyThreadNames(); // And distinguish any thread name (Caption) collisions.               
                    }
                    threadAdded = false;
                }
            } while (nextPacket != null); //when the socket fails we'll get null back...  
        }

        /// <summary>
        /// Called when a valid connection is being administratively closed
        /// </summary>
        protected override void OnClose()
        {
            var message = new LiveViewStopCommandMessage(m_ChannelId, m_SessionId);
            SendMessage(message);
        }

        /// <summary>
        /// Raises the MessageAvailable event.
        /// </summary>
        /// <param name="newMessage"></param>
        protected virtual void OnMessageAvailable(ILogMessage newMessage)
        {
            MessageAvailableEventHandler tempEvent = MessageAvailable;

            if (tempEvent != null)
            {
                try
                {
                    tempEvent.Invoke(this, new MessageAvailableEventArgs(newMessage));
                }
                catch (Exception ex)
                {
                    if (!Log.SilentMode) Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Client exception thrown during message available event", ex.Message);
                }
            }
        }

        /// <summary>
        /// Allows derived classes to register all of the packet factories they need when creating a new packet reader.
        /// </summary>
        /// <param name="packetReader"></param>
        protected override void OnPacketFactoryRegister(PacketReader packetReader)
        {
            SessionPacketFactory sessionPacketFactory = new SessionPacketFactory();
            sessionPacketFactory.Register(packetReader);

            LogMessagePacketFactory logMessagePacketFactory = new LogMessagePacketFactory(m_PacketCache);
            logMessagePacketFactory.Register(packetReader);
        }


        #endregion

        #region Private Properties and Methods

        private void SetSessionClose(SessionClosePacket sessionClosePacket)
        {
            lock(m_Lock)
            {
                if (m_SessionSummary != null && (m_EndInfo == null ?
                    sessionClosePacket.EndingStatus >= m_SessionSummary.Status :
                    sessionClosePacket.EndingStatus > m_SessionSummary.Status))
                {
                    // This can only advance one way, Running -> Normal -> Crashed.
                    m_EndInfo = sessionClosePacket; // Replace any previous one, this packet is a new state.
                    m_SessionSummary.Status = m_EndInfo.EndingStatus;
                }                
            }
        }

        private void SetSessionSummary(SessionSummaryPacket sessionSummaryPacket)
        {
            lock (m_Lock)
            {
                // We only take this packet if we don't have one already.  We should always have one from constructor.
                if (m_SessionSummary == null)
                {
                    m_SessionSummary = new SessionSummary(sessionSummaryPacket);
                    // Session summary was null, so we don't have a record of the session status.
                    // So get the status from the EndInfo packet, if there has been one.
                    if (m_EndInfo != null)
                        m_SessionSummary.Status = m_EndInfo.EndingStatus;

                    //now that we've read up everything, make sure we have a caption on start info.  
                    if (string.IsNullOrEmpty(m_SessionSummary.Caption))
                    {
                        m_SessionSummary.Caption = SessionSummary.DefaultCaption(m_SessionSummary);
                    }
                }
            }
        }

        private void EnqueueMessage(ILogMessage logMessage)
        {
            OnMessageAvailable(logMessage);
        }

        private void AddThread(ThreadInfoPacket threadInfoPacket)
        {
            lock (m_Lock)
            {
                ThreadInfo threadInfo = new ThreadInfo(threadInfoPacket);
                if (m_PacketCache.Threads.Contains(threadInfo) == false)
                {
                    m_PacketCache.Threads.Add(threadInfo);
                }
            }
        }

        private void AddUser(ApplicationUserPacket userPacket)
        {
            var applicationUser = new ApplicationUser(userPacket);
            m_PacketCache.Users.TrySetValue(applicationUser);
        }

        #endregion
    }
}
