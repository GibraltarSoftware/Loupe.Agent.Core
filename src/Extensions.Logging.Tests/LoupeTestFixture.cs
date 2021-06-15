using System;
using Loupe.Extensions.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Extensions.Logging.Tests
{
    public class LoupeTestFixture : IDisposable
    {
        public LoupeTestFixture()
        {
            var factory = new LoggerFactory(new[] {new LoupeLoggerProvider()},
                new LoggerFilterOptions() {CaptureScopes = true, MinLevel = LogLevel.Debug});
            Factory = factory;
        }

        public ILoggerFactory Factory { get; set; }

        /// <inheritdoc />
        public void Dispose()
        {
            Gibraltar.Agent.Log.EndSession("Unit test completion");
        }
    }

    [CollectionDefinition("Loupe")]
    public class LoupeTestCollection : ICollectionFixture<LoupeTestFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
