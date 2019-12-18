using System;
using System.Collections.Generic;
using System.Text;
using Loupe.Configuration;
using Loupe.Extensibility.Data;
using NUnit.Framework;

namespace Loupe.Agent.Test.Configuration
{
    [TestFixture]
    public class Coded_Configuration_Tests
    {
        [Test]
        public void Can_Initialize_Configuration_In_Code()
        {
            var loupeConfig = new AgentConfiguration
            {
                Publisher = new PublisherConfiguration
                {
                    ProductName = "Loupe",
                    ApplicationName = "Agent Text",
                    ApplicationType = ApplicationType.Console,
                    ApplicationDescription = "Console test application for .NET Core",
                    ApplicationVersion = new Version(2, 1)
                },
                Server = new ServerConfiguration
                {
                    UseGibraltarService = true,
                    CustomerName = "Your_Service_Name"
                }
            };

            Assert.That(loupeConfig.Publisher.ApplicationName, Is.EqualTo("Agent Text"));
        }
    }
}
