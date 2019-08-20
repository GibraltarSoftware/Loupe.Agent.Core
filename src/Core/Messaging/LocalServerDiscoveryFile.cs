using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using Loupe.Core.Data;

namespace Loupe.Core.Messaging
{
    /// <summary>
    /// IP Configuration information for a live stream proxy running on the local computer
    /// </summary>
    public class LocalServerDiscoveryFile
    {
        /// <summary>
        /// The standard file extension for a discovery file
        /// </summary>
        public const string Extension = "gpd";

        /// <summary>
        /// A file matching filter for discovery files
        /// </summary>
        public const string FileFilter = "*." + Extension;

        /// <summary>
        /// Load the specified file as a local server discovery file
        /// </summary>
        /// <param name="fileNamePath">The fully qualified file to load</param>
        public LocalServerDiscoveryFile(string fileNamePath)
        {
            FileNamePath = fileNamePath;
            using (var fileStream = File.Open(FileNamePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.ReadWrite))
            {
                Read(fileStream);
            }
        }

        /// <summary>
        /// Create a new publish file for the local computer
        /// </summary>
        /// <param name="publisher"></param>
        /// <param name="subscriber"></param>
        /// <param name="processId"></param>
        /// <param name="destination"></param>
        public LocalServerDiscoveryFile(int publisher, int subscriber, int processId, FileStream destination)
        {
            PublisherPort = publisher;
            SubscriberPort = subscriber;
            ProcessId = processId;
            Write(destination);
            Read(destination);
        }

        private void Write(FileStream destination)
        {
            byte[] rawData = new byte[12]; //MAGIC VALUE! 3 int32 values...

            int curByteIndex = 0;

            byte[] curValue = BinarySerializer.SerializeValue(ProcessId);
            //we always nibble around so we can ensure big endian/little endian behavior.
            foreach (Byte curByte in curValue)
            {
                rawData[curByteIndex++] = curByte;
            }

            curValue = BinarySerializer.SerializeValue(PublisherPort);
            //we always nibble around so we can ensure big endian/little endian behavior.
            foreach (Byte curByte in curValue)
            {
                rawData[curByteIndex++] = curByte;
            }

            curValue = BinarySerializer.SerializeValue(SubscriberPort);
            //we always nibble around so we can ensure big endian/little endian behavior.
            foreach (Byte curByte in curValue)
            {
                rawData[curByteIndex++] = curByte;
            }

            destination.Position = 0;
            destination.Write(rawData, 0, rawData.Length);
            destination.SetLength(rawData.Length);
        }

        private void Read(FileStream source)
        {
            source.Position = 0;
            BinarySerializer.DeserializeValue(source, out int value);
            ProcessId = value;
            BinarySerializer.DeserializeValue(source, out value);
            PublisherPort = value;
            BinarySerializer.DeserializeValue(source, out value);
            SubscriberPort = value;
        }

        /// <summary>
        /// The TCP port to publish information to (for agents)
        /// </summary>
        public int PublisherPort { get; private set; }

        /// <summary>
        /// The TCP port for subscribers to get information from (for analyst)
        /// </summary>
        public int SubscriberPort { get; private set; }

        /// <summary>
        /// The process Id of the socket proxy host.
        /// </summary>
        public int ProcessId { get; private set; }

        /// <summary>
        /// Indicates if the socket proxy host is still running
        /// </summary>
        public bool IsAlive
        {
            get
            {
                bool isAlive = File.Exists(FileNamePath);

                //OK, but is the process Id valid?
                if (isAlive)
                {
                    try
                    {
                        var process = Process.GetProcessById(ProcessId);
                    }
                    catch (Exception ex)
                    {
                        //any exception but an argument exception we want to catch, but shouldn't assume it means the process isn't running.
                        if (ex is ArgumentException)
                            isAlive = false;
                    }
                }

                if (isAlive)
                {
                    try
                    {
                        var properties = IPGlobalProperties.GetIPGlobalProperties();
                        var activeEndpoints = properties.GetActiveTcpListeners();

                        //now find the one on the port we want..
                        bool portExists = false;
                        foreach (var activeEndpoint in activeEndpoints)
                        {
                            if (IPAddress.IsLoopback(activeEndpoint.Address) && (activeEndpoint.Port == PublisherPort))
                            {
                                portExists = true;
                                break;
                            }
                        }

                        isAlive = portExists;
                    }
                    catch (Exception)
                    {
                        //we don't assume any of these are a failure.
                    }
                }

                if (isAlive == false)
                {
                    //we should try to delete the file; this may be left over from a crashed process..
                    FileHelper.SafeDeleteFile(FileNamePath);
                }

                return isAlive;
            }
        }

        /// <summary>
        /// The fully qualified file name and path for the discovery file
        /// </summary>
        public string FileNamePath { get; private set; }
    }
}
