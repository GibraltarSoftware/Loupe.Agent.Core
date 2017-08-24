using System;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Information used in the CredentialsRequired event.
    /// </summary>
    public class CredentialsRequiredEventArgs: EventArgs
    {
        /// <summary>
        /// Create a new event data object
        /// </summary>
        public CredentialsRequiredEventArgs(string endpointUri, Guid repositoryId, bool authFailed, IWebAuthenticationProvider authenticationProvider)
        {
            EndpointUri = endpointUri;
            RepositoryId = repositoryId;
            AuthenticationProvider = authenticationProvider;
            Cancel = false;
            AuthenticationFailed = authFailed;
        }

        /// <summary>
        /// The server being connected to
        /// </summary>
        public string EndpointUri { get; private set; }

        /// <summary>
        /// The repository being connected to
        /// </summary>
        /// <remarks>In extraordinary cases - like authentication is required to the server configuration page - this will be an empty GUID.</remarks>
        public Guid RepositoryId { get; private set; }

        /// <summary>
        /// Indicates if credentials are required because an authentication attempt failed.
        /// </summary>
        public bool AuthenticationFailed { get; private set; }

        /// <summary>
        /// An authentication provider with the credentials to use.
        /// </summary>
        public IWebAuthenticationProvider AuthenticationProvider { get; set; }

        /// <summary>
        /// True to cancel a connection attempt.
        /// </summary>
        public bool Cancel { get; set; }
    }

    /// <summary>
    /// The delegate for handling the Credentials Required event.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="e"></param>
    public delegate void CredentialsRequiredEventHandler(object source, CredentialsRequiredEventArgs e); 
}
