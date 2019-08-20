using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using Loupe.Extensibility.Data;

namespace Loupe.Server.Client.Data
{
    /// <summary>
    /// Convert between data representations of common repository objects
    /// </summary>
    internal static class DataConverter
    {
        private const string LogCategory = "Loupe.Server.Data";

        /// <summary>
        /// Extract all of the data fields from a folder XML structure, validating it.
        /// </summary>
        /// <param name="folderXml"></param>
        /// <param name="id"></param>
        /// <param name="version"></param>
        /// <param name="deleted"></param>
        /// <param name="name"></param>
        /// <param name="parentFolderId"></param>
        /// <param name="typeName"></param>
        /// <param name="selectCriteriaXml"></param>
        /// <param name="invalidMessage"></param>
        /// <returns>True if the structure is valid, false otherwise.</returns>
        public static bool FromFolderXml(FolderXml folderXml, out Guid id, out long version, out bool deleted,
            out string name, out Guid? parentFolderId, out string typeName, out string selectCriteriaXml, out string invalidMessage)
        {
            bool valid = true;

            id = new Guid(folderXml.id);
            version = folderXml.version;
            deleted = folderXml.deleted;
            invalidMessage = string.Empty;
            name = null;
            typeName = null;
            parentFolderId = null;
            selectCriteriaXml = string.Empty;


            if (folderXml.folderDetail == null)
            {
                valid = false;
                invalidMessage = "No folder details were provided\r\n";
            }
            else
            {
                FolderDetailXml detail = folderXml.folderDetail;

                name = detail.name;
                if (string.IsNullOrEmpty(name))
                {
                    valid = false;
                    invalidMessage += "No folder name was provided \r\n";
                }

                if (string.IsNullOrEmpty(detail.parentFolderId) == false)
                {
                    //if there's anything then it HAS to be a valid GUID.
                    try
                    {
                        parentFolderId = new Guid(detail.parentFolderId);
                    }
                    catch (Exception ex)
                    {
                        GC.KeepAlive(ex);
                        valid = false;
                        invalidMessage += "The parent folder id wasn't valid\r\n";
                    }
                }

                FolderTypeXml folderType = detail.folderType;
                if (folderType == FolderTypeXml.search)
                {
                    selectCriteriaXml = detail.selectionCriteriaXml;
                    if (string.IsNullOrEmpty(selectCriteriaXml))
                    {
                        valid = false;
                        invalidMessage += "No criteria were specified for the search folder\r\n";
                    }
                }
                typeName = detail.folderType.ToString();
            }

            return valid;
        }

