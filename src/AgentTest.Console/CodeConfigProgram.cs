using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Loupe.Agent;
using Loupe.Agent.Test.LogMessages;
using Loupe.Configuration;
using Loupe.Extensibility.Data;
using ApplicationType = Loupe.Extensibility.Data.ApplicationType;

namespace Loupe.AgentTest.Console
{
class CodeConfigProgram
{
    static void Main(string[] args)
    {
        //create an AgentConfiguration object, specifying everything you want to override.
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

        Log.StartSession(loupeConfig, "Starting Loupe Console");

        try
        {
            //Here is the body of your console application
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
}
}
