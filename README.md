# Loupe Agent for .NET Core #

The Loupe Agent provides a generic facility for capturing log messages, exceptions, and metrics
from .NET applications.  This repository is for a minimized .NET Core 1.1 version of the Loupe Agent.
Until 2017 the Loupe Agent was a closed source library developed by [Gibraltar Software.](https://onloupe.com)  This is the
first major step in open sourcing this library.  It is supported by Gibraltar Software and is compatible
with the primary [Loupe Agent.](https://www.nuget.org/packages/Gibraltar.Agent/)

To work with .NET Core 1.1, this agent has reduced functionality compared to the primary Loupe Agent and
is therefore only recommended for .NET Core 1.1 and 2.0 applications.

## How do I use Loupe with my Application? ##

This agent works nearly identically to the main Loupe Agent so you can refer to the [Getting Started Guide](https://doc.onloupe.com/#GettingStarted_Introduction.html)
of the main Loupe documentation for how to get moving.  You can also use the [free Loupe Desktop viewer](https://onloupe.com/local-logging/free-net-log-viewer) to
view logs & analyze metrics for your application or use [Loupe Cloud-Hosted](https://onloupe.com/) to add centralized logging,
alerting, and error analysis to your application.

If you want, you can even develop your own viewer from the classes exposed by Loupe.Core in this repository.

## What's In This Repository ##

This is the repository for the Loupe Agent for .NET Core.
The following deliverables live here:

* Loupe.Agent
* Loupe.Core
* Loupe.Extensions.Logging

## How To Build These Projects ##

The various projects can all be built with Visual Studio 2017 by opening src\Agent.sln.

## Contributing ##

Feel free to branch this project and contribute a pull request to the development branch. If your changes are incorporated into the master version they'll be published out to NuGet for everyone to use!