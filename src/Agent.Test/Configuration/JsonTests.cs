using System;
using System.IO;
using Loupe.Configuration;
using Loupe.Extensibility.Data;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Loupe.Agent.Test.Configuration
{
    [TestFixture]
    public class JsonTests
    {
        private string _configFileNamePath;

        [SetUp]
        public void CreateJsonFile()
        {
            _configFileNamePath = Path.GetFullPath("loupe.json");
            using (var writer = new StreamWriter(File.Create(_configFileNamePath)))
            {
                writer.Write(JsonSource);
            }
        }

        [TearDown]
        public void DeleteJsonFile()
        {
            File.Delete(_configFileNamePath);
        }

        [Test]
        public void ListenerValues()
        {
            var actual = Load();
            Assert.False(actual.Listener.AutoTraceRegistration);
            Assert.False(actual.Listener.EnableConsole);
            Assert.False(actual.Listener.EnableNetworkEvents);
            Assert.False(actual.Listener.EndSessionOnTraceClose);
        }

        [Test]
        public void SessionFileValues()
        {
            var actual = Load();
            Assert.False(actual.SessionFile.Enabled);
            Assert.AreEqual(42, actual.SessionFile.AutoFlushInterval);
            Assert.AreEqual(42, actual.SessionFile.IndexUpdateInterval);
            Assert.AreEqual(42, actual.SessionFile.MaxFileSize);
            Assert.AreEqual(42, actual.SessionFile.MaxFileDuration);
            Assert.False(actual.SessionFile.EnableFilePruning);
            Assert.AreEqual(42, actual.SessionFile.MaxLocalDiskUsage);
            Assert.AreEqual(42, actual.SessionFile.MaxLocalFileAge);
            Assert.AreEqual(42, actual.SessionFile.MinimumFreeDisk);
            Assert.True(actual.SessionFile.ForceSynchronous);
            Assert.AreEqual(42, actual.SessionFile.MaxQueueLength);
            Assert.AreEqual("C:\\Temp", actual.SessionFile.Folder);
        }

        [Test]
        public void PackagerValues()
        {
            var actual = Load();
            Assert.AreEqual("Ctrl-Alt-F5", actual.Packager.HotKey);
            Assert.False(actual.Packager.AllowFile);
            Assert.False(actual.Packager.AllowRemovableMedia);
            Assert.False(actual.Packager.AllowEmail);
            Assert.False(actual.Packager.AllowServer);
            Assert.AreEqual("mark@rendlelabs.com", actual.Packager.FromEmailAddress);
            Assert.AreEqual("mark@rendlelabs.com", actual.Packager.DestinationEmailAddress);
            Assert.AreEqual("Sample", actual.Packager.ProductName);
            Assert.AreEqual("Loupe", actual.Packager.ApplicationName);
        }

        [Test]
        public void PublisherValues()
        {
            var actual = Load();
            Assert.AreEqual("Sample", actual.Publisher.ProductName);
            Assert.AreEqual("Sample App", actual.Publisher.ApplicationDescription);
            Assert.AreEqual("Loupe", actual.Publisher.ApplicationName);
            Assert.AreEqual(ApplicationType.AspNet, actual.Publisher.ApplicationType);
            Assert.AreEqual(new Version(42,0,0), actual.Publisher.ApplicationVersion);
            Assert.AreEqual("Development", actual.Publisher.EnvironmentName);
            Assert.AreEqual("QA", actual.Publisher.PromotionLevelName);
            Assert.True(actual.Publisher.ForceSynchronous);
            Assert.AreEqual(10, actual.Publisher.MaxQueueLength);
            Assert.True(actual.Publisher.EnableAnonymousMode);
            Assert.True(actual.Publisher.EnableDebugMode);
        }

        [Test]
        public void ServerValues()
        {
            var actual = Load();
            Assert.False(actual.Server.Enabled);
            Assert.True(actual.Server.AutoSendSessions);
            Assert.False(actual.Server.AutoSendOnError);
            Assert.True(actual.Server.SendAllApplications);
            Assert.True(actual.Server.PurgeSentSessions);
            Assert.AreEqual("RendleLabs", actual.Server.CustomerName);
            Assert.True(actual.Server.UseGibraltarService);
            Assert.True(actual.Server.UseSsl);
            Assert.AreEqual("onloupe.com", actual.Server.Server);
            Assert.AreEqual(81, actual.Server.Port);
            Assert.AreEqual("C:\\inetpub\\foo", actual.Server.ApplicationBaseDirectory);
            Assert.AreEqual("quux", actual.Server.Repository);
        }

        [Test]
        public void NetworkViewerValues()
        {
            var actual = Load();
            Assert.False(actual.NetworkViewer.AllowLocalClients);
            Assert.True(actual.NetworkViewer.AllowRemoteClients);
            Assert.AreEqual(10, actual.NetworkViewer.MaxQueueLength);
            Assert.False(actual.NetworkViewer.Enabled);
        }

        [Test]
        public void PropertiesValues()
        {
            var actual = Load();
            Assert.NotNull(actual.Properties);
            string foo;
            Assert.True(actual.Properties.TryGetValue("Foo", out foo));
            Assert.AreEqual("Bar", foo);
        }

        private AgentConfiguration Load()
        {
            var builder = new ConfigurationBuilder();
            builder.AddJsonFile(_configFileNamePath);
            var target = builder.Build();
            var actual = new AgentConfiguration();
            target.GetSection("Loupe").Bind(actual);
            return actual;
        }

        // JSON with different values from defaults to ensure values are coming from config.
        private const string JsonSource = @"{
  ""Loupe"": {
    ""Listener"": {
      ""AutoTraceRegistration"": false,
      ""EnableConsole"": false,
      ""EnableNetworkEvents"": false,
      ""EndSessionOnTraceClose"": false
    },
    ""SessionFile"": {
      ""Enabled"": false,
      ""AutoFlushInterval"": 42,
      ""IndexUpdateInterval"": 42,
      ""MaxFileSize"": 42,
      ""MaxFileDuration"": 42,
      ""EnableFilePruning"": false,
      ""MaxLocalDiskUsage"": 42,
      ""MaxLocalFileAge"": 42,
      ""MinimumFreeDisk"": 42,
      ""ForceSynchronous"": true,
      ""MaxQueueLength"": 42,
      ""Folder"": ""C:\\Temp"" 
    },
    ""Packager"": {
      ""HotKey"": ""Ctrl-Alt-F5"",
      ""AllowFile"": false,
      ""AllowRemovableMedia"": false,
      ""AllowEmail"": false,
      ""AllowServer"": false,
      ""FromEmailAddress"": ""mark@rendlelabs.com"",
      ""DestinationEmailAddress"": ""mark@rendlelabs.com"",
      ""ProductName"": ""Sample"",
      ""ApplicationName"": ""Loupe""
    },
    ""Publisher"": {
      ""ProductName"": ""Sample"",
      ""ApplicationDescription"": ""Sample App"",
      ""ApplicationName"": ""Loupe"",
      ""ApplicationType"": ""AspNet"",
      ""ApplicationVersionNumber"": ""42.0.0"",
      ""EnvironmentName"": ""Development"",
      ""PromotionLevelName"": ""QA"",
      ""ForceSynchronous"": true,
      ""MaxQueueLength"": 10,
      ""EnableAnonymousMode"": true,
      ""EnableDebugMode"": true
    },
    ""Server"": {
      ""Enabled"": false,
      ""AutoSendSessions"": true,
      ""AutoSendOnError"": false,
      ""SendAllApplications"": true,
      ""PurgeSentSessions"": true,
      ""CustomerName"": ""RendleLabs"",
      ""UseGibraltarService"": true,
      ""UseSsl"": true,
      ""Server"": ""onloupe.com"",
      ""Port"": 81,
      ""ApplicationBaseDirectory"": ""C:\\inetpub\\foo"",
      ""Repository"": ""quux""
    },
    ""NetworkViewer"": {
      ""AllowLocalClients"": false,
      ""AllowRemoteClients"": true,
      ""MaxQueueLength"": 10,
      ""Enabled"": false
    },
    ""Properties"": {
      ""Foo"": ""Bar"" 
    } 
  }
}
";
    }
}