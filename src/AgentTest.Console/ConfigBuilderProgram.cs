using System;
using System.Threading.Tasks;
using Gibraltar.Agent;
using Loupe.Agent.Test.LogMessages;
using Loupe.Configuration;
using Loupe.Extensibility.Data;
using Microsoft.Extensions.Configuration;

namespace Loupe.AgentTest.Console
{
    class ConfigBuilderProgram
    {
        static async Task Main(string[] args)
        {
            Log.StartSession(LoadConfig());

            try
            {
                var logTests = new LogTests();
                logTests.WriteMessagesForOrderTesting();
                logTests.BetterLogSample();
                logTests.WriteException();
                logTests.WriteExceptionAttributedMessages();

                var testFixture = new PerformanceTests();

                System.Console.WriteLine("Running first test round");
                testFixture.AsyncMessage();
                Log.EndFile("Rolling over test file (First time)");


                System.Console.WriteLine("Running second test round");
                testFixture.AsyncMessage();
                Log.EndFile("Rolling over test file (Second time)");


                System.Console.WriteLine("Running third test round");
                testFixture.AsyncMessage();
                Log.EndFile("Rolling over test file (Third time)");


                System.Console.WriteLine("Sending new sessions to server...");
                await Log.SendSessions(SessionCriteria.NewSessions);
                System.Console.WriteLine("Completed sending to server.");

            }
            catch (Exception ex)
            {
                Log.RecordException(ex, "Main", false);
                Log.EndSession(SessionStatus.Crashed, "Exiting due to unhandled exception");
            }
            finally
            {
                Log.EndSession(SessionStatus.Normal, "Exiting test application");
            }
        }

        private static AgentConfiguration LoadConfig()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            var agentConfig = new AgentConfiguration();
            config.GetSection("Loupe").Bind(agentConfig);
            return agentConfig;
        }
    }
}