        /// <summary>
        /// Convert a byte array to a Server Configuration XML object without relying on XML Serializer
        /// </summary>
        /// <param name="rawData"></param>
        /// <returns></returns>
        public static HubConfigurationXml ByteArrayToHubConfigurationXml(byte[] rawData)
        {
            var configurationXml = new HubConfigurationXml();

            using (var documentStream = new MemoryStream(rawData))
            {
                XmlDocument xml = new XmlDocument();
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Ignore;

                using (var reader = XmlReader.Create(documentStream, settings))
                {
                    xml.Load(reader);

                    XmlNode hubConfigurationNode = xml.DocumentElement;

                    if (hubConfigurationNode == null)
                    {
                        throw new GibraltarException("There is no server configuration data in the provided raw data");
                    }

                    //read up our attributes
                    XmlAttribute attribute = hubConfigurationNode.Attributes["id"];
                    if (attribute != null)
                    {
                        configurationXml.id = attribute.InnerText;
                    }

                    attribute = hubConfigurationNode.Attributes["redirectRequested"];
                    if (attribute != null)
                    {
                        if (string.IsNullOrEmpty(attribute.InnerText) == false)
                        {
                            configurationXml.redirectRequested = bool.Parse(attribute.InnerText);
                        }
                    }

                    attribute = hubConfigurationNode.Attributes["status"];
                    if (attribute != null)
                    {
                        if (string.IsNullOrEmpty(attribute.InnerText) == false)
                        {
                            configurationXml.status =
                                (HubStatusXml) Enum.Parse(typeof(HubStatusXml), attribute.InnerText, true);
                        }
                    }

                    attribute = hubConfigurationNode.Attributes["timeToLive"];
                    if (attribute != null)
                    {
                        if (string.IsNullOrEmpty(attribute.InnerText) == false)
                        {
                            configurationXml.timeToLive = int.Parse(attribute.InnerText);
                        }
                    }

                    attribute = hubConfigurationNode.Attributes["protocolVersion"];
                    if (attribute != null)
                    {
                        configurationXml.protocolVersion = attribute.InnerText;
                    }

                    //we only read redirect information if we actually got a redirect request.
                    if (configurationXml.redirectRequested)
                    {
                        attribute = hubConfigurationNode.Attributes["redirectApplicationBaseDirectory"];
                        if (attribute != null)
                        {
                            configurationXml.redirectApplicationBaseDirectory = attribute.InnerText;
                        }

                        attribute = hubConfigurationNode.Attributes["redirectCustomerName"];
                        if (attribute != null)
                        {
                            configurationXml.redirectCustomerName = attribute.InnerText;
                        }

                        attribute = hubConfigurationNode.Attributes["redirectHostName"];
                        if (attribute != null)
                        {
                            configurationXml.redirectHostName = attribute.InnerText;
                        }

                        attribute = hubConfigurationNode.Attributes["redirectPort"];
                        if ((attribute != null) && (string.IsNullOrEmpty(attribute.InnerText) == false))
                        {
                            configurationXml.redirectPort = int.Parse(attribute.InnerText);
                            configurationXml.redirectPortSpecified = true;
                        }

                        attribute = hubConfigurationNode.Attributes["redirectUseGibraltarSds"];
                        if ((attribute != null) && (string.IsNullOrEmpty(attribute.InnerText) == false))
                        {
                            configurationXml.redirectUseGibraltarSds = bool.Parse(attribute.InnerText);
                            configurationXml.redirectUseGibraltarSdsSpecified = true;
                        }

                        attribute = hubConfigurationNode.Attributes["redirectUseSsl"];
                        if ((attribute != null) && (string.IsNullOrEmpty(attribute.InnerText) == false))
                        {
                            configurationXml.redirectUseSsl = bool.Parse(attribute.InnerText);
                            configurationXml.redirectUseSslSpecified = true;
                        }
                    }

                    //now move on to the child elements..  I'm avoiding XPath to avoid failure due to XML schema variations
                    if (hubConfigurationNode.HasChildNodes)
                    {
                        XmlNode expirationDtNode = null;
                        XmlNode publicKeyNode = null;
                        XmlNode liveStreamNode = null;
                        foreach (XmlNode curChildNode in hubConfigurationNode.ChildNodes)
                        {
                            if (curChildNode.NodeType == XmlNodeType.Element)
                            {
                                switch (curChildNode.Name)
                                {
                                    case "expirationDt":
                                        expirationDtNode = curChildNode;
                                        break;
                                    case "publicKey":
                                        publicKeyNode = curChildNode;
                                        break;
                                    case "liveStream":
                                        liveStreamNode = curChildNode;
                                        break;
                                    default:
                                        break;
                                }

                                if ((expirationDtNode != null) && (publicKeyNode != null) && (liveStreamNode != null))
                                    break;
                            }
                        }

                        if (expirationDtNode != null)
                        {
                            attribute = expirationDtNode.Attributes["DateTime"];
                            string dateTimeRaw = attribute.InnerText;

                            attribute = expirationDtNode.Attributes["Offset"];
                            string timeZoneOffset = attribute.InnerText;

                            if ((string.IsNullOrEmpty(dateTimeRaw) == false) &&
                                (string.IsNullOrEmpty(timeZoneOffset) == false))
                            {
                                configurationXml.expirationDt = new DateTimeOffsetXml();
                                configurationXml.expirationDt.DateTime = DateTime.Parse(dateTimeRaw);
                                configurationXml.expirationDt.Offset = int.Parse(timeZoneOffset);
                            }
                        }

                        if (publicKeyNode != null)
                        {
                            configurationXml.publicKey = publicKeyNode.InnerText;
                        }

                        if (liveStreamNode != null)
                        {
                            attribute = liveStreamNode.Attributes["agentPort"];
                            string agentPortRaw = attribute.InnerText;

                            attribute = liveStreamNode.Attributes["clientPort"];
                            string clientPortRaw = attribute.InnerText;

                            attribute = liveStreamNode.Attributes["useSsl"];
                            string useSslRaw = attribute.InnerText;

                            if ((string.IsNullOrEmpty(agentPortRaw) == false)
                                && (string.IsNullOrEmpty(clientPortRaw) == false)
                                && (string.IsNullOrEmpty(useSslRaw) == false))
                            {
                                configurationXml.liveStream = new LiveStreamServerXml();
                                configurationXml.liveStream.agentPort = int.Parse(agentPortRaw);
                                configurationXml.liveStream.clientPort = int.Parse(clientPortRaw);
                                configurationXml.liveStream.useSsl = bool.Parse(useSslRaw);
                            }
                        }
                    }
                }
            }

            return configurationXml;
        }

