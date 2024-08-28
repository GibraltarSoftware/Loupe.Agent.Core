using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Gibraltar.Monitor.Platform
{
    /// <summary>
    /// Determines characteristics of the underlying host in a platform-neutral way.
    /// </summary>
    internal static class HostCapabilities
    {
        /// <summary>
        /// Returns the total number of bytes allocated to the OS
        /// </summary>
        /// <returns></returns>
        /// <exception cref="PlatformNotSupportedException"></exception>
        internal static ulong GetTotalMemory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetTotalMemoryWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetTotalMemoryLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetTotalMemoryMacOS();
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported OS platform");
            }
        }

        private static ulong GetTotalMemoryWindows()
        {
            var memoryStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memoryStatus))
            {
                return memoryStatus.ullTotalPhys;
            }
            throw new InvalidOperationException("Unable to get total memory on Windows");
        }

        private static ulong GetTotalMemoryLinux()
        {
            string[] lines = System.IO.File.ReadAllLines("/proc/meminfo");
            foreach (string line in lines)
            {
                if (line.StartsWith("MemTotal:"))
                {
                    string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && ulong.TryParse(parts[1], out ulong memTotalKb))
                    {
                        return memTotalKb * 1024; // Convert from KB to bytes
                    }
                }
            }
            throw new InvalidOperationException("Unable to get total memory on Linux");
        }

        private static ulong GetTotalMemoryMacOS()
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sysctl",
                Arguments = "hw.memsize",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            using (var process = System.Diagnostics.Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                string[] parts = output.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && ulong.TryParse(parts[1].Trim(), out ulong memTotal))
                {
                    return memTotal;
                }
            }
            throw new InvalidOperationException("Unable to get total memory on macOS");
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    }
}
