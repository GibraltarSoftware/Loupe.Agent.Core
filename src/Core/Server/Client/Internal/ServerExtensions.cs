using System;
using System.Collections.Generic;
using System.Text;
using Loupe.Core.Server.Client.Data;
using Loupe.Configuration;

namespace Loupe.Core.Server.Client.Internal
{
    internal static class ServerExtensions
    {
        /// <summary>
        /// Get a server configuration from a redirect request
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static ServerConfiguration ToServerConfiguration(this HubConfigurationXml configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (configuration.redirectRequested == false)
                throw new InvalidOperationException("No connection redirection information provided");

            var newConfiguration = new ServerConfiguration();

            if (configuration.redirectUseGibraltarSdsSpecified)
                newConfiguration.UseGibraltarService = configuration.redirectUseGibraltarSds;

            newConfiguration.CustomerName = configuration.redirectCustomerName;
            newConfiguration.Server = configuration.redirectHostName;

            if (configuration.redirectPortSpecified)
                newConfiguration.Port = configuration.redirectPort;

            if (configuration.redirectUseSslSpecified)
                newConfiguration.UseSsl = configuration.redirectUseSsl;

            newConfiguration.ApplicationBaseDirectory = configuration.redirectApplicationBaseDirectory;
            newConfiguration.Repository = configuration.redirectCustomerName; //it doesn't specify repository.

            return newConfiguration;
        }
    }
}
