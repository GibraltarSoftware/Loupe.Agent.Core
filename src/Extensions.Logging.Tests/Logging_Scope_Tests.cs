using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Extensions.Logging.Tests
{
    [Collection("Loupe")]
    public class Logging_Scope_Tests
    {
        private readonly LoupeTestFixture _fixture;

        public Logging_Scope_Tests(LoupeTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Can_Create_Named_Scope()
        {
            var logger = _fixture.Factory.CreateLogger(nameof(Can_Create_Named_Scope));

            using (logger.BeginScope("Starting the scope for {Method}", nameof(Can_Create_Named_Scope)))
            {
                //these log messages will have the scope
                logger.LogInformation("This log message will be within Simple_Named_Scope.");
            }

            //this log message won't.
            logger.LogInformation("This log message will not be within Simple_Named_Scope.");
        }

        [Fact]
        public void Can_Create_Dictionary_Scope()
        {
            var logger = _fixture.Factory.CreateLogger(nameof(Can_Create_Dictionary_Scope));

            using (logger.BeginScope(new Dictionary<string, object>
            {
                {"First", "This is the first scope value"},
                {"Second", 2000},
                {"Third", DateTimeOffset.UtcNow}
            }))
            {
                logger.LogInformation("This log message will have three scope elements associated with it.");

                //now add another inner scope.
                using (logger.BeginScope(new Dictionary<string, object>
                {
                    {"Fourth", 4.1D}
                }))
                {
                    logger.LogWarning("This log message will have four scope elements associated with it");
                }

                logger.LogError("This log message will have reverted to just three scope elements associated with it.");
            }

            logger.LogDebug("This log message will have not have any scope elements associated with it.");
        }
    }
}
