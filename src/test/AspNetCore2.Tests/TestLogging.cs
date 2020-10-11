using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Gibraltar.Agent;
using Loupe.Agent.AspNetCore.Infrastructure;
using Loupe.Agent.AspNetCore.Models;
using NSubstitute;
using Xunit;

namespace Loupe.Agent.AspNetCore.Tests
{
    public class TestLogging : IClassFixture<TestApplicationFactory>
    {
        private readonly TestApplicationFactory _factory;
        private readonly ILoupeLog _mockLog;

        public TestLogging(TestApplicationFactory factory)
        {
            _factory = factory;
            _mockLog = _factory.MockLog = Substitute.For<ILoupeLog>();
        }

        [Fact]
        public async Task CallsLog()
        {
            var client = _factory.CreateClient();

            string currentAgentSessionId = Guid.NewGuid().ToString();
            var now = DateTimeOffset.UtcNow;

            var logMessage = CreateLogMessage(currentAgentSessionId, now);

            var logRequest = new LogRequest
            {
                Session = new ClientSession
                {
                    Client = CreateClientDetails(),
                    CurrentAgentSessionId = currentAgentSessionId
                },
                LogMessages = new List<LogMessage>{ logMessage }
            };

            var json = JsonSerializer.Serialize(logRequest, new JsonSerializerOptions {PropertyNamingPolicy = JsonNamingPolicy.CamelCase});
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "/loupe/log")
            {
                Content = content
            };

            var response = await client.SendAsync(request);
            
            Assert.True(response.IsSuccessStatusCode);
            
            _mockLog.Received()
                .Write(LogMessageSeverity.Information, "Loupe", Arg.Any<IMessageSourceProvider>(),
                    Arg.Any<IPrincipal>(), null, LogWriteMode.Queued, Arg.Any<string>(),
                    "TestCategory", "TestCaption", "TestDescription", Arg.Any<object[]>());
        }

        private static LogMessage CreateLogMessage(string currentAgentSessionId, DateTimeOffset now)
        {
            return new LogMessage
            {
                Caption = "TestCaption",
                AgentSessionId = currentAgentSessionId,
                Category = "TestCategory",
                Description = "TestDescription",
                Details = @"{""Test"":""TestDetails""}",
                Sequence = 42,
                TimeStamp = now,
                Severity = LogMessageSeverity.Information,
            };
        }

        private static ClientDetails CreateClientDetails()
        {
            return new ClientDetails
            {
                Description = "TestClientDescription",
                Layout = "TestLayout",
                Manufacturer = "TestManufacturer",
                Name = "TestName",
                Prerelease = "TestPrerelease",
                Product = "TestProduct",
                Version = "TestVersion",
                UserAgentString = "TestUserAgentString",
                Size = CreateSize(),
                OS = CreateOS()
            };
        }

        private static ClientOS CreateOS()
        {
            return new ClientOS
            {
                Architecture = 1,
                Family = "TestFamily",
                Version = "TestVersion"
            };
        }

        private static ClientDimensions CreateSize()
        {
            return new ClientDimensions
            {
                Height = 42,
                Width = 54
            };
        }
    }
}
