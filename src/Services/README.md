# Loupe Dependency Injection

## Overview

I've tried to follow the .NET Core way with Loupe's setup, so the
Services project adds extension methods to `IServiceCollection`
that return an `ILoupeAgentBuilder` against which users can apply
further configuration steps. The other packages can then add their
own extension methods to that type, such as the `AddAspNetCoreDiagnostics`
method from the AspNetCore2 project.

## Step-by-step

### `ServicesExtensions.AddLoupe`

This adds three Singletons to `IServiceCollection`:

- `LoupeAgent` is the main agent
- `LoupeConfigurationCallback` wraps the `Action<AgentConfiguration>` that is
optionally passed to the `AddLoupe` extension method. This will be injected
into the `LoupeAgent` instance when it is created.
- `LoupeAgentService` is an `IHostedService` implementation that exists
purely to force the creation of the `LoupeAgent` *after* the DI container
is fully-baked. The ASP.NET Core hosting system will create an instance
of this class and keep it around until the application exits.

### `ILoupeAgentBuilder`

At the moment, this just provides a single method, `AddListener<T>()
where T : ILoupeDiagnosticListener`. The `AspNetCore2` and `EFCore2`
projects provide implementations of this interface to listen on the
`System.Diagnostics.DiagnosticSource` channels for system-raised
diagnostic events, which are used instead of the old IIS integrated pipeline
hooks from old .NET Loupe.

### `LoupeAgent`

This is where we pull in the various dependencies from .NET Core hosting,
specifically:

- `IConfiguration` to bind any settings from `appSettings.json` and so
on to the Agent configuration.
- `IHostingEnvironment` to grab data like Application Name.
- `IServiceProvider` to find any registered `ILoupeDiagnosticListener`s.
- `IApplicationLifetime` to hook into the `ApplicationStarted` and
`ApplicationStopped` events.

`LoupeAgent` also creates the master `LoupeDiagnosticListener` that hooks
up all the specific `ILoupeDiagnosticListener`s.

### `LoupeDiagnosticListener`

The way the `DiagnosticSource` source system works is all about
`IObservable`s. There is a static property, `DiagnosticListener.AllListeners`,
which is an `IObservable<DiagnosticListener>`. Every time an instance of
`DiagnosticListener` is created anywhere in the process, it gets passed
through this observable. It's also buffered, so if you subscribe after
some Listeners have been created, you'll still get the full list.

The `ILoupeDiagnosticListener` just has a `Name` property which will
be compared to the `DiagnosticListener.Name` property; if they match,
the listener will be subscribed to the source.

There are two ways to subscribe to a `DiagnosticListener`:

1. Using an `IObserver<string, object>` to just handle raw objects and
serialize them using reflection.
2. Using an adapter, from the
[`Microsoft.Extensions.DiagnosticAdapter`](https://www.nuget.org/packages/Microsoft.Extensions.DiagnosticAdapter/2.2.0)
package, which uses runtime codegen to create proxies. This gives better
performance but requires more up-front knowledge of the shape of objects
being received from the source.

## Logging

We also have a separate extension method for hooking up to the
`Microsoft.Extensions.Logging` library, which extends `ILoggerBuilder`.
For users of ASP.NET Core 2.1 and later, this should be called from the
`ConfigureLogging` method on `IWebHostBuilder` to make sure that log
messages are picked up as early as possible, not just when the
application has started.

## Room for improvement

I think a better place for setting all of this up would be on
`IWebHostBuilder`, where we could provide a single `UseLoupe` extension
method that would register all the services *and* start the logging
listener. That would look something like this:

```csharp
namespace Loupe.Agent.AspNetCore
{
    public static class LoupeWebHostBuilderExtensions
    {
        public static IWebHostBuilder UseLoupe(this IWebHostBuilder builder,
                                               Action<ILoupeAgentBuilder> configure)
        {
            builder.ConfigureLogging(logging => logging.AddLoupe());
            builder.ConfigureServices(services =>
            {
                var agentBuilder = services.AddLoupe();
                configure?.Invoke(agentBuilder);
            });
            return builder;
        }
    }
}
```

We could provide an equivalent version for the `IHostBuilder` from
`Microsoft.Extensions.Hosting`, and in .NET Core 3.0 that's going to
become the standard across ASP.NET Core, WPF, WinForms and plain old
console applications, so we'd have everything covered.

This would mean dropping support for .NET Core 2.0.x, which Microsoft
have already done.