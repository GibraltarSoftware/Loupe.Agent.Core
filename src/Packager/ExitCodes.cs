using System;
using System.Collections.Generic;
using System.Text;

namespace Loupe.Packager
{
    /// <summary>
    /// The various exit codes returned by the packager
    /// </summary>
    [Flags]
    public enum ExitCodes
    {
        /// <summary>
        /// The packager completed successfully.  It may not have sent any data if there was none to send.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Flag value indicating a configuration error occurred
        /// </summary>
        ConfigurationError = 1,

        /// <summary>
        /// No product name was specified in any configuration
        /// </summary>
        MissingProductName = ConfigurationError | 2,

        /// <summary>
        /// No transmit mode was specified in any configuration
        /// </summary>
        MissingTransmitMode = ConfigurationError | 4,

        /// <summary>
        /// Transmit to server was specified but the server could not be determined from configuration
        /// </summary>
        MissingServerInfo = ConfigurationError | 8,
        
        /// <summary>
        /// Transmit to file was specified but the destination file was not
        /// </summary>
        MissingFileInfo = ConfigurationError | 16,

        /// <summary>
        /// An unknown transmit mode was specified
        /// </summary>
        InvalidTransmitMode = ConfigurationError | 32,

        /// <summary>
        /// The packager configuration file could not be parsed
        /// </summary>
        BadConfigurationFile = ConfigurationError | 64,

        /// <summary>
        /// Flag value indicating a runtime exception occurred
        /// </summary>
        RuntimeException = 1024,
    }
}
