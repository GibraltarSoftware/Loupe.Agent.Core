using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Loupe.Configuration;
using Loupe.Extensibility.Data;
using Loupe.Logging;

namespace Gibraltar.Monitor.Net
{
    /// <summary>
    /// Monitors the Common Language Runtime for noteworthy events.
    /// </summary>
    /// <remarks>This listener is automatically activated by the PerformanceMonitor class.</remarks>
    public class CLRListener : IDisposable
    {
        private const string ThisLogSystem = "Gibraltar";

        private bool m_Disposed;

        private bool m_NetworkEventsEnabled; //protected by LOCK

        //the network states dictionary is protected by NETWORKSTATESLOCK
        private readonly Dictionary<string, NetworkState> m_NetworkStates = new Dictionary<string, NetworkState>(StringComparer.OrdinalIgnoreCase);

        private readonly object m_Lock = new object();
        private readonly object m_NetworkStatesLock = new object();

        #region Private Class MessageSource

        /// <summary>
        /// Provides method source to log method to prevent normal call stack interpretation 
        /// </summary>
        /// <remarks>Since this listener deals with CLR events the message source information isn't
        /// very interesting.  We don't want to pay the performance price of it doing its normal
        /// lookup so we'll override the behavior.</remarks>
        private class MessageSource : IMessageSourceProvider
        {
            public MessageSource(string className, string methodName)
            {
                MethodName = methodName;
                ClassName = className;
                FileName = null;
                LineNumber = 0;
            }

            /// <summary>
            /// Should return the simple name of the method which issued the log message.
            /// </summary>
            public string MethodName { get; private set; }

            /// <summary>
            /// Should return the full name of the class (with namespace) whose method issued the log message.
            /// </summary>
            public string ClassName { get; private set; }

            /// <summary>
            /// Should return the name of the file containing the method which issued the log message.
            /// </summary>
            public string FileName { get; private set; }

            /// <summary>
            /// Should return the line within the file at which the log message was issued.
            /// </summary>
            public int LineNumber { get; private set; }
        }

        #endregion

        #region Private Class NetworkState

        private class NetworkState
        {
            public NetworkState(NetworkInterface nic)
            {
                Id = nic.Id;
                Name = nic.Name;
                NetworkInterfaceType = nic.NetworkInterfaceType;
                Description = nic.Description;
                Speed = nic.Speed;
                OperationalStatus = nic.OperationalStatus;

                IPInterfaceProperties ipProperties = null;
                try
                {
                    ipProperties = nic.GetIPProperties();
                    DnsAddresses = ipProperties.DnsAddresses;
                    WinsServersAddresses = ipProperties.WinsServersAddresses;
                    GatewayAddresses = ipProperties.GatewayAddresses;
                    UnicastIPAddresses = ipProperties.UnicastAddresses;
                }
                catch
                {
                }

                /* KM: Not in use yet, waiting to see if we realy wait this kind of detail.
                if (ipProperties != null)
                {
                    if (nic.Supports(NetworkInterfaceComponent.IPv4))
                    {
                        try
                        {
                            IP4Properties = ipProperties.GetIPv4Properties();
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        IP4Properties = null;
                    }

                    if (nic.Supports(NetworkInterfaceComponent.IPv6))
                    {
                        try
                        {
                            IP6Properties = ipProperties.GetIPv6Properties();
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        IP6Properties = null;
                    }
                }
                */
            }

            public string Id { get; private set; }

            public string Name { get; private set; }

            public NetworkInterfaceType NetworkInterfaceType { get; private set; }

            public string Description { get; private set; }

            public long Speed { get; private set; }

            public OperationalStatus OperationalStatus { get; private set; }

            public IPAddressCollection DnsAddresses { get; private set; }

            public GatewayIPAddressInformationCollection GatewayAddresses { get; private set; }

            public IPAddressCollection WinsServersAddresses { get; private set; }

            public UnicastIPAddressInformationCollection UnicastIPAddresses { get; private set; }

            //            public IPv4InterfaceProperties IP4Properties { get; private set; }

            //            public IPv6InterfaceProperties IP6Properties { get; private set; }
        }

        #endregion

        #region Public Properties and Methods


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// <remarks>Calling Dispose() (automatic when a using statement ends) will generate the metric.</remarks>
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);

            //SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Initialize the Common Language Runtime Listener with the provided configuration
        /// </summary>
        /// <param name="configuration"></param>
        public void Initialize(ListenerConfiguration configuration)
        {
            lock (m_Lock)
            {
                if (configuration.EnableNetworkEvents)
                {
                    if (m_NetworkEventsEnabled == false)
                        RegisterNetworkEvents();
                }
                else
                {
                    if (m_NetworkEventsEnabled)
                        UnregisterNetworkEvents();
                }
            }            
        }