        /// <summary>
        /// Convert a byte array to sessions list XML object without relying on XML Serializer
        /// </summary>
        /// <param name="rawData"></param>
        /// <returns></returns>
        public static SessionsListXml ByteArrayToSessionsListXml(byte[] rawData)
        {
            SessionsListXml sessionsListXml = new SessionsListXml();
            using (MemoryStream inputStream = new MemoryStream(rawData))
            {
                using (TextReader textReader = new StreamReader(inputStream, Encoding.UTF8))
                {
                    using (XmlReader xmlReader = XmlReader.Create(textReader))
                    {
                        //to load up the first element.
                        if (xmlReader.ReadToFollowing("SessionsListXml") == false)
                        {
                            //it isn't a sessions list..
                            throw new InvalidDataException("The provided XML data is not a sessions list");
                        }

                        string sessionsVersionRaw = xmlReader.GetAttribute("version");
                        if (string.IsNullOrEmpty(sessionsVersionRaw) == false)
                        {
                            sessionsListXml.version = long.Parse(sessionsVersionRaw);
                        }

                        if (xmlReader.ReadToFollowing("sessions"))
                        {
                            //this is a repeating section so we have to be ready for that...
                            List<SessionXml> sessions = new List<SessionXml>();
                            bool moreSessions = true;
                            while (moreSessions)
                            {
                                xmlReader.ReadStartElement();
                                if (xmlReader.LocalName.Equals("session"))
                                {
                                    string guidRaw = xmlReader.GetAttribute("id");
                                    string versionRaw = xmlReader.GetAttribute("version");
                                    string deletedRaw = xmlReader.GetAttribute("deleted");

#if DEBUG
                                    if ((string.IsNullOrEmpty(guidRaw)) && (Debugger.IsAttached))
                                        Debugger.Break();
#endif

                                    //now convert to a SessionXml object and add to the item.
                                    SessionXml newSession = new SessionXml();
                                    newSession.id = guidRaw;
                                    newSession.version = long.Parse(versionRaw);
                                    newSession.deleted = bool.Parse(deletedRaw);
                                    sessions.Add(newSession);

                                    //and if this isn't an empty session element, we need to end it.
                                    if (xmlReader.IsEmptyElement == false)
                                        xmlReader.ReadEndElement();
                                }
                                else
                                {
                                    moreSessions = false;
                                }
                            }

                            sessionsListXml.sessions = sessions.ToArray();
                        }
                    }
                }
            }

            return sessionsListXml;
        }

        /// <summary>
        /// Convert a raw byte array to a session files list without using .NET XML Serialization
        /// </summary>
        /// <param name="rawData"></param>
        /// <returns></returns>
        public static SessionFilesListXml ByteArrayToSessionFilesListXml(byte[] rawData)
        {
            SessionFilesListXml sessionsFilesListXml = new SessionFilesListXml();
            using (MemoryStream inputStream = new MemoryStream(rawData))
            {
                using (TextReader textReader = new StreamReader(inputStream, Encoding.UTF8))
                {
                    using (XmlReader xmlReader = XmlReader.Create(textReader))
                    {
                        //to load up the first element.
                        if (xmlReader.ReadToFollowing("SessionFilesListXml") == false)
                        {
                            //it isn't a sessions list..
                            throw new InvalidDataException("The provided XML data is not a session files list");
                        }

                        string sessionsVersionRaw = xmlReader.GetAttribute("version");
                        if (string.IsNullOrEmpty(sessionsVersionRaw) == false)
                        {
                            sessionsFilesListXml.version = long.Parse(sessionsVersionRaw);
                        }

                        if (xmlReader.ReadToFollowing("files"))
                        {
                            //this is a repeating section so we have to be ready for that...
                            List<SessionFileXml> files = new List<SessionFileXml>();
                            bool moreFiles = true;
                            while (moreFiles)
                            {
                                xmlReader.ReadStartElement();
                                if (xmlReader.LocalName.Equals("file"))
                                {
                                    string guidRaw = xmlReader.GetAttribute("id");
                                    string sequenceRaw = xmlReader.GetAttribute("sequence");
                                    string versionRaw = xmlReader.GetAttribute("version");

#if DEBUG
                                    if (string.IsNullOrEmpty(guidRaw) && Debugger.IsAttached)
                                        Debugger.Break();
#endif

                                    //now convert to a SessionFileXml object and add to the item.
                                    SessionFileXml newFile = new SessionFileXml();
                                    newFile.id = guidRaw;
                                    newFile.sequence = int.Parse(sequenceRaw);
                                    newFile.version = long.Parse(versionRaw);
                                    files.Add(newFile);

                                    //and if this isn't an empty session element, we need to end it.
                                    if (xmlReader.IsEmptyElement == false)
                                        xmlReader.ReadEndElement();
                                }
                                else
                                {
                                    moreFiles = false;
                                }
                            }

                            sessionsFilesListXml.files = files.ToArray();
                        }
                    }
                }
            }

            return sessionsFilesListXml;
        }

