using System;
using System.Collections.Generic;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Manages the cached credentials for the current process
    /// </summary>
    /// <remarks>By retrieving repository credentials from the credential manager you ensure coordination
    /// between all of the channels in the process.  This eliminates cases where two threads presenting 
    /// alternate credentials cause reauthentication which dramatically decreases efficiency of communication.</remarks>
    public static class CachedCredentialsManager
    {
        private static readonly object g_Lock = new object();
        private static readonly object g_RequestLock = new object();

        //we have to cache using a case-sensitive comparison of the endpoint because hashing is case-sensitive.
        private static readonly Dictionary<CredentialCacheKey, IWebAuthenticationProvider> g_CachedCredentials = new Dictionary<CredentialCacheKey, IWebAuthenticationProvider>(); //PROTECTED BY LOCK
        private static readonly Dictionary<string, string> g_CachedBlockedCredentials = new Dictionary<string, string>(StringComparer.Ordinal); //PROTECTED BY LOCK

        /// <summary>
        /// Event raised when a connection requires credentials and they aren't present.
        /// </summary>
        public static event CredentialsRequiredEventHandler CredentialsRequired;

        #region Private Class CredentialCacheKey

        private class CredentialCacheKey : IEquatable<CredentialCacheKey>
        {
            private int m_HashCode; //to avoid extra cost of calculation

            public CredentialCacheKey(string entryUri, Guid repositoryId)
            {
                EntryUri = entryUri;
                RepositoryId = repositoryId;
                m_HashCode = EntryUri.ToLowerInvariant().GetHashCode();
                m_HashCode = m_HashCode ^ RepositoryId.GetHashCode();
            }

            public string EntryUri { get; private set; }
            public Guid RepositoryId { get; private set; }

            public bool Equals(CredentialCacheKey other)
            {
                if (String.Equals(EntryUri, other.EntryUri, StringComparison.OrdinalIgnoreCase) == false)
                    return false;

                if (RepositoryId != other.RepositoryId)
                    return false;

                return true;
            }

            public override int GetHashCode()
            {
                return m_HashCode;
            }
        }

        #endregion

        /// <summary>
        /// Get credentials for the specified URL target and repository information
        /// </summary>
        /// <param name="targetChannel">The web channel representing the endpoint that the credentials are for</param>
        /// <param name="useApiKey">True if an API key was used to originally set up the connection</param>
        /// <param name="repositoryId">The owner Id to specify to the server (for example repository Id)</param>
        /// <param name="keyContainerName">The name of the key container to retrieve the private key from</param>
        /// <param name="useMachineStore">True to use the machine store instead of the user store for the digital certificate</param>
        /// <returns></returns>
        /// <remarks>If existing credentials are available they will be provided, otherwise a new credentials object will be created and returned.
        /// This method is Multithread safe.</remarks>
        public static IWebAuthenticationProvider GetCredentials(WebChannel targetChannel, bool useApiKey, Guid repositoryId, string keyContainerName, bool useMachineStore)
        {
            return GetCredentials(GetEntryUri(targetChannel), useApiKey, repositoryId, keyContainerName, useMachineStore);
        }

        /// <summary>
        /// Get credentials for the specified URL target and repository information
        /// </summary>
        /// <param name="targetChannel">The web channel representing the endpoint that the credentials are for</param>
        /// <param name="useApiKey">True if an API key was used to originally set up the connection</param>
        /// <param name="repositoryId">The owner Id to specify to the server (for example repository Id)</param>
        /// <param name="keyContainerName">The name of the key container to retrieve the private key from</param>
        /// <param name="useMachineStore">True to use the machine store instead of the user store for the digital certificate</param>
        /// <returns></returns>
        /// <remarks>If existing credentials are available they will be provided, otherwise null will be returned.
        /// This method is Multithread safe.</remarks>
        public static IWebAuthenticationProvider GetCachedCredentials(WebChannel targetChannel, bool useApiKey, Guid repositoryId, string keyContainerName, bool useMachineStore)
        {
            return GetCachedCredentials(GetEntryUri(targetChannel), useApiKey, repositoryId, keyContainerName, useMachineStore);
        }

        /// <summary>
        /// Determine the entry URI used for credential keys
        /// </summary>
        public static string GetEntryUri(WebChannel channel)
        {
            return GetEntryUri(channel.HostName);
        }

        /// <summary>
        /// Determine the entry URI used for credential keys
        /// </summary>
        public static string GetEntryUri(string hostName)
        {
            return hostName.ToLowerInvariant(); //hopefully that doesn't mess up Unicode host names...
        }

        /// <summary>
        /// Get credentials for the specified URL target and repository information
        /// </summary>
        /// <param name="entryUri">The URI of the endpoint that the credentials are for</param>
        /// <param name="useApiKey">True if an API key was used to originally set up the connection</param>
        /// <param name="repositoryId">The owner Id to specify to the server (for example repository Id)</param>
        /// <param name="keyContainerName">The name of the key container to retrieve the private key from</param>
        /// <param name="useMachineStore">True to use the machine store instead of the user store for the digital certificate</param>
        /// <returns></returns>
        /// <remarks>If existing credentials are available they will be provided, otherwise a new credentials object will be created and returned.
        /// This method is Multithread safe.</remarks>
        private static IWebAuthenticationProvider GetCredentials(string entryUri, bool useApiKey, Guid repositoryId, string keyContainerName, bool useMachineStore)
        {
            IWebAuthenticationProvider credentials = GetCachedCredentials(entryUri, useApiKey, repositoryId, keyContainerName, useMachineStore);

            if (credentials == null)
            {
                //we failed to get them above - so we need to ask the user (outside of our lock so we don't block the world)
                credentials = RequestUserCredentials(entryUri, repositoryId);
            }

            return credentials;
        }

        private static IWebAuthenticationProvider GetCachedCredentials(string entryUri, bool useApiKey, Guid repositoryId, string keyContainerName, bool useMachineStore)
        {
            var cacheKey = new CredentialCacheKey(entryUri, repositoryId);

            IWebAuthenticationProvider credentials;
            lock (g_Lock) //gotta be MT safe!
            {
                System.Threading.Monitor.PulseAll(g_Lock);

                if (g_CachedCredentials.TryGetValue(cacheKey, out credentials) == false)
                {
                    //we didn't find it - we need to go ahead and add new credentials
                    if (useApiKey)
                    {
                        credentials = new RepositoryCredentials(repositoryId, keyContainerName, useMachineStore);
                        g_CachedCredentials.Add(cacheKey, credentials);
                    }
                }

                if (g_CachedBlockedCredentials.ContainsKey(entryUri))
                {
                    //since they're blocked we repeat the original user's intent    
                    System.Threading.Monitor.PulseAll(g_Lock);
                    throw new WebChannelAuthorizationException("User declined to provide credentials");
                }
            }
            return credentials;
        }

        /// <summary>
        /// Request user credentials, coordinating between multiple threads looking for the same credentials.
        /// </summary>
        /// <remarks>Unlike Update, this will not re-prompt the user if they previously declined to provide credentials</remarks>
        public static bool RequestCredentials(WebChannel targetChannel, Guid repositoryId)
        {
            return RequestCredentials(GetEntryUri(targetChannel), repositoryId);
        }

        /// <summary>
        /// Request user credentials, coordinating between multiple threads looking for the same credentials.
        /// </summary>
        /// <remarks>Unlike Update, this will not re-prompt the user if they previously declined to provide credentials and it will assume any cached credentials work.</remarks>
        public static bool RequestCredentials(string entryUri, Guid repositoryId)
        {
            var credentials = RequestUserCredentials(entryUri, repositoryId);

            return (credentials != null);
        }

        /// <summary>
        /// Request user credentials, coordinating between multiple threads looking for the same credentials.
        /// </summary>
        /// <remarks>Unlike Update, this will not re-prompt the user if they previously declined to provide credentials and it will assume any cached credentials work.</remarks>
        /// <returns>The new authentication provider</returns>
        /// <exception cref="WebChannelAuthorizationException">Thrown when no credentials were provided</exception>
        private static IWebAuthenticationProvider RequestUserCredentials(string entryUri, Guid repositoryId)
        {
            var cacheKey = new CredentialCacheKey(entryUri, repositoryId);

            IWebAuthenticationProvider authenticationProvider;

            //we only want one thread to pop up the UI to request authentication at a time.
            lock (g_RequestLock)
            {
                //WAIT: Since we requested the lock someone may have put credentials in the collection, so we have to check again.
                lock (g_Lock)
                {
                    System.Threading.Monitor.PulseAll(g_Lock);

                    if (g_CachedBlockedCredentials.ContainsKey(entryUri)) //someone may have just canceled.
                        throw new WebChannelAuthorizationException("User declined to provide credentials");

                    if (g_CachedCredentials.TryGetValue(cacheKey, out authenticationProvider))
                        return authenticationProvider; //yep, someone else got them.
                }

                var credentialEventArgs = new CredentialsRequiredEventArgs(entryUri, repositoryId, false, null);
                OnCredentialsRequired(null, credentialEventArgs);

                if (credentialEventArgs.Cancel)
                {
                    //if the user canceled we need to cache that so we don't keep pounding the user.
                    lock(g_Lock)
                    {
                        g_CachedBlockedCredentials[entryUri] = entryUri;

                        System.Threading.Monitor.PulseAll(g_Lock);
                    }

                    throw new WebChannelAuthorizationException("User declined to provide credentials");
                }

                if (ReferenceEquals(credentialEventArgs.AuthenticationProvider, null))
                {
                    throw new WebChannelAuthorizationException("No credentials are available for the specified server");
                }

                authenticationProvider = credentialEventArgs.AuthenticationProvider;

                lock (g_Lock)
                {
                    g_CachedCredentials.Add(cacheKey, authenticationProvider);

                    System.Threading.Monitor.PulseAll(g_Lock);
                }

                System.Threading.Monitor.PulseAll(g_RequestLock);
            }

            return authenticationProvider;
        }

        /// <summary>
        /// Attempt to re-query the credentials for the specified URI
        /// </summary>
        /// <param name="targetChannel">The web channel to update credentials for</param>
        /// <param name="forceUpdate">True to force a requery to the user even if they previously canceled requesting credentials</param>
        public static bool UpdateCredentials(WebChannel targetChannel, bool forceUpdate)
        {
            if (targetChannel == null)
                throw new ArgumentNullException(nameof(targetChannel));

            return UpdateCredentials(GetEntryUri(targetChannel), Guid.Empty, forceUpdate);
        }

        /// <summary>
        /// Attempt to re-query the credentials for the specified URI
        /// </summary>
        /// <param name="targetChannel">The web channel to update credentials for</param>
        /// <param name="repositoryId">The owner Id to specify to the server (for example repository Id)</param>
        /// <param name="forceUpdate">True to force a requery to the user even if they previously canceled requesting credentials</param>
        public static bool UpdateCredentials(WebChannel targetChannel, Guid repositoryId, bool forceUpdate)
        {
            if (targetChannel == null)
                throw new ArgumentNullException(nameof(targetChannel));

            if (repositoryId == Guid.Empty)
                throw new ArgumentNullException(nameof(repositoryId));

            return UpdateCredentials(GetEntryUri(targetChannel), repositoryId, forceUpdate);
        }

        /// <summary>
        /// Attempt to re-query the credentials for the specified URI
        /// </summary>
        /// <param name="entryUri">The entry URI to update credentials for</param>
        /// <param name="repositoryId">The owner Id to specify to the server (for example repository Id)</param>
        /// <param name="forceUpdate">True to force a requery to the user even if they previously canceled requesting credentials</param>
        /// <returns>True if the user provided updated credentials, false if they canceled</returns>
        public static bool UpdateCredentials(string entryUri, Guid repositoryId, bool forceUpdate)
        {
            var cacheKey = new CredentialCacheKey(entryUri, repositoryId);

            bool newCredentialsProvided = false;
            lock(g_Lock) //if we have any in cache we need to update those, so in this case we stall EVERYONE.
            {
                //WAIT: Since we requested the lock someone may have put credentials in the collection, so we have to check again.
                System.Threading.Monitor.PulseAll(g_Lock);

                if ((forceUpdate == false) && (g_CachedBlockedCredentials.ContainsKey(entryUri))) 
                    return false;

                g_CachedCredentials.TryGetValue(cacheKey, out var credentials);

                //we only want one thread to pop up the UI to request authentication at a time.
                lock(g_RequestLock)
                {
                    var credentialEventArgs = new CredentialsRequiredEventArgs(entryUri, repositoryId, true, credentials);
                    OnCredentialsRequired(null, credentialEventArgs);

                    if (credentialEventArgs.Cancel == false)
                    {
                        if (credentialEventArgs.AuthenticationProvider == null)
                        {
                            throw new InvalidOperationException("No credentials are available for the specified server");
                        }

                        newCredentialsProvided = true;

                        g_CachedBlockedCredentials.Remove(entryUri); //if it was previously blocked, unblock it.

                        g_CachedCredentials[cacheKey] = credentialEventArgs.AuthenticationProvider; //overwrite any existing value.
                    }

                    System.Threading.Monitor.PulseAll(g_RequestLock);
                }

                System.Threading.Monitor.PulseAll(g_Lock);
            }

            return newCredentialsProvided;
        }

        private static void OnCredentialsRequired(object source, CredentialsRequiredEventArgs e)
        {
            CredentialsRequiredEventHandler handler = CredentialsRequired;
            if (handler != null)
                handler(source, e);
        }
    }
}
