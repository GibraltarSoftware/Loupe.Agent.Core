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
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Gibraltar.Server.Client
{
    /// <summary>
    /// Customer-level credentials (used to authenticate to an SDS using the SDS shared secret)
    /// </summary>
    public sealed class SharedSecretAuthenticationProvider : IWebAuthenticationProvider
    {
        /// <summary>
        /// The prefix for the authorization header for this credential type
        /// </summary>
        public const string AuthorizationPrefix = "Gibraltar-Shared";

        /// <summary>
        /// Create a new set of customer credentials
        /// </summary>
        /// <param name="sharedSecret"></param>
        public SharedSecretAuthenticationProvider(string sharedSecret)
        {   
            if (string.IsNullOrEmpty(sharedSecret))
                throw new ArgumentNullException(sharedSecret);

            SharedSecret = sharedSecret.Trim();
        }

        #region Public Properties and Methods

        /// <summary>
        /// The shared secret assigned to the customer.
        /// </summary>
        public string SharedSecret { get; private set; }

        #endregion

        #region IWebAuthenticaitonProvider implementation

        /// <summary>
        /// Indicates if the authentication provider believes it has authenticated with the channel
        /// </summary>
        /// <remarks>If false then no logout will be attempted, and any request that requires authentication will
        /// cause a login attempt without waiting for an authentication failure.</remarks>
        public bool IsAuthenticated { get { return true; } }

        /// <summary>
        /// indicates if the authentication provider can perform a logout
        /// </summary>
        bool IWebAuthenticationProvider.LogoutIsSupported { get { return false; } }

        /// <summary>
        /// Perform a login on the supplied channel
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="client"></param>
        Task IWebAuthenticationProvider.Login(WebChannel channel, HttpClient client)
        {
            //nothing to do on login - we don't pre-auth.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Perform a logout on the supplied channel
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="client"></param>
        Task IWebAuthenticationProvider.Logout(WebChannel channel, HttpClient client)
        {
            //nothing to do on logout - we don't have state
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
            if (requestSupportsAuthentication == false)
                return;

            //figure out the effective relative URL.
            string fullUrl = resourceUrl;
            if (client.BaseAddress != null)
            {
                fullUrl = client.BaseAddress + resourceUrl;
            }

            var clientUri = new Uri(fullUrl);

            //we're doing sets not adds to make sure we overwrite any existing value.
            request.Headers.TryAddWithoutValidation(RepositoryCredentials.AuthorizationHeader, AuthorizationPrefix + ": " + CalculateHash(clientUri.PathAndQuery));
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
            UTF8Encoding encoder = new UTF8Encoding();
            byte[] buffer = encoder.GetBytes(SharedSecret + saltText);

            using (SHA1CryptoServiceProvider cryptoTransformSHA1 = new SHA1CryptoServiceProvider())
            {
                byte[] hash = cryptoTransformSHA1.ComputeHash(buffer);
                return Convert.ToBase64String(hash);
            }
        }

        #endregion    
    }
}