        /// <summary>
        /// Converts a session XML object to a byte array without relying on XML Serializer
        /// </summary>
        /// <param name="sessionXml"></param>
        /// <returns></returns>
        public static byte[] SessionXmlToByteArray(SessionXml sessionXml)
        {
            using (MemoryStream outputStream = new MemoryStream(2048))
            {
                using (TextWriter textWriter = new StreamWriter(outputStream, Encoding.UTF8))
                {
                    using (XmlWriter xmlWriter = XmlWriter.Create(textWriter, new XmlWriterSettings()))
                    {
                        // Write XML using xmlWriter.  This has to be kept precisely in line with the XSD or we fail.
                        xmlWriter.WriteStartElement("SessionXml");

                        WriteXmlAttribute(xmlWriter, "id", sessionXml.id);
                        WriteXmlAttribute(xmlWriter, "version", sessionXml.version);
                        WriteXmlAttribute(xmlWriter, "deleted", sessionXml.deleted);

                        if (sessionXml.isCompleteSpecified)
                        {
                            WriteXmlAttribute(xmlWriter, "isComplete", sessionXml.isComplete);
                        }

                        //start the session detail
                        SessionDetailXml detailXml = sessionXml.sessionDetail;

                        if (detailXml != null)
                        {
                            xmlWriter.WriteStartElement("sessionDetail", "http://www.gibraltarsoftware.com/Gibraltar/Repository.xsd");

                            WriteXmlAttribute(xmlWriter, "productName", detailXml.productName);
                            WriteXmlAttribute(xmlWriter, "applicationName", detailXml.applicationName);
                            WriteXmlAttribute(xmlWriter, "environmentName", detailXml.environmentName);
                            WriteXmlAttribute(xmlWriter, "promotionLevelName", detailXml.promotionLevelName);
                            WriteXmlAttribute(xmlWriter, "applicationVersion", detailXml.applicationVersion);
                            WriteXmlAttribute(xmlWriter, "applicationType", detailXml.applicationType.ToString()); //enums won't auto-convert
                            WriteXmlAttribute(xmlWriter, "applicationDescription", detailXml.applicationDescription);
                            WriteXmlAttribute(xmlWriter, "caption", detailXml.caption);
                            WriteXmlAttribute(xmlWriter, "status", detailXml.status.ToString()); //enums won't auto-convert
                            WriteXmlAttribute(xmlWriter, "timeZoneCaption", detailXml.timeZoneCaption);
                            WriteXmlAttribute(xmlWriter, "durationSec", detailXml.durationSec);
                            WriteXmlAttribute(xmlWriter, "agentVersion", detailXml.agentVersion);
                            WriteXmlAttribute(xmlWriter, "userName", detailXml.userName);
                            WriteXmlAttribute(xmlWriter, "userDomainName", detailXml.userDomainName);
                            WriteXmlAttribute(xmlWriter, "hostName", detailXml.hostName);
                            WriteXmlAttribute(xmlWriter, "dnsDomainName", detailXml.dnsDomainName);
                            WriteXmlAttribute(xmlWriter, "isNew", detailXml.isNew);
                            WriteXmlAttribute(xmlWriter, "isComplete", detailXml.isComplete);
                            WriteXmlAttribute(xmlWriter, "messageCount", detailXml.messageCount);
                            WriteXmlAttribute(xmlWriter, "criticalMessageCount", detailXml.criticalMessageCount);
                            WriteXmlAttribute(xmlWriter, "errorMessageCount", detailXml.errorMessageCount);
                            WriteXmlAttribute(xmlWriter, "warningMessageCount", detailXml.warningMessageCount);
                            WriteXmlAttribute(xmlWriter, "updateUser", detailXml.updateUser);
                            WriteXmlAttribute(xmlWriter, "osPlatformCode", detailXml.osPlatformCode);
                            WriteXmlAttribute(xmlWriter, "osVersion", detailXml.osVersion);
                            WriteXmlAttribute(xmlWriter, "osServicePack", detailXml.osServicePack);
                            WriteXmlAttribute(xmlWriter, "osCultureName", detailXml.osCultureName);
                            WriteXmlAttribute(xmlWriter, "osArchitecture", detailXml.osArchitecture.ToString()); //enums won't auto-convert
                            WriteXmlAttribute(xmlWriter, "osBootMode", detailXml.osBootMode.ToString()); //enums won't auto-convert
                            WriteXmlAttribute(xmlWriter, "osSuiteMaskCode", detailXml.osSuiteMaskCode);
                            WriteXmlAttribute(xmlWriter, "osProductTypeCode", detailXml.osProductTypeCode);
                            WriteXmlAttribute(xmlWriter, "runtimeVersion", detailXml.runtimeVersion);
                            WriteXmlAttribute(xmlWriter, "runtimeArchitecture", detailXml.runtimeArchitecture.ToString()); //enums won't auto-convert
                            WriteXmlAttribute(xmlWriter, "currentCultureName", detailXml.currentCultureName);
                            WriteXmlAttribute(xmlWriter, "currentUiCultureName", detailXml.currentUiCultureName);
                            WriteXmlAttribute(xmlWriter, "memoryMb", detailXml.memoryMb);
                            WriteXmlAttribute(xmlWriter, "processors", detailXml.processors);
                            WriteXmlAttribute(xmlWriter, "processorCores", detailXml.processorCores);
                            WriteXmlAttribute(xmlWriter, "userInteractive", detailXml.userInteractive);
                            WriteXmlAttribute(xmlWriter, "terminalServer", detailXml.terminalServer);
                            WriteXmlAttribute(xmlWriter, "screenWidth", detailXml.screenWidth);
                            WriteXmlAttribute(xmlWriter, "screenHeight", detailXml.screenHeight);
                            WriteXmlAttribute(xmlWriter, "colorDepth", detailXml.colorDepth);
                            WriteXmlAttribute(xmlWriter, "commandLine", detailXml.commandLine);
                            WriteXmlAttribute(xmlWriter, "fileSize", detailXml.fileSize);
                            WriteXmlAttribute(xmlWriter, "fileAvailable", detailXml.fileAvailable);
                            WriteXmlAttribute(xmlWriter, "computerId", detailXml.computerId);
                          
                            //and now the elements
                            DateTimeOffsetXmlToXmlWriter(xmlWriter, "startDt", detailXml.startDt);
                            DateTimeOffsetXmlToXmlWriter(xmlWriter, "endDt", detailXml.endDt);
                            DateTimeOffsetXmlToXmlWriter(xmlWriter, "addedDt", detailXml.addedDt);
                            DateTimeOffsetXmlToXmlWriter(xmlWriter, "updatedDt", detailXml.updatedDt);

                            xmlWriter.WriteEndElement();
                        }

                        xmlWriter.WriteEndElement();

                        xmlWriter.Flush(); // to make sure it writes it all out now.

                        return outputStream.ToArray();
                    }
                }
            }            
        }

