# Loupe Agent for .NET 5, .NET Core, and Xamarin #

This repository has the modern Loupe Agents for .NET Standard (for Xamarin) and .NET Core / .NET 5.
The Loupe Agent provides a generic facility for capturing log messages, exceptions, and metrics
from .NET applications.  It is supported by Gibraltar Software and replaces the original
[Loupe Agent.](https://www.nuget.org/packages/Gibraltar.Agent/)

## How do I use Loupe with my Application? ##

To add Loupe to your application we recommend referencing either the [Loupe.Extensions.Logging package](https://www.nuget.org/packages/Loupe.Extensions.Logging/)
or [Loupe.Agent.Core package](https://www.nuget.org/packages/Loupe.Agent.Core/).  These will pull in the related dependencies.

For complete instructions, see the [Getting Started Guide](https://doc.onloupe.com/#GettingStarted_Introduction.html)
of the main Loupe documentation for how to get moving. It covers a range of scenarios for .NET 5, .NET Core and instructions
for other agents for .NET Framework and Java.

You can use the [free Loupe Desktop viewer](https://onloupe.com/local-logging/free-net-log-viewer) to
view logs & analyze metrics for your application or use [Loupe Cloud-Hosted](https://onloupe.com/) to add centralized logging,
alerting, and error analysis to your application.

If you want, you can even develop your own viewer from the classes exposed by Loupe.Core in this repository.

## What's In This Repository ##

This is the repository for the Loupe Agent for .NET Core.
The following NuGet packages live here:

* **Loupe.Agent.Core**: The primary API to use for logging & metrics.
* **Loupe.Agent.Core.Extensibility**: Contains some reused types and the configuration classes.
* **Loupe.Agent.Core.Internal**: The internal implementation of Loupe, not typically used directly by client applications.
* **Loupe.Agent.Core.Services**: Configuration extensions for Loupe used to simplify integration with various startup scenarios.
* **Loupe.Extensions.Logging**: An implementation of Microsoft.Extensions.Logging.Abstractions to interface with common .NET Core libraries.
* **Loupe.Agent.AspNetCore**: The primary agent for ASP.NET Core applications.
* **Loupe.Agent.EntityFrameworkCore**: Performance and diagnostic information for EF Core 2 and later.
* **Loupe.Agent.PerformanceCounters**: Performance counter and process metrics.
* **Loupe.Packager**: A command-line tool for pushing Loupe log files to a central server or a package to move offline.

Each of these packages maps to a single project in the repository. Other projects, primarily for unit testing, are not
included in the packages.

## How To Build These Projects ##

The various projects can all be built with Visual Studio 2019 by opening src\Agent.sln.

## Contributing ##

Feel free to branch this project and contribute a pull request to the development branch. If your changes are incorporated into the master version they'll be published out to NuGet for everyone to use!
