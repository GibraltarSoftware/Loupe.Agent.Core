using System;
using System.Collections.Generic;
using System.Linq;
using Loupe.Extensions.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Extensions.Logging.Tests
{
    [Collection("Loupe")]
    public class LoupeLogEnricherTests
    {
        private readonly LoupeTestFixture _fixture;

        public LoupeLogEnricherTests(LoupeTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void CreatesJsonFromScope()
        {
            var provider = new LoupeLoggerProvider();
            var logger = (LoupeLogger) provider.CreateLogger("Test");
            string json;
            using (logger.BeginScope("Number: {Number}", 42))
            {
                json = LoupeLogEnricher.GetJson((object) null, provider);

                //eliminate all whitespace for comparison to avoid formatting variations.
                json = EliminateWhiteSpaceCharacters(json);
            }

            Assert.Equal("{\"Number\":42}", json);
        }

        [Fact]
        public void CreatesJsonFromState()
        {
            var provider = new LoupeLoggerProvider();
            var state = new Dictionary<string, object>
            {
                ["Word"] = "Hello",
                ["Integer"] = 42,
                ["Decimal"] = 23.3m
            };
            var json = LoupeLogEnricher.GetJson(state, provider);

            //eliminate all whitespace for comparison to avoid formatting variations.
            json = EliminateWhiteSpaceCharacters(json);

            Assert.Contains("\"Word\":\"Hello\"", json);
            Assert.Contains("\"Integer\":42", json);
            Assert.Contains("\"Decimal\":23.3", json);
        }

        [Fact]
        public void ClosestScopeWins()
        {
            var provider = new LoupeLoggerProvider();
            var logger = (LoupeLogger) provider.CreateLogger("Test");
            string json;
            using (logger.BeginScope("Number: {Number}", 42))
            {
                var state = new Dictionary<string, object>
                {
                    ["Number"] = 23,
                };
                json = LoupeLogEnricher.GetJson(state, provider);

                //eliminate all whitespace for comparison to avoid formatting variations.
                json = EliminateWhiteSpaceCharacters(json);
            }
            Assert.Equal("{\"Number\":23}", json);
        }

        [Fact]
        public void ClearsScopeOnDispose()
        {
            var provider = new LoupeLoggerProvider();
            var logger = (LoupeLogger) provider.CreateLogger("Test");
            using (logger.BeginScope("Number: {Number}", 42))
            {
                logger.LogInformation("Ignore");
            }
            var state = new Dictionary<string, object>
            {
                ["Value"] = 23,
            };
            var json = LoupeLogEnricher.GetJson(state, provider);

            //eliminate all whitespace for comparison to avoid formatting variations.
            json = EliminateWhiteSpaceCharacters(json);

            Assert.Equal("{\"Value\":23}", json);
            
        }

        private string EliminateWhiteSpaceCharacters(string original)
        {
            return new string(original.ToCharArray()
                .Where(c => !char.IsWhiteSpace(c))
                .ToArray());
        }
    }
}