        private static void DateTimeOffsetXmlToXmlWriter(XmlWriter xmlWriter, string elementName, DateTimeOffsetXml dateTimeOffsetXml)
        {
            if (dateTimeOffsetXml == null)
                return;

            xmlWriter.WriteStartElement(elementName);
            WriteXmlAttribute(xmlWriter, "DateTime", dateTimeOffsetXml.DateTime);
            WriteXmlAttribute(xmlWriter, "Offset", dateTimeOffsetXml.Offset);
            xmlWriter.WriteEndElement();            
        }

        private static void DateTimeOffsetToXmlWriter(XmlWriter xmlWriter, string elementName, DateTimeOffset dateTimeOffset)
        {
            DateTimeOffsetXmlToXmlWriter(xmlWriter, elementName, ToDateTimeOffsetXml(dateTimeOffset));
        }


        /// <summary>
        /// convert a client repository set of information from its field form to XML
        /// </summary>
        /// <returns></returns>
        public static ClientRepositoryXml ToClientRepositoryXml(Guid id, string hostName, string computerKey, string statusName, DateTimeOffset addedDt, long currentSessionsVersion, string publicKey, DateTimeOffset lastContactDt)
        {
            ClientRepositoryXml newObject = new ClientRepositoryXml();

            newObject.id = id.ToString();
            newObject.addedDt = ToDateTimeOffsetXml(addedDt);
            newObject.computerKey = computerKey;
            newObject.currentSessionsVersion = currentSessionsVersion;
            newObject.hostName = hostName;
            newObject.lastContactDt = ToDateTimeOffsetXml(lastContactDt);
            newObject.publicKey = publicKey;
            newObject.status = ToClientRepositoryStatusXml(statusName);

            return newObject;
        }

        /// <summary>
        /// Create a single session XML object from its minimal raw information.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="version"></param>
        /// <param name="deleted"></param>
        /// <returns></returns>
        public static SessionXml ToSessionXml(Guid id, long version, bool deleted)
        {
            SessionXml newObject = new SessionXml();

            newObject.id = id.ToString();
            newObject.version = version;
            newObject.deleted = deleted;

            return newObject;
        }

