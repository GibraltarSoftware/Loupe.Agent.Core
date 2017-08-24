using System;
using Gibraltar.Agent.Internal;

namespace Gibraltar.Agent
{
    /// <summary>
    /// Packages up session files collected on the local computer and sends them to a
    /// Loupe Server or anywhere via email or file transport with no user interface. For a
    /// user interface, see PackagerDialog.
    /// </summary>
    /// <remarks>
    /// 	<para>
    ///         The packager class can be used to package up session files synchronously or
    ///         asynchronously and send them via email or just store the results to a single,
    ///         compact file without any user interface.
    ///     </para>
    /// 	<para>When creating a package of sessions, the packager needs to know:</para>
    /// 	<list type="bullet">
    /// 		<item>
    /// 			<strong>What sessions to include:</strong> The <see cref="SessionCriteria">SessionCriteria</see> enumeration is used to indicate how
    ///             to select packages. It's a flag enumeration, allowing you to specify
    ///             multiple values at the same time.
    ///         </item>
    /// 		<item><strong>What to do with the package:</strong> It can be emailed or stored
    ///         as a local file for you to subsequently handle on your own.</item>
    /// 		<item><strong>Whether to mark sessions as read:</strong> Typically you'll want
    ///         to include sessions only on one package so that if a user generates a new
    ///         package later it won't contain sessions that have been completely sent before.
    ///         With this option set, Loupe will mark all of the sessions that it includes
    ///         in the package as long as the package is created successfully. If there is a
    ///         problem creating the package no sessions will be marked as sent.</item>
    /// 	</list>
    /// 	<para><strong>Asynchronous Usage</strong></para>
    /// 	<para>
    ///         The packager can function asynchronously by using the methods that end with
    ///         Async. To get result information for asynchronous operations you need to
    ///         subscribe to the <see cref="EndSend">EndSession</see> event.
    ///     </para>
    /// 	<para><strong>Sending the Active Session</strong></para>
    /// 	<para>When you specify Session Criteria that includes the ActiveSession several
    ///     unique things happen.</para>
    /// 	<list type="bullet">
    /// 		<item>To enable the session to be sent the current log file is ended and a new
    ///         one is started (just like if Log.EndFile was called).</item>
    /// 		<item>The session will not be marked as read as it is still being changed.
    ///         Therefore, it can still be sent later after it closes when the complete session
    ///         will be available.</item>
    /// 		<item>If the session has already been split into multiple files they will have
    ///         to be merged in memory first and then sent. Depending on the size of your
    ///         session data this may consume significant memory and processor time. As a
    ///         general rule of thumb it takes about 50 times the amount of memory as the
    ///         session file size because of the significant compression used when storing the
    ///         data.</item>
    /// 	</list>
    /// 	<para></para>
    /// </remarks>
    /// <example>
    /// 	<code lang="CS" title="Create and Send Package Via Email" description="The following example sends all of the session files for the product the current application is running as to the email address specified in the application config file. Once packaged, sessions are marked as read and won't be automatically included in future packages.">
    /// //Send an email with all of the information about the current application we haven't sent before.
    /// using(Packager packager = new Packager())
    /// {
    ///     packager.SendEmail(SessionCriteria.NewSessions, true, null);
    /// }
    /// </code>
    /// 	<code lang="CS" title="Create and Send Package to File" description="The following example packages all of the session files for the product the current application is running as into a local file. Once packaged, sessions are marked as read and won't be automatically included in future packages.">
    /// 		<![CDATA[
    /// //Send a file with all of the information about the current application we haven't sent before.
    /// using (Packager packager = new Packager())
    /// {
    ///     //the file name will automatically have the appropriate extension added, so it is specified 
    ///     //below without any file extension.  Typically you will want to generate a unique, temporary
    ///     //file name to store the data as instead of using a fixed file name.
    ///     packager.SendToFile(SessionCriteria.NewSessions, true, "C:\YourAppSessionData");
    /// }]]>
    /// 	</code>
    /// </example>
    public sealed class Packager : IDisposable
    {
        private readonly Gibraltar.Data.Packager m_Packager;

