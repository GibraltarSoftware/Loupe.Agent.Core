# Loupe Agent for .NET Core #

The Loupe Agent provides a generic facility for capturing log messages, exceptions, and metrics
from .NET applications.  This repository is for a minimized .NET Core 1.1 version of the Loupe Agent.
Until 2017 the Loupe Agent was a closed source library developed by [Gibraltar Software.](https://onloupe.com)  This is the
first major step in open sourcing this library.  It is supported by Gibraltar Software and is compatible
with the primary [Loupe Agent.](https://www.nuget.org/packages/Gibraltar.Agent/)

To work with .NET Core 1.1, this agent has reduced functionality compared to the primary Loupe Agent and
is therefore only recommended for .NET Core 1.1 and 2.0 applications.

## How do I use Loupe with my Application? ##

To add Loupe to your application we recommend referencing either the [Loupe.Extensions.Logging package](https://www.nuget.org/packages/Loupe.Extensions.Logging/)
or [Loupe.Agent.Core package](https://www.nuget.org/packages/Loupe.Agent.Core/).  These will pull in the related dependencies.

This agent works nearly identically to the main Loupe Agent so you can refer to the [Getting Started Guide](https://doc.onloupe.com/#GettingStarted_Introduction.html)
of the main Loupe documentation for how to get moving. The main difference is configuration - since .NET Core doesn't
support the traditional app.config/web.config style configuration this has all been externalized into the Loupe.Configuration.AgentConfiguration
class which you can use with your preferred configuration approach for .NET Core.

You can use the [free Loupe Desktop viewer](https://onloupe.com/local-logging/free-net-log-viewer) to
view logs & analyze metrics for your application or use [Loupe Cloud-Hosted](https://onloupe.com/) to add centralized logging,
alerting, and error analysis to your application.

If you want, you can even develop your own viewer from the classes exposed by Loupe.Core in this repository.

## What's In This Repository ##

This is the repository for the Loupe Agent for .NET Core.
The following NuGet packages live here:

* Loupe.Agent.Core: The primary API to use for logging & metrics.
* Loupe.Agent.AspNetCore: The primary agent for ASP.NET Core applications.
* Loupe.Agent.Core.Extensibility: Contains some reused types and the configuration classes.
* Loupe.Agent.Core.Internal: The internal implementation of Loupe, not typically used directly by client applications.
* Loupe.Extensions.Logging: An implementation of Microsoft.Extensions.Logging.Abstractions to interface with common .NET Core libraries.

Each of these packages maps to a single project in the repository. Other projects, primarily for unit testing, are not
included in the packages.

## How To Build These Projects ##

The various projects can all be built with Visual Studio 2017 by opening src\Agent.sln.

## Contributing ##

Feel free to branch this project and contribute a pull request to the development branch. If your changes are incorporated into the master version they'll be published out to NuGet for everyone to use!