        /// <summary>
        /// Create a sessionXml from the session summary provided
        /// </summary>
        /// <returns></returns>
        public static SessionXml ToSessionXml(ISessionSummary sessionSummary)
        {
            if (sessionSummary == null)
                throw new ArgumentNullException(nameof(sessionSummary));


            SessionXml newObject = ToSessionXml(sessionSummary.Id, sessionSummary.ComputerId, 0, false, false, true, sessionSummary.StartDateTime, sessionSummary.EndDateTime, sessionSummary.FullyQualifiedUserName,
                                                                sessionSummary.Product, sessionSummary.Application, sessionSummary.Environment,
                                                                sessionSummary.PromotionLevel, sessionSummary.ApplicationVersion.ToString(), sessionSummary.ApplicationType.ToString(),
                                                                sessionSummary.ApplicationDescription, sessionSummary.Caption, sessionSummary.Status.ToString(),
                                                                sessionSummary.TimeZoneCaption, sessionSummary.StartDateTime, sessionSummary.EndDateTime, Convert.ToInt32(sessionSummary.Duration.TotalSeconds),
                                                                sessionSummary.AgentVersion.ToString(), sessionSummary.UserName, sessionSummary.UserDomainName,
                                                                sessionSummary.HostName, sessionSummary.DnsDomainName, sessionSummary.MessageCount, sessionSummary.CriticalCount,
                                                                sessionSummary.ErrorCount, sessionSummary.WarningCount, sessionSummary.OSPlatformCode,
                                                                sessionSummary.OSVersion.ToString(), sessionSummary.OSServicePack, sessionSummary.OSCultureName, sessionSummary.OSArchitecture.ToString(),
                                                                sessionSummary.OSBootMode.ToString(), sessionSummary.OSSuiteMask, sessionSummary.OSProductType, sessionSummary.RuntimeVersion.ToString(),
                                                                sessionSummary.RuntimeArchitecture.ToString(), sessionSummary.CurrentCultureName, sessionSummary.CurrentUICultureName,
                                                                sessionSummary.MemoryMB, sessionSummary.Processors, sessionSummary.ProcessorCores, sessionSummary.UserInteractive,
                                                                sessionSummary.TerminalServer, sessionSummary.ScreenWidth, sessionSummary.ScreenHeight, sessionSummary.ColorDepth,
                                                                sessionSummary.CommandLine, false, 0, sessionSummary.Properties);

            return newObject;
        }


