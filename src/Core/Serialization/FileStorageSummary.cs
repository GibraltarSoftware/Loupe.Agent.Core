using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Loupe.Serialization
{
    /// <summary>
    /// Consolidates storage summary information for all packet types and fragments in a session
    /// </summary>
    public class FileStorageSummary
    {
        /// <summary>
        /// List of records providing storage summary info about each packet type in the session fragments
        /// Note that PacketSize is calculated by scaling the uncompressed packet sizes to their portion of TotalRawFileSize
        /// </summary>
        public readonly List<PacketTypeStorageSummary> PacketList = new List<PacketTypeStorageSummary>();

        /// <summary>
        /// List of records providing storage summary info about each fragment associated with this session.
        /// </summary>
        public readonly List<FragmentStorageSummary> FragmentList = new List<FragmentStorageSummary>();

        /// <summary>
        /// Returns the total number of bytes for all fragments
        /// </summary>
        public long TotalRawFileSize { get; private set; }

        /// <summary>
        /// Returns the total number of bytes for all uncompressed packets
        /// </summary>
        public long TotalPacketSize { get; private set; }

        /// <summary>
        /// Merge data from one session fragment
        /// </summary>
        public void Merge(List<PacketTypeStorageSummary> packetTypes, FragmentStorageSummary fragment)
        {
            FragmentList.Add(fragment);

            foreach (var item in packetTypes)
            {
                bool found = false; 
                foreach (var summary in PacketList)
                {
                    if (string.CompareOrdinal(summary.QualifiedTypeName, item.QualifiedTypeName) == 0)
                    {
                        summary.PacketCount += item.PacketCount;
                        summary.PacketSize += item.PacketSize;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    PacketList.Add(item);
                }
            }
        }

        /// <summary>
        /// Summarize the data about fragments and packet types
        /// </summary>
        public void Summarize()
        {
            PacketList.Sort();
            FragmentList.Sort();

            TotalRawFileSize = 0;
            foreach (var fragment in FragmentList)
                TotalRawFileSize += fragment.FragmentSize;

            TotalPacketSize = 0;
            foreach (var packetType in PacketList)
                TotalPacketSize += packetType.PacketSize;

            // Normalize all the packet sizes to apportion every byte of the raw file sizes to packets.
            var remainingBytes = TotalRawFileSize;
            for (int i = PacketList.Count - 1; i >= 1; i-- )
            {
                var currentPacket = PacketList[i];
                double percentage = (double)currentPacket.PacketSize / TotalPacketSize;
                currentPacket.PacketSize = (long)Math.Round(TotalRawFileSize * percentage);
                remainingBytes -= currentPacket.PacketSize;
            }

            // This last little bit of logic deals with ensuring we don't lose any bytes to round-off error.
            // We apply this to the largest value so the adjustment will be the least significant
            if (PacketList.Count > 0)
                PacketList[0].PacketSize = remainingBytes;
        }
    }

    /// <summary>
    /// Records a summary of size for one specific session fragment
    /// </summary>
    [DebuggerDisplay("Start: {StartTime} End: {EndTime} Total Bytes: {FragmentSize}")]
    public class FragmentStorageSummary : IComparable<FragmentStorageSummary>
    {
        /// <summary>
        /// Start time of fragment
        /// </summary>
        public DateTimeOffset StartTime { get; private set; }

        /// <summary>
        /// End time of fragment
        /// </summary>
        public DateTimeOffset EndTime { get; private set; }

        /// <summary>
        /// Number of bytes in the fragment
        /// </summary>
        public long FragmentSize { get; private set; }

        /// <summary>
        /// Create a storage summary instance for a particular session fragment
        /// </summary>
        public FragmentStorageSummary(DateTimeOffset startTime, DateTimeOffset endTime, long size)
        {
            StartTime = startTime;
            EndTime = endTime;
            FragmentSize = size;
        }

        /// <summary>
        /// Compare two FragmentStorageSummary for sorting purposes
        /// </summary>
        public int CompareTo(FragmentStorageSummary other)
        {
            return (int)(EndTime.UtcTicks - other.EndTime.UtcTicks);
        }
    }

    /// <summary>
    /// Records a summary of packet count and aggregate size for one specific packet type
    /// </summary>
    [DebuggerDisplay("Name: {QualifiedTypeName} Packets: {PacketCount} Total Bytes: {PacketSize}")]
    public class PacketTypeStorageSummary : IComparable
    {
        /// <summary>
        /// Qualified type name from the related PacketDefinition
        /// </summary>
        public string QualifiedTypeName { get; private set; }

        /// <summary>
        /// Short type name from the related PacketDefinition
        /// </summary>
        /// <remarks>
        /// In particular, there can be many instances of EventMetricSamplePacket that
        /// vary only by QualifiedTypeName
        /// </remarks>
        public string TypeName { get; private set; }

        /// <summary>
        /// Number of packets of this type that were read
        /// </summary>
        public int PacketCount { get; set; }

        /// <summary>
        /// Total number of bytes of this packet type read from the file
        /// </summary>
        /// <remarks>
        /// Packet sizes are collected as uncompressed bytes.  But once all fragments have been read,
        /// the FileStorageSummary.Summarize method is called from Session to scale all the PacketSize
        /// values such that they represent compressed bytes.
        /// </remarks>
        public long PacketSize { get; set; }

        /// <summary>
        /// Returns the average number of bytes per packet (rounded up)
        /// </summary>
        public long AveragePacketSize
        {
            get
            {
                if (PacketCount <= 0)
                    return PacketSize;

                return (long)Math.Ceiling((double)PacketSize/PacketCount);
            }
        }
        /// <summary>
        /// Create a storage summary instance referencing a particualr PacketDefinition
        /// </summary>
        public PacketTypeStorageSummary(PacketDefinition packetDefinition)
        {
            QualifiedTypeName = packetDefinition.QualifiedTypeName;
            TypeName = packetDefinition.TypeName;
            PacketCount = packetDefinition.PacketCount;
            PacketSize = packetDefinition.PacketSize;
        }

        /// <summary>
        /// Default sort is descending by PacketCount within descending PacketSize
        /// </summary>
        public int CompareTo(object obj)
        {
            var other = (PacketTypeStorageSummary)obj;

            if (PacketSize == other.PacketSize)
                return other.PacketCount - PacketCount;

            return (int)(other.PacketSize - PacketSize);
        }
    }
}