        private bool m_Disposed;
        
        /// <summary>
        /// Raised at the start of the packaging and sending process (after all input is collected)
        /// </summary>
        public event EventHandler BeginSend;

        /// <summary>
        /// Raised at the end of the packaging and sending process with completion status information.
        /// </summary>
        public event PackageSendEventHandler EndSend;

        /// <summary>
        /// Create a new packager for the current process.
        /// </summary>
        /// <example>
        /// 	<code lang="CS" title="Create Package" description="Create a new package for the current process to send an email with all data available.">
        /// //Send an email with all of the information about the current application we haven't sent before.
        /// using(Packager packager = new Packager())
        /// {
        ///     packager.SendEmail(SessionCriteria.AllSessions, true, null);
        /// }
        /// </code>
        /// </example>
        /// <remarks>Only sessions for the current product &amp; application will be considered</remarks>
        public Packager()
        {
            //We have to ping the log object to make sure everything has been initialized. (this might be the first thing ever done in this process with the agent)
            Monitor.Log.IsLoggingActive(false);

            m_Packager = new Gibraltar.Data.Packager();
            Initialize();
        }

        /// <summary>
        /// Create a new packager for the specified product.
        /// </summary>
        /// <param name="productName">The product to package sessions for.</param>
        /// <remarks>All applications for the specified product will be considered.</remarks>
        public Packager(string productName)
        {
            //We have to ping the log object to make sure everything has been initialized. (this might be the first thing ever done in this process with the agent)
            Monitor.Log.IsLoggingActive(false);

            //we aren't using our other overloads because the Packager class has its own logic for how things should override.
            m_Packager = new Gibraltar.Data.Packager(productName);
            Initialize();
        }

        /// <summary>
        /// Create a new packager for the specified product &amp; application.
        /// </summary>
        /// <param name="productName">The product to package sessions for.</param>
        /// <param name="applicationName"> Optional.  Restricts the packager to considering sessions for just this application within the specified product.</param>
        /// <remarks>If the application name is null then this will consider all applications for the specified product.</remarks>
        public Packager(string productName, string applicationName)
        {
            //We have to ping the log object to make sure everything has been initialized. (this might be the first thing ever done in this process with the agent)
            Monitor.Log.IsLoggingActive(false);

            //we aren't using our other overloads because the Packager class has its own logic for how things should override.
            m_Packager = new Gibraltar.Data.Packager(productName, applicationName);
            Initialize();
        }

        /// <summary>
        /// Create a new packager for the current process.
        /// </summary>
        /// <param name="productName">The product to package sessions for.</param>
        /// <param name="applicationName">Optional.  Restricts the packager to considering sessions for just this application within the specified product.</param>
        /// <param name="directory">Optional.  The log file directory on disk to look in for session files</param>
        /// <remarks>
        /// <para>If a repository folder is specified then only session files in that folder will be considered.</para>
        /// <para>If the application name is null then this will consider all applications for the specified product.</para>
        /// </remarks>
        public Packager(string productName, string applicationName = null, string directory = null)
        {
            //We have to ping the log object to make sure everything has been initialized. (this might be the first thing ever done in this process with the agent)
            Monitor.Log.IsLoggingActive(false);

            //we aren't using our other overloads because the Packager class has its own logic for how things should override.
            m_Packager = new Gibraltar.Data.Packager(productName, applicationName, directory);
            Initialize();
        }

        #region Public Properties and Methods

        /// <summary>
        /// The product name of the current running application this packager was initialized with.
        /// </summary>
        public string ProductName { get { return m_Packager.ProductName; } }

        /// <summary>
        /// The name of the current running application this packager was initialized with.
        /// </summary>
        public string ApplicationName { get { return m_Packager.ApplicationName; } }

        /// <summary>
        /// A caption for the resulting package
        /// </summary>
        public string Caption 
        { 
            get { return m_Packager.Caption; } 
            set { m_Packager.Caption = value; } 
        }

        /// <summary>
        /// A description for the resulting package.
        /// </summary>
        public string Description
        { 
            get { return m_Packager.Description; } 
            set { m_Packager.Description = value; } 
        }