        /// <summary>
        /// Create a single session XML object from its detail information
        /// </summary>
        public static SessionXml ToSessionXml(Guid id, Guid? computerId, long version, bool deleted, 
                bool IsComplete, bool IsNew, DateTimeOffset AddedDt, DateTimeOffset UpdatedDt,
                string UpdatedUser, string ProductName, string ApplicationName, 
                string environmentName, string promotionLevelName, string ApplicationVersion,
                string ApplicationTypeName, string ApplicationDescription, string Caption, string StatusName,
                string TimeZoneCaption, DateTimeOffset StartDt, DateTimeOffset EndDt, int DurationSec,
                string AgentVersion, string UserName, string UserDomainName, string HostName,
                string DNSDomainName, int MessageCount, int CriticalMessageCount, int ErrorMessageCount,
                int WarningMessageCount, int OSPlatformCode, string OSVersion, string OSServicePack,
                string OSCultureName, string OSArchitectureName, string OSBootModeName, int OSSuiteMaskCode,
                int OSProductTypeCode, string RuntimeVersion, string RuntimeArchitectureName, string CurrentCultureName,
                string CurrentUICultureName, int MemoryMB, int Processors, int ProcessorCores, bool UserInteractive,
                bool TerminalServer, int ScreenWidth, int ScreenHeight, int ColorDepth, string CommandLine, 
                bool fileAvailable, int fileSize, IDictionary<string, string> properties)
        {
            SessionXml newObject = ToSessionXml(id, version, deleted);

            SessionDetailXml newDetailObject = new SessionDetailXml();

            newDetailObject.addedDt = ToDateTimeOffsetXml(AddedDt);
            newDetailObject.agentVersion = AgentVersion;
            newDetailObject.applicationDescription = ApplicationDescription;
            newDetailObject.applicationName = ApplicationName;
            newDetailObject.environmentName = environmentName;
            newDetailObject.promotionLevelName = promotionLevelName;
            newDetailObject.applicationType = ToApplicationTypeXml(ApplicationTypeName);
            newDetailObject.applicationVersion = ApplicationVersion;
            newDetailObject.caption = Caption;
            newDetailObject.colorDepth = ColorDepth;
            newDetailObject.commandLine = CommandLine;
            newDetailObject.computerId = computerId.HasValue ? computerId.ToString() : null;
            newDetailObject.criticalMessageCount = CriticalMessageCount;
            newDetailObject.currentCultureName = CurrentCultureName;
            newDetailObject.currentUiCultureName = CurrentUICultureName;
            newDetailObject.dnsDomainName = DNSDomainName;
            newDetailObject.durationSec = DurationSec;
            newDetailObject.endDt = ToDateTimeOffsetXml(EndDt);
            newDetailObject.errorMessageCount = ErrorMessageCount;
            newDetailObject.fileAvailable = fileAvailable;
            newDetailObject.fileSize = fileSize;
            newDetailObject.hostName = HostName;
            newDetailObject.isComplete = IsComplete;
            newDetailObject.isNew = IsNew;
            newDetailObject.memoryMb = MemoryMB;
            newDetailObject.messageCount = MessageCount;
            newDetailObject.osArchitecture = ToProcessorArchitectureXml(OSArchitectureName);
            newDetailObject.osBootMode = ToBootModeXml(OSBootModeName);
            newDetailObject.osCultureName = OSCultureName;
            newDetailObject.osPlatformCode = OSPlatformCode;
            newDetailObject.osProductTypeCode = OSProductTypeCode;
            newDetailObject.osServicePack = OSServicePack;
            newDetailObject.osSuiteMaskCode = OSSuiteMaskCode;
            newDetailObject.osVersion = OSVersion;
            newDetailObject.processorCores = ProcessorCores;
            newDetailObject.processors = Processors;
            newDetailObject.productName = ProductName;
            newDetailObject.runtimeArchitecture = ToProcessorArchitectureXml(RuntimeArchitectureName);
            newDetailObject.runtimeVersion = RuntimeVersion;
            newDetailObject.screenHeight = ScreenHeight;
            newDetailObject.screenWidth = ScreenWidth;
            newDetailObject.startDt = ToDateTimeOffsetXml(StartDt);
            newDetailObject.status = ToSessionStatusXml(StatusName);
            newDetailObject.terminalServer = TerminalServer;
            newDetailObject.timeZoneCaption = TimeZoneCaption;
            newDetailObject.updatedDt = ToDateTimeOffsetXml(UpdatedDt);
            newDetailObject.updateUser = UpdatedUser;
            newDetailObject.userDomainName = UserDomainName;
            newDetailObject.userInteractive = UserInteractive;
            newDetailObject.userName = UserName;
            newDetailObject.warningMessageCount = WarningMessageCount;
            newDetailObject.properties = ToSessionPropertiesXml(properties);
            newObject.sessionDetail = newDetailObject;

            return newObject;            
        }

        ///<summary>
        /// Convert a properties dictionary to a session property XML array
        ///</summary>
        ///<param name="properties"></param>
        ///<returns></returns>
        public static SessionPropertyXml[] ToSessionPropertiesXml(IDictionary<string, string> properties)
        {
            if ((properties == null) || (properties.Count == 0))
                return null;

            List<SessionPropertyXml> propertiesXml = new List<SessionPropertyXml>(properties.Count);

            foreach (var property in properties)
            {                
                var currentProperty = ToSessionPropertyXml(Guid.NewGuid(), property.Key, property.Value); //we are banking on it not actually using that ID any more....
                propertiesXml.Add(currentProperty);
            }

            return propertiesXml.ToArray();
        }


        /// <summary>
        /// Create a single session property object from its raw data elements.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static SessionPropertyXml ToSessionPropertyXml(Guid id, string name, string value)
        {
            SessionPropertyXml newObject = new SessionPropertyXml();

            newObject.id = id.ToString();
            newObject.name = name;
            newObject.value = value;

            return newObject;
        }

        /// <summary>
        /// Create a single session file object from its raw data elements.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sequence"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static SessionFileXml ToSessionFileXml(Guid id, int sequence, long version)
        {
            SessionFileXml newObject = new SessionFileXml();

            newObject.id = id.ToString();
            newObject.sequence = sequence;
            newObject.version = version;

            return newObject;
        }

        /// <summary>
        /// Convert an application type to its XML equivalent.
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public static ApplicationTypeXml ToApplicationTypeXml(string typeName)
        {
            return (ApplicationTypeXml)Enum.Parse(typeof(ApplicationTypeXml), typeName, true);
        }

        /// <summary>
        /// Convert a boot mode to its XML equivalent
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public static BootModeXml ToBootModeXml(string typeName)
        {
            return (BootModeXml)Enum.Parse(typeof(BootModeXml), typeName, true);
        }

        /// <summary>
        /// Convert a boot mode to its XML equivalent
        /// </summary>
        /// <param name="statusName"></param>
        /// <returns></returns>
        public static ClientRepositoryStatusXml ToClientRepositoryStatusXml(string statusName)
        {
            return (ClientRepositoryStatusXml)Enum.Parse(typeof(ClientRepositoryStatusXml), statusName, true);
        }

