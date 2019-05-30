using Gibraltar.Agent;
using System;
using System.Diagnostics;
using Gibraltar.Data;
using Loupe.Configuration;
using SessionCriteria = Gibraltar.Agent.SessionCriteria;

namespace Loupe.Packager
{
    static class Program
    {
        private const string LogCategory = "Loupe.Packager";

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static int Main(string[] args)
        {
            Log.Initializing += OnLogInitializing;
            Log.StartSession("Packager Application Starting");

            try
            {
                var returnVal = ExitCodes.Success;

                //now, there are two TOTALLY different ways we can go:  With or without a UI.  We need to parse the command line to find out.            
                Arguments commandArgs = null;
                try
                {
                    commandArgs = new Arguments(args);
                }
                catch (Exception ex)
                {
                    Log.RecordException(ex, LogCategory, true);
                }

                string productName = null;
                string applicationName = null;
                string folder = null;

                //see if we got a product or app name on the command line.  if specified they override our settings, even blanks.
                var waitForProcessExit = false;
                var monitorProcessId = 0;
                if (commandArgs != null)
                {
                    productName = commandArgs["p"];
                    applicationName = commandArgs["a"];
                    folder = commandArgs["folder"];

                    waitForProcessExit = (commandArgs["w"] != null);
                    if (waitForProcessExit)
                    {
                        string rawProcessId = commandArgs["w"];
                        if (int.TryParse(rawProcessId, out monitorProcessId) == false)
                        {
                            Log.Error(LogCategory, "Unable to process command line",
                                "The command line argument for Process ID '{0}' could not be interpreted as a number.",
                                rawProcessId);
                        }
                    }
                }

                //if we couldn't find a product name, we're in trouble
                if (string.IsNullOrEmpty(productName))
                {
                    returnVal = ExitCodes.MissingProductName;
                    Log.Error(LogCategory, "Unable to Start due to Configuration",
                        "There is no product name specified in the configuration so the packager can't start.");
                    Console.WriteLine("There is no product name specified so the packager can't start.");
                }
                else
                {
                    //see if we have to wait for a process to exit before we can run.
                    if (waitForProcessExit)
                    {
                        //try to get the process, it may no longer exist.
                        Process monitorProcess = null;
                        try
                        {
                            monitorProcess = Process.GetProcessById(monitorProcessId);
                        }
                        catch (ArgumentException ex)
                        {
                            Log.Verbose(ex, LogCategory, "Unable to find the process to wait on",
                                "When attempting to get the process object for the specified wait process Id '{0}' an exception was thrown:\r\n{1}",
                                monitorProcessId, ex.Message);
                        }

                        var hasExited = true;
                        if (monitorProcess != null)
                        {
                            Log.Information(LogCategory, "Waiting on calling process to exit",
                                "The wait option was specified with Process ID {0}, so the packager will wait for it to exit before continuing (up to 60 seconds).",
                                monitorProcessId);
                            hasExited = monitorProcess
                                .WaitForExit(60000); //we don't want to wait forever, it'll cause a problem.
                        }

                        Log.Information(LogCategory, "Wait on process complete", hasExited
                                ? "The process we were waiting on (pid {0}) is no longer running so the packager can continue."
                                : "The process we were waiting on (pid {0}) is still running but we've waited as long as we're willing to so we'll continue and package anyway.",
                            monitorProcessId);
                    }

                    var transmitMode = commandArgs["m"];
                    if (string.IsNullOrEmpty(transmitMode))
                    {
                        returnVal = ExitCodes.MissingTransmitMode;
                        Log.Error(LogCategory, "Unable to process command line",
                            "There is no transmit mode (-m) specified so the packager can't start.");
                        Console.WriteLine("There is no transmit mode (-m) specified so the packager can't start.");
                    }
                    else
                    {
                        try
                        {
                            transmitMode = transmitMode.ToUpperInvariant();
                            switch (transmitMode)
                            {
                                case "SERVER":
                                    //We need all of the server-specific parameters
                                    var configuration = new ServerConfiguration();

                                    configuration.CustomerName = commandArgs["customer"];
                                    configuration.Server = commandArgs["server"];
                                    configuration.ApplicationKey = commandArgs["key"];

                                    if (string.IsNullOrEmpty(configuration.Server) == false)
                                    {
                                        if (int.TryParse(commandArgs["port"], out var sdsPort))
                                            configuration.Port = sdsPort;

                                        var sslRaw = commandArgs["ssl"];
                                        if (string.IsNullOrEmpty(sslRaw) == false)
                                        {
                                            if (bool.TryParse(sslRaw, out var sdsUseSsl))
                                                configuration.UseSsl = sdsUseSsl;
                                        }

                                        configuration.ApplicationBaseDirectory = commandArgs["directory"];
                                        configuration.Repository = commandArgs["repository"];
                                    }

                                    string purgeRaw = commandArgs["purgeSentSessions"];
                                    if (string.IsNullOrEmpty(purgeRaw) == false)
                                    {
                                        if (bool.TryParse(purgeRaw, out var purgeSentSessions))
                                            configuration.PurgeSentSessions = purgeSentSessions;
                                    }

                                    //careful - we have to make different calls depending on whether we want to override server info.
                                    try
                                    {
                                        configuration.Validate();
                                    }
                                    catch (Exception ex)
                                    {
                                        returnVal = ExitCodes.MissingServerInfo;
                                        Log.Error(ex, LogCategory, "Unable to process command line",
                                            "There is insufficient server connection information to send to a server.\r\n{0}", ex.Message);
                                        Console.WriteLine(
                                            "There is insufficient server connection information to send to a server.\r\n{0}", ex.Message);
                                        break;
                                    }

                                    bool runForever = (commandArgs["t"] != null);

                                    if (runForever)
                                    {
                                        Log.Information(LogCategory, "Starting Publisher", "Restricting to Product: {0}, Application: {1}.\r\nServer: {2}", productName, applicationName??"any", configuration);
                                        Console.WriteLine("Starting publisher..");
                                        var publishEngine = new RepositoryPublishEngine(productName,
                                            applicationName, folder, configuration);

                                        publishEngine.Start();

                                        Console.WriteLine("Sessions being published to {0}\r\nPress any key to stop.", configuration);
                                        Console.ReadKey();

                                        Console.WriteLine("Stopping publisher..");
                                        publishEngine.Stop(true);
                                        Console.WriteLine("Publishing stopped.");
                                    }
                                    else
                                    {
                                        using (var packager =
                                            new Gibraltar.Agent.Packager(productName, applicationName, folder))
                                        {
                                            Console.WriteLine("Sending new sessions to {0}", configuration);
                                            packager.SendToServer(SessionCriteria.NewSessions, true,
                                                configuration.PurgeSentSessions, configuration);
                                        }
                                    }

                                    break;
                                case "FILE":
                                    string fullFileNamePath = commandArgs["d"];

                                    if (string.IsNullOrEmpty(fullFileNamePath))
                                    {
                                        returnVal = ExitCodes.MissingFileInfo;
                                        Log.Error(LogCategory, "Unable to process command line",
                                            "There is no file name (-d) specified so the packager can't start.");
                                        Console.WriteLine(
                                            "There is no file name (-d) specified so the packager can't start.");
                                    }
                                    else
                                    {
                                        using (var packager =
                                            new Gibraltar.Agent.Packager(productName, applicationName, folder))
                                        {
                                            packager.SendToFile(SessionCriteria.NewSessions, true,
                                                fullFileNamePath);
                                        }
                                    }

                                    break;
                                default:
                                    returnVal = ExitCodes.InvalidTransmitMode;
                                    Log.Error(LogCategory, "Unable to process command line",
                                        "Unrecognized transmit mode: {0}.  Try server, email or file",
                                        transmitMode);
                                    Console.WriteLine("Unrecognized transmit mode: Try server, email or file");
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            returnVal = ExitCodes.RuntimeException;
                            Log.RecordException(ex, LogCategory, false);
                        }
                    }
                }

                return (int)returnVal;
            }
            finally
            {
                Log.EndSession();
            }
        }

        private static void OnLogInitializing(object sender, LogInitializingEventArgs e)
        {
            e.Configuration.Publisher.EnableDebugMode = true;
        }
    }
}
