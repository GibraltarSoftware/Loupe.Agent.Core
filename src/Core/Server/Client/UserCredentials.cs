#region File Header
// /********************************************************************
//  * COPYRIGHT:
//  *    This software program is furnished to the user under license
//  *    by Gibraltar Software Inc, and use thereof is subject to applicable 
//  *    U.S. and international law. This software program may not be 
//  *    reproduced, transmitted, or disclosed to third parties, in 
//  *    whole or in part, in any form or by any manner, electronic or
//  *    mechanical, without the express written consent of Gibraltar Software Inc,
//  *    except to the extent provided for by applicable license.
//  *
//  *    Copyright © 2008 - 2015 by Gibraltar Software, Inc.  
//  *    All rights reserved.
//  *******************************************************************/
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Traditional user/password credentials
    /// </summary>
    public sealed class UserCredentials : IWebAuthenticationProvider
    {
        /// <summary>
        /// The prefix for the authorization header for this credential type
        /// </summary>
        public const string AuthorizationPrefix = "Gibraltar-User-Credentials";

        /// <summary>
        /// The name of the parameter in the form post for the user name
        /// </summary>
        public const string UserNameParameter = "userName";

        /// <summary>
        /// The name of the parameter in the form post for the password
        /// </summary>
        public const string PasswordParameter = "password";

        private readonly object m_Lock = new object();

        private string m_AccessToken; //PROTECTED BY LOCK
        private bool m_AttemptingAuthentication; //PROTECTED BY LOCK

        /// <summary>
        /// Create a new set of customer credentials
        /// </summary>
        /// <param name="repositoryId">The owner Id to specify to the server (for example repository Id)</param>
        /// <param name="userName">The user's name</param>
        /// <param name="password">The user's password</param>
        /// <param name="savePassword">True if the user wants their password persisted</param>
        public UserCredentials(Guid repositoryId, string userName, string password, bool savePassword)
        {
            if (string.IsNullOrEmpty(userName))
                throw new ArgumentNullException(userName);

            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(password);

            if (repositoryId == Guid.Empty)
                throw new ArgumentOutOfRangeException(nameof(repositoryId), "The supplied repository Id is an empty guid, which can't be right.");

            RepositoryId = repositoryId;

            UserName = userName.Trim();
            Password = password.Trim();
            SavePassword = savePassword;
        }

        #region Public Properties and Methods

        /// <summary>
        /// The username to provide to the server
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// The password to provide to the server
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// True to have the credentials persisted locally.
        /// </summary>
        public bool SavePassword { get; set; }

        /// <summary>
        /// The owner Id to specify to the server (for example repository Id)
        /// </summary>
        public Guid RepositoryId { get; private set; }

        #endregion

        #region IWebAuthenticaitonProvider implementation

        /// <summary>
        /// Indicates if the authentication provider believes it has authenticated with the channel
        /// </summary>
        /// <remarks>If false then no logout will be attempted, and any request that requires authentication will
        /// cause a login attempt without waiting for an authentication failure.</remarks>
        public bool IsAuthenticated
        {
            get
            {
                bool isAuthenticated = false;

                //we have to always use a lock when handling the access token.
                lock (m_Lock)
                {
                    isAuthenticated = (string.IsNullOrEmpty(m_AccessToken) == false);
                    System.Threading.Monitor.PulseAll(m_Lock);
                }

                return isAuthenticated;
            }
        }

        /// <summary>
        /// indicates if the authentication provider can perform a logout
        /// </summary>
        bool IWebAuthenticationProvider.LogoutIsSupported => true;

        /// <summary>
        /// Perform a login on the supplied channel
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="client"></param>
        async Task IWebAuthenticationProvider.Login(WebChannel channel, HttpClient client)
        {
            //since we're async we have to do extra work to be sure we block while any other thread is
            //attempting to authenticate and we wait for the resolution.
            bool performAuthentication = false;
            lock(m_Lock)
            {
                if (m_AttemptingAuthentication == false)
                {
                    m_AccessToken = null;
                    performAuthentication = true;
                }

                System.Threading.Monitor.PulseAll(m_Lock);
            }

            if (performAuthentication)
            {
                try
                {
                    bool retry = false;
                    do
                    {
                        var parameters = new List<KeyValuePair<string, string>>();
                        parameters.Add(new KeyValuePair<string, string>(UserNameParameter, UserName));
                        parameters.Add(new KeyValuePair<string, string>(PasswordParameter, Password));
                        retry = false;

                        //post our credentials and get back our session key. 
                        try
                        {
                            var response = await client.PostAsync("Hub/Login", new FormUrlEncodedContent(parameters)).ConfigureAwait(false);
                            WebChannel.EnsureRequestSuccessful(response);

                            byte[] rawTokenBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                            lock(m_Lock)
                            {
                                m_AccessToken = Encoding.UTF8.GetString(rawTokenBytes);
                                System.Threading.Monitor.PulseAll(m_Lock);
                            }
                        }
                        catch (WebChannelAuthorizationException ex)
                        {
                            lock(m_Lock)
                            {
                                if (CachedCredentialsManager.UpdateCredentials(channel, RepositoryId, false))
                                {
                                    retry = true;
                                }
                                System.Threading.Monitor.PulseAll(m_Lock);
                            }
                            GC.KeepAlive(ex);

                            if (!retry)
                                throw;
                        }
                    } while (retry);
                }
                finally
                {
                    lock(m_Lock)
                    {
                        m_AttemptingAuthentication = false;
                    }
                }
            }
            else
            {
                //wait for the thread doing auth to complete...
                Trace.TraceInformation("Waiting on server authentication being performed on another thread\r\nEntry Uri: {0}", channel.EntryUri);

                bool isAuthenticated;
                lock(m_Lock)
                {
                    while (m_AttemptingAuthentication)
                    {
                        System.Threading.Monitor.Wait(m_Lock, 16);
                    }

                    isAuthenticated = IsAuthenticated;

                    System.Threading.Monitor.PulseAll(m_Lock);
                }

                if (isAuthenticated == false)
                {
                    Trace.TraceWarning("Authentication on other thread failed\r\nWe will presume this is a normal authorization exception and generate our own.\r\nEntry Uri: {0}", channel.EntryUri);
                    throw new WebChannelAuthorizationException("Authentication to server failed");
                }
            }
        }

        /// <summary>
        /// Perform a logout on the supplied channel
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="client"></param>
        Task IWebAuthenticationProvider.Logout(WebChannel channel, HttpClient client)
        {
            //we have to always use a lock when handling the access token.
            lock (m_Lock)
            {
                m_AccessToken = null;
                System.Threading.Monitor.PulseAll(m_Lock);
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Perform per-request authentication processing.
        /// </summary>
        /// <param name="channel">The channel object</param>
        /// <param name="client">The web client that is about to be used to execute the request.  It can't be used by the authentication provider to make requests.</param>
        /// <param name="request">The request that is about to be sent</param>
        /// <param name="resourceUrl">The resource URL (with query string) specified by the client.</param>
        /// <param name="requestSupportsAuthentication">Indicates if the request being processed supports authentication or not.</param>
        /// <remarks>If the request doesn't support authentication, it's a best practice to not provide any authentication information.</remarks>
        void IWebAuthenticationProvider.PreProcessRequest(WebChannel channel, HttpClient client, HttpRequestMessage request, string resourceUrl, bool requestSupportsAuthentication)
        {
            //figure out the effective relative URL.
            string fullUrl = resourceUrl;
            if (client.BaseAddress != null)
            {
                fullUrl = client.BaseAddress + resourceUrl;
            }

            //we're doing sets not adds to make sure we overwrite any existing value.
            if (requestSupportsAuthentication)
            {
                //the client doesn't like the colon, we have to bypass validation.
                request.Headers.TryAddWithoutValidation(RepositoryCredentials.AuthorizationHeader, AuthorizationPrefix + ": " + m_AccessToken);
                request.Headers.Add(RepositoryCredentials.ClientRepositoryHeader, RepositoryId.ToString());
            }
            else
            {
                //remove our repository header.
                request.Headers.Remove(RepositoryCredentials.ClientRepositoryHeader);
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("User Credentials - UserName: {0}  RepositoryId: {1}", UserName, RepositoryId);
        }

        #endregion

        #region Private Properties and Methods

        /// <summary>
        /// Calculates the effective hash given the provided salt text.
        /// </summary>
        /// <param name="saltText"></param>
        /// <returns></returns>
        private string CalculateHash(string saltText)
        {
            var encoder = new UTF8Encoding();
            byte[] buffer;

            //we have to always use a lock when handling the access token.
            lock (m_Lock)
            {
                buffer = encoder.GetBytes(m_AccessToken + saltText);
                System.Threading.Monitor.PulseAll(m_Lock);
            }

            using (var cryptoTransformSha1 = new SHA1CryptoServiceProvider())
            {
                return Convert.ToBase64String(cryptoTransformSha1.ComputeHash(buffer));
            }
        }

        #endregion
    }
}