        ///<summary>
        ///Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        ///</summary>
        ///<filterpriority>2</filterpriority>
        public void Dispose()
        {
            // Call the underlying implementation
            Dispose(true);

            // SuppressFinalize because there won't be anything left to finalize
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Write the completed package to the provided full file name (without extension)
        /// and path. The extension will be set to glp automatically.
        /// </summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.
        /// Any provided extension will be removed and replaced with the standard Loupe package extension.</remarks>
        /// <example>
        /// 	<code lang="CS">
        /// 		<![CDATA[
        /// //Send a file with all of the information about the current application we haven't sent before.
        /// using (Packager packager = new Packager())
        /// {
        ///     //the file name will automatically have the appropriate extension added, so it is specified 
        ///     //below without any file extension.  Typically you will want to generate a unique, temporary
        ///     //file name to store the data as instead of using a fixed file name.
        ///     packager.SendToFile(SessionCriteria.NewSessions, true, "C:\YourAppSessionData");
        /// }]]>
        /// 	</code>
        /// </example>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="fullFileNamePath">The file name and path to write the final package to.</param>
        /// <exception cref="GibraltarException">Thrown when generation or transmission fails, includes details on failure</exception>
        /// <exception cref="ArgumentNullException">A required parameter was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The provided file information is not a fully qualified file name and path.</exception>
        public void SendToFile(SessionCriteria sessions, bool markAsRead, string fullFileNamePath)
        {
            m_Packager.SendToFile((Gibraltar.Data.SessionCriteria)sessions, markAsRead, fullFileNamePath, false);
        }

        /// <summary>
        /// Write the completed package to the provided full file name (without extension)
        /// and path. The extension will be set to glp automatically.
        /// </summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.
        /// Any provided extension will be removed and replaced with the standard Loupe package extension.</remarks>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="fullFileNamePath">The file name and path to write the final package to.</param>
        /// <exception cref="GibraltarException">Thrown when generation or transmission fails, includes details on failure</exception>
        /// <exception cref="ArgumentNullException">A required parameter was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The provided file information is not a fully qualified file name and path.</exception>
        public void SendToFile(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead, string fullFileNamePath)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToFile(predicateAdapter.Predicate, markAsRead, fullFileNamePath, false);
        }

        /// <summary>
        /// Asynchronously write the completed package to the provided full file name
        /// (without extension) and path. The extension will be set to glp automatically.
        /// </summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.
        /// Any provided extension will be removed and replaced with the standard Loupe package extension.</remarks>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="fullFileNamePath">The file name and path to write the final package to.</param>
        /// <exception cref="ArgumentNullException">A required parameter was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The provided file information is not a fully qualified file name and path.</exception>
        public void SendToFileAsync(SessionCriteria sessions, bool markAsRead, string fullFileNamePath)
        {
            m_Packager.SendToFile((Gibraltar.Data.SessionCriteria)sessions, markAsRead, fullFileNamePath, true);
        }

        /// <summary>
        /// Asynchronously write the completed package to the provided full file name
        /// (without extension) and path. The extension will be set to glp automatically.
        /// </summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.
        /// Any provided extension will be removed and replaced with the standard Loupe package extension.</remarks>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="fullFileNamePath">The file name and path to write the final package to.</param>
        /// <exception cref="ArgumentNullException">A required parameter was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The provided file information is not a fully qualified file name and path.</exception>
        public void SendToFileAsync(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead, string fullFileNamePath)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToFile(predicateAdapter.Predicate, markAsRead, fullFileNamePath, true);
        }

        /// <summary>Send sessions to a Loupe Server using the current agent configuration</summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        /// <overloads>Send sessions to the Loupe Server</overloads>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(SessionCriteria sessions, bool markAsRead)
        {
            m_Packager.SendToServer((Gibraltar.Data.SessionCriteria)sessions, markAsRead, false, false, false, null, null, 0, false, null, null, false);
        }