        #endregion

        #region Protected Properties and Methods

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
                }
                
                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here

                //because we're interfacing with system events we're going to go ahead and potentially reference
                //other objects because we're only talking to runtime internal objects
                //NOTE: We are deliberately NOT using the lock because we don't want to risk a deadlock.
                if (m_NetworkEventsEnabled)
                    UnregisterNetworkEvents();

                m_Disposed = true; // Make sure we only do this once
            }
        }

        #endregion

        #region Private Properties and Methods


        private void RegisterNetworkEvents()
        {
            try
            {
                m_NetworkEventsEnabled = true;
                NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            }
// ReSharper disable EmptyGeneralCatchClause
            catch
// ReSharper restore EmptyGeneralCatchClause
            {
            }

            try
            {
                EnsureNetworkInterfacesRecorded();
            }
// ReSharper disable EmptyGeneralCatchClause
            catch
// ReSharper restore EmptyGeneralCatchClause
            {
            }
        }

        private void UnregisterNetworkEvents()
        {
            try
            {
                NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
            }
// ReSharper disable EmptyGeneralCatchClause
            catch
// ReSharper restore EmptyGeneralCatchClause
            {
            }
            finally
            {
                m_NetworkEventsEnabled = false;
            }
        }

        private void EnsureNetworkInterfacesRecorded()
        {
            RecordNetworkState(false); // false if we want to record the initial baseline state without logging it.
            // It may not log it anyway, because we get initialized during Log initialization, so it just drops the writes.
        }

        /// <summary>
        /// Convert a raw data rate number (in bps) into human-readable form.
        /// </summary>
        /// <remarks>Exact multiples of 1000 are bumped up to the next larger units, as are values exceeding four digits.
        /// Fractional units are displayed, if applicable, up to three digits (using InvariantCulture).
        /// Rates less than 1 Kbps (or less than 1 Mbps and containing fractional Kbps) are displayed as fractional Kbps.</remarks>
        /// <param name="bpsRate">The data rate (in bps--bits per second) to be displayed.</param>
        /// <returns>A string formatted as a rate with units.</returns>
        internal static string FormatDataRate(long bpsRate)
        {
            // Notice that data rates use 1000 not 1024 for K/M/G factors; they are generally not powers of 2.

            long rate = bpsRate;
            long fraction = rate % 1000; // Get the fractional portion of Kbps.
            rate /= 1000; // Get the whole portion of Kbps.
            string unitString = "Kbps"; // Use these units by default

            if (rate >= 10000 || (rate >= 1000 && fraction == 0))
            {
                // At least 10 Mbps or it's some Mbps with exact Kbps fraction, let's use Mbps units...
                fraction = rate % 1000; // Get the fractional portion of Mbps.
                rate /= 1000; // Get the whole portion of Mbps.

                if (rate >= 10000 || (rate >= 1000 && fraction == 0))
                {
                    // At least 10 Gbps or it's some Gbps with exact Mbps fraction, let's use Gbps units...
                    fraction = rate % 1000; // Get the fractional portion of Gbps.
                    rate /= 1000; // Get the whole portion of Gbps.
                    unitString = "Gbps";
                }
                else
                {
                    // Otherwise, we're going with Mbps.
                    unitString = "Mbps";
                }
            }
            // Otherwise, we're going with the default Kbps.

            string formatString;

            if (fraction == 0)
            {
                // It's an exact amount of the selected units.  Display without fraction.
                formatString = string.Format(CultureInfo.InvariantCulture, "{0} {1}", rate, unitString); // No need for {0:N0} ?
            }
            else
            {
                // It's a fractional amount of the selected units.  Format for fractional decimal display.
                string fractionString = string.Empty;

                while (fraction > 0)
                {
                    fractionString += fraction / 100; // Append the next fractional digit.
                    fraction = (fraction * 10) % 1000; // Shift next digit to 100ths place, discard previous digit.
                }

                formatString = string.Format(CultureInfo.InvariantCulture, "{0}.{1} {2}", rate, fractionString, unitString);
            }

            return formatString;
        }

        private void RecordNetworkState(bool logChanges)
        {
            NetworkInterface[] adapters;
            try
            {
                // This "Only works on Linux and Windows" under Mono, so do in a try/catch to be safe.
                adapters = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch
            {
                UnregisterNetworkEvents(); // Disable further network logging?
                return;
            }

            lock (m_NetworkStatesLock)
            {
                //now check each one (except loopback) to see if it's in our collection.
                foreach (NetworkInterface adapter in adapters)
                {
                    if ((adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        && (adapter.NetworkInterfaceType != NetworkInterfaceType.Tunnel))
                    {
                        if (m_NetworkStates.TryGetValue(adapter.Id, out var previousState) == false)
                        {
                            //it's brand new - need to add it and record it as new.
                            previousState = new NetworkState(adapter);
                            m_NetworkStates.Add(previousState.Id, previousState);

                            if (logChanges)
                            {
                                string interfaceInfo = FormatNetworkAdapterState(previousState);
                                LogEvent(LogMessageSeverity.Verbose, "System.Events.Network", "Network Interface Detected", interfaceInfo);
                            }
                        }
                        else
                        {
                            //see if it changed.
                            bool hasChanged = false;
                            string changes = string.Empty;

                            NetworkState newState = new NetworkState(adapter);

                            if (newState.OperationalStatus != previousState.OperationalStatus)
                            {
                                hasChanged = true;
                                changes += string.Format(CultureInfo.InvariantCulture, "Operational Status Changed from {0} to {1}\r\n", previousState.OperationalStatus, newState.OperationalStatus);
                            }

                            if (newState.Speed != previousState.Speed)
                            {
                                hasChanged = true;
                                changes += string.Format(CultureInfo.InvariantCulture, "Speed Changed from {0} to {1}\r\n",
                                                         FormatDataRate(previousState.Speed), FormatDataRate(newState.Speed));
                            }

                            //find any IP configuration change.
                            if (IPConfigurationChanged(previousState, newState))
                            {
                                hasChanged = true;
                                changes += "TCP/IP Configuration Changed.\r\n";
                            }

                            if (hasChanged)
                            {
                                //replace the item in the collection with the new item
                                m_NetworkStates.Remove(previousState.Id);
                                m_NetworkStates.Add(newState.Id, newState);

                                if (logChanges)
                                {
                                    string interfaceInfo = FormatNetworkAdapterState(newState);
                                    LogEvent(LogMessageSeverity.Information, "System.Events.Network", "Network Interface Changes Detected", "\r\n{0}\r\nNew State:\r\n{1}", changes, interfaceInfo);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool IPConfigurationChanged(NetworkState previousState, NetworkState newState)
        {
            //Gateways            
            if ((previousState.GatewayAddresses != null) && (newState.GatewayAddresses == null))
            {
                return true;
            }

            if ((previousState.GatewayAddresses == null) && (newState.GatewayAddresses != null))
            {
                return true;
            }

            if ((previousState.GatewayAddresses != null) && (newState.GatewayAddresses != null))
            {
                //need to check addresses.
                if (previousState.GatewayAddresses.Count != newState.GatewayAddresses.Count)
                    return true;

                foreach (GatewayIPAddressInformation ipAddressInformation in newState.GatewayAddresses)
                {
                    bool foundOurAddress = false;
                    foreach (GatewayIPAddressInformation previousAddress in newState.GatewayAddresses)
                    {
                        //if this address is our previous address, we're ready to check the next address.
                        if (ipAddressInformation.Address.Equals(previousAddress.Address))
                        {
                            foundOurAddress = true;
                            break;
                        }
                    }

                    if (foundOurAddress == false)
                    {
                        //if we got through all of the gateways and didn't find our address, this is a change.
                        return true;
                    }
                }
            }

            //IP Addresses
            if ((previousState.UnicastIPAddresses != null) && (newState.UnicastIPAddresses == null))
            {
                return true;
            }

            if ((previousState.UnicastIPAddresses == null) && (newState.UnicastIPAddresses != null))
            {
                return true;
            }

            if ((previousState.UnicastIPAddresses != null) && (newState.UnicastIPAddresses != null))
            {
                //need to check addresses.
                if (previousState.UnicastIPAddresses.Count != newState.UnicastIPAddresses.Count)
                    return true;

                foreach (UnicastIPAddressInformation ipAddressInformation in newState.UnicastIPAddresses)
                {
                    bool foundOurAddress = false;
                    foreach (UnicastIPAddressInformation previousAddress in newState.UnicastIPAddresses)
                    {
                        //if this address is our previous address, we're ready to check the next address.
                        if (ipAddressInformation.Address.Equals(previousAddress.Address))
                        {
                            foundOurAddress = true;
                            break;
                        }
                    }

                    if (foundOurAddress == false)
                    {
                        //if we got through all of the gateways and didn't find our address, this is a change.
                        return true;
                    }
                }
            }

            //DNS            
            if ((previousState.DnsAddresses != null) && (newState.DnsAddresses == null))
            {
                return true;
            }

            if ((previousState.DnsAddresses == null) && (newState.DnsAddresses != null))
            {
                return true;
            }

            if ((previousState.DnsAddresses != null) && (newState.DnsAddresses != null))
            {
                //need to check addresses.
                if (previousState.DnsAddresses.Count != newState.DnsAddresses.Count)
                    return true;

                foreach (IPAddress ipAddressInformation in newState.DnsAddresses)
                {
                    bool foundOurAddress = false;
                    foreach (IPAddress previousAddress in newState.DnsAddresses)
                    {
                        //if this address is our previous address, we're ready to check the next (outer) address.
                        if (ipAddressInformation.Equals(previousAddress))
                        {
                            foundOurAddress = true;
                            break;
                        }
                    }

                    if (foundOurAddress == false)
                    {
                        //if we got through all of the gateways and didn't find our address, this is a change.
                        return true;
                    }
                }
            }

            //WINS            
            if ((previousState.WinsServersAddresses != null) && (newState.WinsServersAddresses == null))
            {
                return true;
            }

            if ((previousState.WinsServersAddresses == null) && (newState.WinsServersAddresses != null))
            {
                return true;
            }

            if ((previousState.WinsServersAddresses != null) && (newState.WinsServersAddresses != null))
            {
                //need to check addresses.
                if (previousState.WinsServersAddresses.Count != newState.WinsServersAddresses.Count)
                    return true;

                foreach (IPAddress ipAddressInformation in newState.WinsServersAddresses)
                {
                    bool foundOurAddress = false;
                    foreach (IPAddress previousAddress in newState.WinsServersAddresses)
                    {
                        //if this address is our previous address, we're ready to check the next (outer) address.
                        if (ipAddressInformation.Equals(previousAddress))
                        {
                            foundOurAddress = true;
                            break;
                        }
                    }

                    if (foundOurAddress == false)
                    {
                        //if we got through all of the gateways and didn't find our address, this is a change.
                        return true;
                    }
                }
            }


            return false; //if we got this far with nothing, no changes.
        }


        private static string FormatNetworkAdapterState(NetworkState adapterState)
        {
            StringBuilder stringBuild = new StringBuilder(1024);

            stringBuild.AppendFormat("Name: {0}\r\n", adapterState.Name);
            stringBuild.AppendFormat("Interface Type: {0}\r\n", adapterState.NetworkInterfaceType);
            stringBuild.AppendFormat("Description: {0}\r\n", adapterState.Description);


            string displayStatus;
            switch (adapterState.OperationalStatus)
            {
                case OperationalStatus.Up:
                    displayStatus = "UP: The network interface is up; it can transmit data packets.";
                    break;
                case OperationalStatus.Down:
                    displayStatus = "DOWN: The network interface is unable to transmit data packets.";
                    break;
                case OperationalStatus.Testing:
                    displayStatus = "TESTING: The network interface is running tests.";
                    break;
                case OperationalStatus.Unknown:
                    displayStatus = "UNKNOWN: The network interface status is not known.";
                    break;
                case OperationalStatus.Dormant:
                    displayStatus = "DORMANT: The network interface is not in a condition to transmit data packets; it is waiting for an external event.";
                    break;
                case OperationalStatus.NotPresent:
                    displayStatus = "NOT PRESENT: The network interface is unable to transmit data packets because of a missing component, typically a hardware component.";
                    break;
                case OperationalStatus.LowerLayerDown:
                    displayStatus = "LOWER LAYER DOWN: The network interface is unable to transmit data packets because it runs on top of one or more other interfaces, and at least one of these 'lower layer' interfaces is down.";
                    break;
                default:
                    displayStatus = adapterState.OperationalStatus.ToString();
                    break;
            }

            stringBuild.AppendFormat("Status: {0}\r\n", displayStatus);

            if (adapterState.OperationalStatus == OperationalStatus.Up)
            {

                //convert speed to Kbps
                stringBuild.AppendFormat("Maximum Speed: {0}\r\n", FormatDataRate(adapterState.Speed));

                //since we have at least one IP protocol, output general IP stuff
                stringBuild.AppendFormat("DNS Servers: {0}\r\n", FormatIPAddressList(adapterState.DnsAddresses));
                stringBuild.AppendFormat("WINS Servers: {0}\r\n", FormatIPAddressList(adapterState.WinsServersAddresses));
                stringBuild.AppendFormat("Gateways: {0}\r\n", FormatGatewayIPAddressList(adapterState.GatewayAddresses));

                stringBuild.AppendFormat("IPv4 Addresses: {0}\r\n", FormatUnicastAddressList(adapterState.UnicastIPAddresses, AddressFamily.InterNetwork));
                stringBuild.AppendFormat("IPV6 Addresses: {0}\r\n", FormatUnicastAddressList(adapterState.UnicastIPAddresses, AddressFamily.InterNetworkV6));

                //check the quality of the IP addresses:
                if (adapterState.UnicastIPAddresses != null)
                {
                    foreach (UnicastIPAddressInformation addressInformation in adapterState.UnicastIPAddresses)
                    {
                        if ((addressInformation.DuplicateAddressDetectionState != DuplicateAddressDetectionState.Preferred)
                            && (addressInformation.DuplicateAddressDetectionState != DuplicateAddressDetectionState.Deprecated))
                        {
                            string reason;

                            switch (addressInformation.DuplicateAddressDetectionState)
                            {
                                case DuplicateAddressDetectionState.Invalid:
                                    reason = "the address is not valid. A nonvalid address is expired and no longer assigned to an interface; applications should not send data packets to it.";
                                    break;
                                case DuplicateAddressDetectionState.Tentative:
                                    reason = "the duplicate address detection procedure's evaluation of the address has not completed successfully. Applications should not use the address because it is not yet valid and packets sent to it are discarded.";
                                    break;
                                case DuplicateAddressDetectionState.Duplicate:
                                    reason = "the address is not unique. This address should not be assigned to the network interface.";
                                    break;
                                default:
                                    reason = addressInformation.DuplicateAddressDetectionState.ToString();
                                    break;
                            }

                            stringBuild.AppendFormat("\r\nThe IP address {0} is not currently usable because {1}\r\n", addressInformation.Address, reason);
                        }
                    }
                }
            }

            return stringBuild.ToString();
        }

        private static string FormatUnicastAddressList(UnicastIPAddressInformationCollection addressCollection, AddressFamily family)
        {
            //figure out the IP4 addressees
            string ipAddresses;
            if (addressCollection == null)
            {
                ipAddresses = "NONE";
            }
            else if (addressCollection.Count == 0)
            {
                ipAddresses = "NONE";
            }
            else
            {
                ipAddresses = string.Empty;
                foreach (UnicastIPAddressInformation addressInformation in addressCollection)
                {
                    if (addressInformation.Address.AddressFamily == family)
                    {
                        if (string.IsNullOrEmpty(ipAddresses))
                        {
                            ipAddresses = addressInformation.Address.ToString();
                        }
                        else
                        {
                            ipAddresses += ", " + addressInformation.Address;
                        }
                    }
                }
            }

            return ipAddresses;
        }

        private static string FormatIPAddressList(IPAddressCollection addressCollection)
        {
            if (addressCollection == null)
                return "NONE";

            if (addressCollection.Count == 0)
                return "NONE";

            string addresses = string.Empty;

            foreach (IPAddress ipAddress in addressCollection)
            {
                if (string.IsNullOrEmpty(addresses))
                {
                    addresses = ipAddress.ToString();
                }
                else
                {
                    addresses += ", " + ipAddress;
                }
            }

            return addresses;
        }

        private static string FormatGatewayIPAddressList(GatewayIPAddressInformationCollection addressCollection)
        {
            if (addressCollection == null)
                return "NONE";

            if (addressCollection.Count == 0)
                return "NONE";

            string addresses = "";

            foreach (GatewayIPAddressInformation ipAddress in addressCollection)
            {
                if (string.IsNullOrEmpty(addresses))
                {
                    addresses += ipAddress.Address.ToString();
                }
                else
                {
                    addresses += ", " + ipAddress.Address;
                }
            }

            return addresses;
        }

        private static void LogEvent(LogMessageSeverity severity, string category, string caption, string description, params object[] args)
        {
            MessageSource source = new MessageSource("Gibraltar.Agent.Net.CLRListener", "LogEvent");
            Log.WriteMessage(severity, LogWriteMode.Queued, ThisLogSystem, category, source, null, null, null, caption, description, args);
        }

        #endregion

        #region Event Handlers

        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            try
            {
                //we don't get any specific information from the event so we'll have to figure it out.
                RecordNetworkState(true);
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
            }
        }

        #endregion
    }
}
