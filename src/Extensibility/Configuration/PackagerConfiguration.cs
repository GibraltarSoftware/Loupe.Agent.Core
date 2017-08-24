namespace Loupe.Configuration
{
    /// <summary>
    /// The configuration of the packager.
    /// </summary>
    public sealed class PackagerConfiguration
    {
        /// <summary>
        /// The default HotKey configuration string for the packager.
        /// </summary>
        public const string DefaultHotKey = "Ctrl-Alt-F4";

        /// <summary>
        /// Initialize the packager configuration from the application configuration
        /// </summary>
        public PackagerConfiguration()
        {
            HotKey = DefaultHotKey;
            AllowFile = true;
            AllowEmail = true;
            AllowRemovableMedia = true;
            AllowServer = true;            
        }

        /// <summary>
        /// The key sequence used to pop up the packager.
        /// </summary>
        public string HotKey { get; set; }

        /// <summary>
        /// When true the user will be allowed to save the package to a file.
        /// </summary>
        public bool AllowFile { get; set; }

        /// <summary>
        /// When true the user will be allowed to save the package directly to the root of a removable media volume
        /// </summary>
        public bool AllowRemovableMedia { get; set; }

        /// <summary>
        /// When true the user will be allowed to send the package via email
        /// </summary>
        public bool AllowEmail { get; set; }

        /// <summary>
        /// When true the user will be allowed to send sessions to a session data server
        /// </summary>
        public bool AllowServer { get; set; }

        /// <summary>
        /// The email address to use as the sender&apos;s address
        /// </summary>
        /// <remarks>If specified, the user will not be given the option to override it.</remarks>
        public string FromEmailAddress { get; set; }

        /// <summary>
        /// The address to send the email to.
        /// </summary>
        /// <remarks>If specified, the user will not be given the option to override it.</remarks>
        public string DestinationEmailAddress { get; set; }

        /// <summary>
        /// The product name to use instead of the current application.
        /// </summary>
        /// <remarks>Primarily used in the Packager.exe.config file to specify the end-user product and application
        /// you want to package information for instead of the current application.  If specified, the name
        /// must exactly match the name shown in Loupe for the product.
        /// <para>To limit the package to one application within a product specify the applicationName as well
        /// as the productName.  Specifying just the product name will cause the package to contain all applications
        /// for the specified product.</para></remarks>
        public string ProductName { get; set; }

        /// <summary>
        /// The application name to use instead of the current application.
        /// </summary>
        /// <remarks><para>Primarily used in the Packager.exe.config file to specify the end-user application
        /// you want to package information for instead of the current application.  If specified, the name
        /// must exactly match the name shown in Loupe for the application.</para>
        /// <para>Application name is ignored if product name is not also specified.</para></remarks>
        public string ApplicationName { get; set; }


        /// <summary>
        /// Normalize the configuration options
        /// </summary>
        public void Sanitize()
        {
            if (string.IsNullOrEmpty(HotKey))
                HotKey = DefaultHotKey;

            if (string.IsNullOrEmpty(ProductName))
                ProductName = null;

            if (string.IsNullOrEmpty(ApplicationName))
                ApplicationName = null;

            if (string.IsNullOrEmpty(FromEmailAddress))
                FromEmailAddress = null;

            if (string.IsNullOrEmpty(DestinationEmailAddress))
                DestinationEmailAddress = null;
        }
    }
}
