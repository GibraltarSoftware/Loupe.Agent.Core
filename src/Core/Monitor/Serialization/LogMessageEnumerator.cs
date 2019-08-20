using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Loupe.Data;
using Loupe.Serialization;
using Loupe.Extensibility.Data;
using Loupe.Logging;

#pragma warning disable 1591

namespace Loupe.Monitor.Serialization
{
    public class LogMessageEnumerator : IEnumerator<LogMessage>
    {
        private readonly Session m_Session;
        private readonly List<GLFReader> m_AvailableReaders;
        private readonly List<GLFReader> m_UnloadedReaders;
        private readonly ThreadInfoCollection m_Threads;
        private readonly ApplicationUserCollection m_Users;

        private bool m_HasCorruptData;
        private int m_PacketsLostCount;
        private long m_LastSequence;
        private PacketReader m_PacketReader;
        private PacketManager m_PacketManager;
        private bool m_IsDisposed;

        public LogMessageEnumerator(Session session, ThreadInfoCollection threads, ApplicationUserCollection users, List<GLFReader> availableReaders)
        {
            m_Session = session;
            m_AvailableReaders = availableReaders;
            m_UnloadedReaders = new List<GLFReader>();
            m_Threads = threads;
            m_Users = users;

            Reset();
        }

        public bool MoveNext()
        {
            Current = null;

            Stream stream;
            int packetCount = 0;

            //we have to move cleanly through all of the available streams
            while (m_PacketManager != null)
            {
                //we loop until we find a good packet
                while ((stream = m_PacketManager.GetNextPacket()) != null)
                {
                    IPacket nextPacket;
                    try
                    {
                        nextPacket = m_PacketReader.ReadPacket(stream);
                        packetCount++;
                    }
                    catch (Exception ex)
                    {
                        m_HasCorruptData = true;
                        m_PacketsLostCount = PacketsLostCount + 1;

                        var serializationException = ex as LoupeSerializationException;
                        if ((serializationException != null) //and really this should always be the case...
                            && (serializationException.StreamFailed))
                        {
                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, Session.LogCategory,
                                          "Exception during packet stream read, unable to continue deserializing data",
                                          "While attempting to deserialize packet number {0} an exception was reported. This has failed the stream so serialization will stop.\r\nException: {1}",
                                          packetCount, ex.Message);
                            return false;
                        }
                        else
                        {
                            if (!Log.SilentMode)
                                Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, Session.LogCategory, "Exception during packet read, discarding packet and continuing",
                                          "While attempting to deserialize packet number {0} an exception was reported.  Since this may be a problem with just this one packet, we'll continue deserializing.\r\nException: {1}", packetCount, ex.Message);
                            continue; //See that?  We're NOT breaking out of the while loop; just move on to the next packet!                        
                        }
                    }

                    if (nextPacket != null)
                    {
                        try
                        {
                            LogMessagePacket logMessagePacket;
                            ApplicationUserPacket applicationUserPacket;
                            ThreadInfoPacket threadInfoPacket;

                            if ((logMessagePacket = nextPacket as LogMessagePacket) != null) //this covers trace packet too
                            {
                                // The new serialized version of LMP adds a ThreadIndex field.  Older versions will set this
                                // field from the ThreadId field, so we can rely on ThreadIndex in any case.
                                if (m_Threads.TryGetValue(logMessagePacket.ThreadIndex, out var threadInfo) == false)
                                {
                                    // Uh-oh.  This should never happen.
                                    threadInfo = null; // We couldn't find the ThreadInfo for this log message!
                                }

                                ApplicationUser applicationUser = null;
                                if (string.IsNullOrEmpty(logMessagePacket.UserName) == false)
                                    m_Users.TryFindUserName(logMessagePacket.UserName, out applicationUser);

                                var logMessage = new LogMessage(m_Session, threadInfo, applicationUser, logMessagePacket);

                                //COOL! This is what we want.
                                Current = logMessage;
                            }
                            else if ((applicationUserPacket = nextPacket as ApplicationUserPacket) != null)
                            {
                                var applicationUser = new ApplicationUser(applicationUserPacket);
                                m_Users.TrySetValue(applicationUser);
                            }
                            else if ((threadInfoPacket = nextPacket as ThreadInfoPacket) != null)
                            {
                                var threadInfo = new ThreadInfo(threadInfoPacket);
                                if (m_Threads.Contains(threadInfo) == false)
                                {
                                    m_Threads.Add(threadInfo);
                                }
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
                                Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, Session.LogCategory, "Unexpected exception while loading packet stream", "{0}", ex.Message);
#endif
                        }
                    }

                    //if we found a log message then we've done our job as an iterator.
                    if (Current != null)
                        return true;
                }