        /// <summary>
        /// Convert a folder type to its XML equivalent
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public static FolderTypeXml ToFolderType(string typeName)
        {
            return (FolderTypeXml)Enum.Parse(typeof(FolderTypeXml), typeName, true);
        }

        /// <summary>
        /// Convert a processor architecture to its XML equivalent.
        /// </summary>
        /// <param name="statusName"></param>
        /// <returns></returns>
        public static ProcessorArchitectureXml ToProcessorArchitectureXml(string statusName)
        {
            return (ProcessorArchitectureXml)Enum.Parse(typeof(ProcessorArchitectureXml), statusName, true);
        }

        /// <summary>
        /// Convert a session status to its XML equivalent. 
        /// </summary>
        /// <param name="statusName"></param>
        /// <returns></returns>
        public static SessionStatusXml ToSessionStatusXml(string statusName)
        {
            return (SessionStatusXml)Enum.Parse(typeof(SessionStatusXml), statusName, true);
        }

        /// <summary>
        /// Convert a DateTimeOffset value to its XML equivalent.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static DateTimeOffsetXml ToDateTimeOffsetXml(DateTimeOffset dateTime)
        {
            DateTimeOffsetXml newObject = new DateTimeOffsetXml();
            newObject.DateTime = dateTime.DateTime;
            newObject.Offset = (int)dateTime.Offset.TotalMinutes;

            return newObject;
        }

        /// <summary>
        /// Convert the DateTimeOffset XML structure to its native form
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static DateTimeOffset FromDateTimeOffsetXml(DateTimeOffsetXml dateTime)
        {
            if (dateTime == null)
                return DateTimeOffset.MinValue;

            var sourceDateTime = dateTime.DateTime;
            if (sourceDateTime.Kind != DateTimeKind.Unspecified)
                sourceDateTime = DateTime.SpecifyKind(sourceDateTime, DateTimeKind.Unspecified); //otherwise the DTO onstructor will fail.

            return new DateTimeOffset(sourceDateTime, new TimeSpan(0, dateTime.Offset, 0));
        }


        /// <summary>
        /// Convert a log message severity to its XML equivalent.
        /// </summary>
        /// <param name="severityName"></param>
        /// <returns></returns>
        public static LogMessageSeverityXml ToLogMessageSeverityXml(string severityName)
        {
            return (LogMessageSeverityXml)Enum.Parse(typeof(LogMessageSeverityXml), severityName, true);
        }

        /// <summary>
        /// Convert a log message severity XML to its native form
        /// </summary>
        /// <param name="severityName"></param>
        /// <returns></returns>
        public static LogMessageSeverity FromLogMessageSeverityXml(string severityName)
        {
            return (LogMessageSeverity)Enum.Parse(typeof(LogMessageSeverity), severityName, true);
        }

        ///<summary>
        /// Convert the provided processor architecture to our normal enumeration
        ///</summary>
        ///<param name="architectureXml"></param>
        ///<returns></returns>
        public static ProcessorArchitecture FromProcessorArchitectureXml(ProcessorArchitectureXml architectureXml)
        {
            return (ProcessorArchitecture)Enum.Parse(typeof(ProcessorArchitecture), architectureXml.ToString(), true);
        }

        /// <summary>
        /// Convert the provided bot mode to our normal enumeration
        /// </summary>
        /// <param name="bootModeXml"></param>
        /// <returns></returns>
        public static OSBootMode FromBootModeXml(BootModeXml bootModeXml)
        {
            return (OSBootMode)Enum.Parse(typeof(OSBootMode), bootModeXml.ToString(), true);
        }

        private static void WriteXmlAttribute(XmlWriter xmlWriter, string name, string value)
        {
            if (value == null)
                return; //the correct way to indicate a null 

            xmlWriter.WriteStartAttribute(name);
            xmlWriter.WriteValue(value);
            xmlWriter.WriteEndAttribute();
        }

        private static void WriteXmlAttribute(XmlWriter xmlWriter, string name, long value)
        {
            xmlWriter.WriteStartAttribute(name);
            xmlWriter.WriteValue(value);
            xmlWriter.WriteEndAttribute();
        }

        private static void WriteXmlAttribute(XmlWriter xmlWriter, string name, bool value)
        {
            xmlWriter.WriteStartAttribute(name);
            xmlWriter.WriteValue(value);
            xmlWriter.WriteEndAttribute();
        }

        private static void WriteXmlAttribute(XmlWriter xmlWriter, string name, DateTime value)
        {
            xmlWriter.WriteStartAttribute(name);
            xmlWriter.WriteValue(value);
            xmlWriter.WriteEndAttribute();
        }
    }
}
