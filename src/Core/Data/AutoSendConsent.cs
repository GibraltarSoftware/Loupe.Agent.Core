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
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Gibraltar.Monitor;
using Gibraltar.Server.Client.Data;
using Loupe.Extensibility.Data;

#endregion

namespace Gibraltar.Data
{
    /// <summary>
    /// The end-user consent for the local computer and a specific product or product and application pair
    /// </summary>
    [DebuggerDisplay("Opt In: {OptIn} Selection Made: {SelectionMade} Id: {Id}")]
    public class AutoSendConsent
    {
        /// <summary>
        /// The log category for auto send consent data.
        /// </summary>
        public const string LogCategory = "Loupe.Agent.Consent";

        /// <summary>
        /// Construct a new fully specified auto-send consent
        /// </summary>
        /// <param name="id"></param>
        /// <param name="productName"></param>
        /// <param name="applicationName"></param>
        /// <param name="applicationVersion"></param>
        /// <param name="userPrompts"></param>
        /// <param name="selectionMade"></param>
        /// <param name="optIn"></param>
        /// <param name="includeDetails"></param>
        /// <param name="updatedDt"></param>
        /// <param name="updatedUser"></param>
        public AutoSendConsent(Guid id, string productName, string applicationName, Version applicationVersion, int userPrompts, bool selectionMade, bool optIn, bool includeDetails, DateTimeOffset updatedDt, string updatedUser)
        {
            Id = id;
            ProductName = productName;
            ApplicationName = applicationName;
            ApplicationVersion = applicationVersion;
            UserPrompts = userPrompts;
            SelectionMade = selectionMade;
            OptIn = optIn;
            IncludeDetails = includeDetails;
            UpdatedDt = updatedDt;
            UpdatedUser = updatedUser;
        }

        internal AutoSendConsent(string productName, string applicationName, Version applicationVersion, string auditUser)
        {
            Id = Guid.NewGuid();
            ProductName = productName;
            ApplicationName = applicationName;
            ApplicationVersion = applicationVersion;
            UserPrompts = 0;
            SelectionMade = false;
            OptIn = false;
            UpdatedDt = DateTimeOffset.Now;
            UpdatedUser = auditUser;
            IsNew = true;
        }

        #region Public Properties and Methods

        /// <summary>
        /// The unique id of the underlying consent
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// The product name that consent was recorded for
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// Optional.  The application within the product that consent was recorded for.
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// The version of the application when consent was last recorded
        /// </summary>
        public Version ApplicationVersion { get; set; }

        /// <summary>
        /// The number of times the user has been prompted to make a decision for this version.
        /// </summary>
        public int UserPrompts { get; set; }

        /// <summary>
        /// True if the user has made a selection (and the OptIn value is now valid) or false if no decision has been made yet.
        /// </summary>
        public bool SelectionMade { get; set; }

        /// <summary>
        /// True to opt into auto send, false to opt out.
        /// </summary>
        public bool OptIn { get; set; }

        /// <summary>
        /// True to include details in the auto send, false to only send summary, anonymous information
        /// </summary>
        public bool IncludeDetails { get; set; }

        /// <summary>
        /// The date &amp; time the consent was last updated
        /// </summary>
        public DateTimeOffset UpdatedDt { get; set; }

        /// <summary>
        /// The full user name that recorded the last update
        /// </summary>
        public string UpdatedUser { get; set; }

        /// <summary>
        /// Indicates if this is a newly created consent and not yet recorded.
        /// </summary>
        public bool IsNew { get; private set; }

        /// <summary>
        /// Attempts to load the autosend consent from the specified file name &amp; path.  Returns null if the consent could not be loaded
        /// </summary>
        /// <param name="fileNamePath"></param>
        /// <returns></returns>
        public static AutoSendConsent LoadFile(string fileNamePath)
        {
            AutoSendConsent consent = null;

            using(FileStream stream = FileHelper.OpenFileStream(fileNamePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream != null)
                {
                    try
                    {
                        byte[] fileContents = new byte[10800];
                        stream.Read(fileContents, 0, 10800);
                        consent = DataConverter.ByteArrayToAutoSendConsent(fileContents);
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        Log.Write(LogMessageSeverity.Error, LogWriteMode.Queued, ex, LogCategory, "Unable to load auto send consent", "While reloading the file from disk an exception was thrown.\r\nException: {0}", ex.Message);
#endif
                        GC.KeepAlive(ex);
                    }
                }
            }

            return consent;
        }

        /// <summary>
        /// Save the state of the consent to the provided file name &amp; path, overwriting anything there.
        /// </summary>
        /// <param name="fileNamePath"></param>
        public void Save(string fileNamePath)
        {
            //in case there are many threads contending we're going to give it a few shots...
            int fileAttempts = 0;
            while (fileAttempts < 4)
            {
                using (var stream = FileHelper.OpenFileStream(fileNamePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    if (stream == null)
                    {
                        fileAttempts++;
                        Thread.Sleep(16);
                    }
                    else
                    {
                        byte[] rawData = DataConverter.AutoSendConsentToByteArray(this);
                        stream.SetLength(0);
                        stream.Write(rawData, 0, rawData.Length);
                        break; //and done!
                    }
                }
            }
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            StringBuilder output = new StringBuilder(1024);

            if (SelectionMade)
            {
                output.AppendFormat("Selection: {0}\r\n", OptIn ? "Opt In" : "Opt Out");

                if (OptIn)
                {
                    output.AppendFormat("Level: {0}\r\n", IncludeDetails ? "Include All" : "Anonymous Summary");
                }
            }
            else
            {
                output.AppendFormat("No opt in selection made.  User has been prompted {0} times.\r\n", UserPrompts);
            }

            output.AppendFormat("Applies to:\r\n  Product: {0}\r\n", ProductName);
            
            if (string.IsNullOrEmpty(ApplicationName) == false)
            {
                output.AppendFormat("  Application: {0}\r\n", ApplicationName);                
            }

            output.AppendFormat("Last Updated {0:G} by {1}\r\n", UpdatedDt, UpdatedUser);

            return output.ToString();
        }

        /// <summary>
        /// Indicates if the specified consent contains a selection or not.
        /// </summary>
        /// <param name="consent"></param>
        /// <returns></returns>
        public static bool IsNullOrNoSelection(AutoSendConsent consent)
        {
            return ((consent == null) || (consent.SelectionMade == false));
        }

        #endregion
    }
}