        /// <summary>Send sessions to a Loupe Server using the current agent configuration</summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        /// <overloads>Send sessions to the Loupe Server</overloads>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToServer(predicateAdapter.Predicate, markAsRead, false, false, false, null, null, 0, false, null, null, false);
        }

        /// <summary>Send sessions to a Loupe Server using the current agent configuration</summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        /// <overloads>Send sessions to the Loupe Server</overloads>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(SessionCriteria sessions, bool markAsRead, bool purgeSentSessions)
        {
            m_Packager.SendToServer((Gibraltar.Data.SessionCriteria)sessions, markAsRead, purgeSentSessions, false, false, null, null, 0, false, null, null, false);
        }

        /// <summary>Send sessions to a Loupe Server using the current agent configuration</summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        /// <overloads>Send sessions to the Loupe Server</overloads>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead, bool purgeSentSessions)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToServer(predicateAdapter.Predicate, markAsRead, purgeSentSessions, false, false, null, null, 0, false, null, null, false);
        }

        /// <summary>
        /// Send sessions to the Loupe Service using the specified customer
        /// name.
        /// </summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        /// <overloads>Send sessions to the Loupe Server</overloads>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="customerName">The Loupe Service customer name.</param>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(SessionCriteria sessions, bool markAsRead, string customerName)
        {
            m_Packager.SendToServer((Gibraltar.Data.SessionCriteria)sessions, markAsRead, false, true, true, customerName, null, 0, false, null, null, false);
        }

        /// <summary>
        /// Send sessions to the Loupe Service using the specified customer
        /// name.
        /// </summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        /// <overloads>Send sessions to the Loupe Server</overloads>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="customerName">The Loupe Service customer name.</param>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead, string customerName)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToServer(predicateAdapter.Predicate, markAsRead, false, true, true, customerName, null, 0, false, null, null, false);
        }

        /// <summary>
        /// Send sessions to the Loupe Service using the specified customer
        /// name.
        /// </summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        /// <overloads>Send sessions to the Loupe Server</overloads>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <param name="customerName">The Loupe Service customer name.</param>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(SessionCriteria sessions, bool markAsRead, bool purgeSentSessions, string customerName)
        {
            m_Packager.SendToServer((Gibraltar.Data.SessionCriteria)sessions, markAsRead, purgeSentSessions, true, true, customerName, null, 0, false, null, null, false);
        }

        /// <summary>
        /// Send sessions to the Loupe Service using the specified customer
        /// name.
        /// </summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        /// <overloads>Send sessions to the Loupe Server</overloads>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <param name="customerName">The Loupe Service customer name.</param>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead, bool purgeSentSessions, string customerName)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToServer(predicateAdapter.Predicate, markAsRead, purgeSentSessions, true, true, customerName, null, 0, false, null, null, false);
        }

        /// <summary>
        /// Send sessions to a Loupe Server located at the specified web
        /// address
        /// </summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        /// <overloads>Send sessions to the Loupe Server</overloads>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="server">The full DNS name of the server where the service is located.</param>
        /// <param name="port"> An optional port number override for the server.</param>
        /// <param name="useSsl">Indicates if the connection should be encrypted with Ssl.</param>
        /// <param name="applicationBaseDirectory">The virtual directory on the host for the server.</param>
        /// <param name="repository">The specific repository on the server for a private server.</param>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(SessionCriteria sessions, bool markAsRead, string server, int port, bool useSsl, string applicationBaseDirectory, string repository)
        {
            m_Packager.SendToServer((Gibraltar.Data.SessionCriteria)sessions, markAsRead, false, true, false, null, server, port, useSsl, applicationBaseDirectory, repository, false);
        }

        /// <summary>
        /// Send sessions to a Loupe Server located at the specified web
        /// address
        /// </summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        /// <overloads>Send sessions to the Loupe Server</overloads>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="server">The full DNS name of the server where the service is located.</param>
        /// <param name="port"> An optional port number override for the server.</param>
        /// <param name="useSsl">Indicates if the connection should be encrypted with Ssl.</param>
        /// <param name="applicationBaseDirectory">The virtual directory on the host for the server.</param>
        /// <param name="repository">The specific repository on the server for a private server.</param>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead, string server, int port, bool useSsl, string applicationBaseDirectory, string repository)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToServer(predicateAdapter.Predicate, markAsRead, false, true, false, null, server, port, useSsl, applicationBaseDirectory, repository, false);
        }

        /// <summary>
        /// Send sessions to a Loupe Server located at the specified web
        /// address
        /// </summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        /// <overloads>Send sessions to the Loupe Server</overloads>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <param name="server">The full DNS name of the server where the service is located.</param>
        /// <param name="port"> An optional port number override for the server.</param>
        /// <param name="useSsl">Indicates if the connection should be encrypted with Ssl.</param>
        /// <param name="applicationBaseDirectory">The virtual directory on the host for the server.</param>
        /// <param name="repository">The specific repository on the server for a private server.</param>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(SessionCriteria sessions, bool markAsRead, bool purgeSentSessions, string server, int port, bool useSsl, string applicationBaseDirectory, string repository)
        {
            m_Packager.SendToServer((Gibraltar.Data.SessionCriteria)sessions, markAsRead, purgeSentSessions, true, false, null, server, port, useSsl, applicationBaseDirectory, repository, false);
        }

        /// <summary>
        /// Send sessions to a Loupe Server located at the specified web
        /// address
        /// </summary>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        /// <overloads>Send sessions to the Loupe Server</overloads>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <param name="server">The full DNS name of the server where the service is located.</param>
        /// <param name="port"> An optional port number override for the server.</param>
        /// <param name="useSsl">Indicates if the connection should be encrypted with Ssl.</param>
        /// <param name="applicationBaseDirectory">The virtual directory on the host for the server.</param>
        /// <param name="repository">The specific repository on the server for a private server.</param>
        /// <exception cref="GibraltarException">The server couldn't be contacted or there was a communication error.</exception>
        /// <exception cref="ArgumentException">The server configuration specified is invalid.</exception>
        public void SendToServer(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead, bool purgeSentSessions, string server, int port, bool useSsl, string applicationBaseDirectory, string repository)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToServer(predicateAdapter.Predicate, markAsRead, purgeSentSessions, true, false, null, server, port, useSsl, applicationBaseDirectory, repository, false);
        }

        /// <summary>
        /// Asynchronously send sessions to a Loupe Server using the current agent configuration
        /// </summary>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        public void SendToServerAsync(SessionCriteria sessions, bool markAsRead)
        {
            m_Packager.SendToServer((Gibraltar.Data.SessionCriteria)sessions, markAsRead, false, false, false, null, null, 0, false, null, null, true);
        }

        /// <summary>
        /// Asynchronously send sessions to a Loupe Server using the current agent configuration
        /// </summary>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        public void SendToServerAsync(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToServer(predicateAdapter.Predicate, markAsRead, false, false, false, null, null, 0, false, null, null, true);
        }

        /// <summary>
        /// Asynchronously send sessions to a Loupe Server using the current agent configuration
        /// </summary>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        public void SendToServerAsync(SessionCriteria sessions, bool markAsRead, bool purgeSentSessions)
        {
            m_Packager.SendToServer((Gibraltar.Data.SessionCriteria)sessions, markAsRead, purgeSentSessions, false, false, null, null, 0, false, null, null, true);
        }

        /// <summary>
        /// Asynchronously send sessions to a Loupe Server using the current agent configuration
        /// </summary>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        public void SendToServerAsync(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead, bool purgeSentSessions)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToServer(predicateAdapter.Predicate, markAsRead, purgeSentSessions, false, false, null, null, 0, false, null, null, true);
        }

        /// <summary>
        /// Asynchronously send sessions to the Gibraltar Loupe Service using the specified customer name
        /// </summary>
        /// <param name="customerName">The Loupe Service customer name</param>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        public void SendToServerAsync(SessionCriteria sessions, bool markAsRead, string customerName)
        {
            m_Packager.SendToServer((Gibraltar.Data.SessionCriteria)sessions, markAsRead, false, true, true, customerName, null, 0, false, null, null, true);
        }

        /// <summary>
        /// Asynchronously send sessions to the Gibraltar Loupe Service using the specified customer name
        /// </summary>
        /// <param name="customerName">The Loupe Service customer name</param>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        public void SendToServerAsync(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead, string customerName)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToServer(predicateAdapter.Predicate, markAsRead, false, true, true, customerName, null, 0, false, null, null, true);
        }

        /// <summary>
        /// Asynchronously send sessions to the Gibraltar Loupe Service using the specified customer name
        /// </summary>
        /// <param name="customerName">The Loupe Service customer name</param>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        public void SendToServerAsync(SessionCriteria sessions, bool markAsRead, bool purgeSentSessions, string customerName)
        {
            m_Packager.SendToServer((Gibraltar.Data.SessionCriteria)sessions, markAsRead, purgeSentSessions, true, true, customerName, null, 0, false, null, null, true);
        }

        /// <summary>
        /// Asynchronously send sessions to the Gibraltar Loupe Service using the specified customer name
        /// </summary>
        /// <param name="customerName">The Loupe Service customer name</param>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        public void SendToServerAsync(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead, bool purgeSentSessions, string customerName)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToServer(predicateAdapter.Predicate, markAsRead, purgeSentSessions, true, true, customerName, null, 0, false, null, null, true);
        }

        /// <summary>
        /// Asynchronously send sessions to a private Loupe Server located at the specified web address
        /// </summary>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="server">The full DNS name of the server where the service is located.</param>
        /// <param name="port"> An optional port number override for the server.</param>
        /// <param name="useSsl">Indicates if the connection should be encrypted with Ssl.</param>
        /// <param name="applicationBaseDirectory">The virtual directory on the host for the server.</param>
        /// <param name="repository">The specific repository on the server for a private server.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        public void SendToServerAsync(SessionCriteria sessions, bool markAsRead, string server, int port, bool useSsl, string applicationBaseDirectory, string repository)
        {
            m_Packager.SendToServer((Gibraltar.Data.SessionCriteria)sessions, markAsRead, false, true, false, null, server, port, useSsl, applicationBaseDirectory, repository, true);
        }

        /// <summary>
        /// Asynchronously send sessions to a private Loupe Server located at the specified web address
        /// </summary>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="server">The full DNS name of the server where the service is located.</param>
        /// <param name="port"> An optional port number override for the server.</param>
        /// <param name="useSsl">Indicates if the connection should be encrypted with Ssl.</param>
        /// <param name="applicationBaseDirectory">The virtual directory on the host for the server.</param>
        /// <param name="repository">The specific repository on the server for a private server.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        public void SendToServerAsync(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead, string server, int port, bool useSsl, string applicationBaseDirectory, string repository)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToServer(predicateAdapter.Predicate, markAsRead, false, true, false, null, server, port, useSsl, applicationBaseDirectory, repository, true);
        }

        /// <summary>
        /// Asynchronously send sessions to a private Loupe Server located at the specified web address
        /// </summary>
        /// <param name="sessions">The set of match rules to apply to sessions to determine what to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <param name="server">The full DNS name of the server where the service is located.</param>
        /// <param name="port"> An optional port number override for the server.</param>
        /// <param name="useSsl">Indicates if the connection should be encrypted with Ssl.</param>
        /// <param name="applicationBaseDirectory">The virtual directory on the host for the server.</param>
        /// <param name="repository">The specific repository on the server for a private server.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        public void SendToServerAsync(SessionCriteria sessions, bool markAsRead, bool purgeSentSessions, string server, int port, bool useSsl, string applicationBaseDirectory, string repository)
        {
            m_Packager.SendToServer((Gibraltar.Data.SessionCriteria)sessions, markAsRead, purgeSentSessions, true, false, null, server, port, useSsl, applicationBaseDirectory, repository, true);
        }

        /// <summary>
        /// Asynchronously send sessions to a private Loupe Server located at the specified web address
        /// </summary>
        /// <param name="sessionMatchPredicate">A delegate to evaluate sessions and determine which ones to send.</param>
        /// <param name="markAsRead">True to have every included session marked as read upon successful completion.</param>
        /// <param name="purgeSentSessions">True to have every included session removed from the local repository upon successful completion.</param>
        /// <param name="server">The full DNS name of the server where the service is located.</param>
        /// <param name="port"> An optional port number override for the server.</param>
        /// <param name="useSsl">Indicates if the connection should be encrypted with Ssl.</param>
        /// <param name="applicationBaseDirectory">The virtual directory on the host for the server.</param>
        /// <param name="repository">The specific repository on the server for a private server.</param>
        /// <remarks>The EndSend event will be raised when the send operation completes.  Because sessions are 
        /// sent one by one, they will be individually marked as read once sent.</remarks>
        public void SendToServerAsync(Predicate<SessionSummary> sessionMatchPredicate, bool markAsRead, bool purgeSentSessions, string server, int port, bool useSsl, string applicationBaseDirectory, string repository)
        {
            var predicateAdapter = new SessionSummaryPredicate(sessionMatchPredicate);
            m_Packager.SendToServer(predicateAdapter.Predicate, markAsRead, purgeSentSessions, true, false, null, server, port, useSsl, applicationBaseDirectory, repository, true);
        }

        #endregion

        #region Private Properties and Methods

        private void Initialize()
        {
            m_Packager.BeginSend += m_Packager_BeginSend;
            m_Packager.EndSend += m_Packager_EndSend;
        }

        /// <summary>
        /// Performs the actual releasing of managed and unmanaged resources.
        /// </summary>
        /// <remarks>
        /// Most usage should instead call Dispose(), which will call Dispose(true) for you
        /// and will suppress redundant finalization.</remarks>
        /// <param name="releaseManaged">Indicates whether to release managed resources.
        /// This should only be called with true, except from the finalizer which should call Dispose(false).</param>
        private void Dispose(bool releaseManaged)
        {
            if (!m_Disposed)
            {
                if (releaseManaged)
                {
                    // Free managed resources here (normal Dispose() stuff, which should itself call Dispose(true))
                    // Other objects may be referenced in this case
                    if (m_Packager != null)
                    {
                        m_Packager.Dispose();
                    }
                }
                // Free native resources here (alloc's, etc)
                // May be called from within the finalizer, so don't reference other objects here

                m_Disposed = true; // Make sure we don't do it more than once
            }
        }

        /// <summary>
        /// Called to raise the BeginSend event at the start of the packaging and sending process (after all input is collected)
        /// </summary>
        /// <remarks>If overriding this method, be sure to call Base.OnBeginSend to ensure that the event is still raised to its caller.</remarks>
        private void OnBeginSend()
        {
            //save the delegate field in a temporary field for thread safety
            EventHandler tempEvent = BeginSend;

            if (tempEvent != null)
            {
                tempEvent(this, new EventArgs());
            }
        }

        /// <summary>
        /// Called to raise the EndSend event at the end of the packaging and sending process with completion status information.
        /// </summary>
        /// <remarks>If overriding this method, be sure to call Base.OnBeginSend to ensure that the event is still raised to its caller.</remarks>
        private void OnEndSend(PackageSendEventArgs e)
        {
            //save the delegate field in a temporary field for thread safety
            PackageSendEventHandler tempEvent = EndSend;

            if (tempEvent != null)
            {
                tempEvent(this, e);
            }
        }

        #endregion

        #region Event Handlers

        private void m_Packager_EndSend(object sender, Gibraltar.Data.PackageSendEventArgs args)
        {
            //translate the args....
            PackageSendEventArgs wrapperArgs = new PackageSendEventArgs(args);
            OnEndSend(wrapperArgs);
        }

        private void m_Packager_BeginSend(object sender, EventArgs e)
        {
            OnBeginSend();
        }

        #endregion
    }

    /// <summary>
    /// Used to provide information on the status of a package send.
    /// </summary>
    /// <param name="sender">The packager object raising the event</param>
    /// <param name="e">The information on the package send event</param>
    public delegate void PackageSendEventHandler(object sender, PackageSendEventArgs e);
}
