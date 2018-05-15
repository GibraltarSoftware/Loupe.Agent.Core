using System;
using System.Collections.Generic;
using Loupe.Agent.AspNetCore.Metrics;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Xunit;

namespace AspNetCore2.Tests
{
    public class ParameterStringFormatTests
    {
        [Fact]
        public void ReturnsEmptyStringForNoParameters()
        {
            Assert.Equal("", ParameterStringFormat.FromList(new List<ParameterDescriptor>()));
        }

        [Fact]
        public void ReturnsSingleValueForOneParameter()
        {
            var parameters = new List<ParameterDescriptor>
            {
                new ParameterDescriptor {Name = "foo"}
            };
            Assert.Equal("foo", ParameterStringFormat.FromList(parameters));
        }
        
        [Fact]
        public void ReturnsCommaSeparatedListForTwoParameters()
        {
            var parameters = new List<ParameterDescriptor>
            {
                new ParameterDescriptor {Name = "foo"},
                new ParameterDescriptor {Name = "bar"},
            };
            Assert.Equal("foo, bar", ParameterStringFormat.FromList(parameters));
        }
        
        [Fact]
        public void ReturnsCommaSeparatedListForThreeParameters()
        {
            var parameters = new List<ParameterDescriptor>
            {
                new ParameterDescriptor {Name = "foo"},
                new ParameterDescriptor {Name = "bar"},
                new ParameterDescriptor {Name = "quux"},
            };
            Assert.Equal("foo, bar, quux", ParameterStringFormat.FromList(parameters));
        }
        
        [Fact]
        public void ReturnsCommaSeparatedListForFourParameters()
        {
            var parameters = new List<ParameterDescriptor>
            {
                new ParameterDescriptor {Name = "foo"},
                new ParameterDescriptor {Name = "bar"},
                new ParameterDescriptor {Name = "quux"},
                new ParameterDescriptor {Name = "wibble"},
            };
            Assert.Equal("foo, bar, quux, wibble", ParameterStringFormat.FromList(parameters));
        }
    }
}