                MoveNextReader();
            }

            return false;
        }

        public void Reset()
        {
            Current = null;
            m_UnloadedReaders.Clear();
            m_UnloadedReaders.AddRange(m_AvailableReaders);
            MoveNextReader();
        }

        public LogMessage Current { get; private set; }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public bool HasCorruptData { get { return m_HasCorruptData; } }

        public int PacketsLostCount { get { return m_PacketsLostCount; } }

        #region IDisposable Members

        ///<summary>
        ///Performs application-defined tasks associated with freeing, releasing, or resetting managed resources.
        ///</summary>
        ///<filterpriority>2</filterpriority>
        public void Dispose()
        {
            // Call the underlying implementation
            Dispose(true);

            // and SuppressFinalize because there won't be anything left to finalize
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
            if (!m_IsDisposed)
            {
                m_IsDisposed = true; // Only Dispose stuff once

                if (releaseManaged)
                {
                    // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                    m_PacketManager = null;
                    m_PacketReader = null;
                    m_AvailableReaders.Clear();
                    m_UnloadedReaders.Clear();
                }
            }
        }

        #endregion
        private void MoveNextReader()
        {
            m_PacketManager = null;
            m_PacketReader = null;

            if (m_UnloadedReaders.Count > 0)
            {
                var glfReader = m_UnloadedReaders[0];
                try
                {
                    //this is best effort- add this stream to our list of loaded streams BEFORE we try to process it
                    Stream packetStream = glfReader.GetPacketStreamStart(); // Resets stream to start of packet data.

                    m_PacketManager = new PacketManager(packetStream);
                    m_PacketReader = new PacketReader(null, true, glfReader.MajorVersion, glfReader.MinorVersion);

                    //register our packet factories we need
                    var sessionPacketFactory = new SessionPacketFactory();
                    sessionPacketFactory.Register(m_PacketReader);

                    var logMessagePacketFactory = new LogMessagePacketFactory(m_Session);
                    logMessagePacketFactory.Register(m_PacketReader);

                    //var analysisPacketFactory = new AnalysisPacketFactory();
                    //analysisPacketFactory.Register(m_PacketReader);

                    //var metricPacketFactory = new MetricPacketFactory(m_Session);
                    //metricPacketFactory.Register(m_PacketReader);

                    //var metricDefinitionPacketFactory = new MetricDefinitionPacketFactory(m_Session);
                    //metricDefinitionPacketFactory.Register(m_PacketReader);

                    //var metricSamplePacketFactory = new MetricSamplePacketFactory(m_Session);
                    //metricSamplePacketFactory.Register(m_PacketReader);
                }
                catch (Exception ex)
                {
                    if (!Log.SilentMode)
                        Log.Write(LogMessageSeverity.Warning, LogWriteMode.WaitForCommit, ex, Session.LogCategory, "Exception loading packet stream.", "{0}: {1}", ex.GetType(), ex.Message);
                    m_PacketsLostCount++; // there may be multiple packets lost, but there will be at least one;
                }

                m_UnloadedReaders.Remove(glfReader); //we've now loaded it.
            }
        }
    }
}
