using System;
using System.Collections.Generic;
using System.Text;
using Loupe.Extensions.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Extensions.Logging.Tests
{
    [Collection("Loupe")]
    public class Basic_Logging_Tests
    {
        private readonly LoupeTestFixture _fixture;

        public Basic_Logging_Tests(LoupeTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Can_Log_Simple_Messages()
        {
            //In your code you'll want to inject the logger in the constructor,
            //generally from the .NET Core Dependency Injection framework.
            var logger = GetLogger<Basic_Logging_Tests>();

            //These top four severities are the common messages used in a deployed application.

            //Critical messages indicate situations that require immediate attention - 
            //where the application can't continue to do work with out outside help.
            logger.LogCritical("This message will show up as a Critical message in the log.");

            //Error messages indicate the application can't do what it was requested to do, but
            //it may be a local situation (like a bad argument or bad data) as opposed to a system-wide
            //problem (which would be a Critical message)
            logger.LogError("This message will show up as a Error message in the log.");

            //Warning messages indicate unusual or problematic situations that may cause an operation
            //to fail (or fail eventually) like running out of disk space (but not out) or a transient
            //recoverable error (like retrying a database operation on timeout)
            logger.LogWarning("This message will show up as a Warning message in the log.");

            //Information messages should indicate the main workflow of the application as it
            //works, creating the narrative for what the application is doing.
            logger.LogInformation("This message will show up as an Information message in the log.");

            //Debug messages are generally not enabled by default in production but should be
            //safe to run.  Note that this has nothing to do with Release/Debug compilation settings.
            logger.LogDebug("This message will show up as a Debug message in the log.");

            //Trace messages are the lowest severity and are not expected to be enabled in
            //production.  They may produce high volume and take a long time to generate due
            //to data being serialized.
            logger.LogTrace("This message will show up as a Trace message in the log.");
        }

        [Fact]
        public void Can_Log_Data_In_Message()
        {
            //In your code you'll want to inject the logger in the constructor,
            //generally from the .NET Core Dependency Injection framework.
            var logger = GetLogger<Basic_Logging_Tests>();

            //You can insert data in log messages using multiple different .NET
            //string format approaches.  Here's an example using the original .NET approach:
            var firstVal = "first";
            var secondVal = 2000;
            var thirdVal = DateTimeOffset.UtcNow;
            logger.LogInformation("This message will have three values inserted: " +
                                  "{0}, {1}, {2}",
                firstVal, secondVal, thirdVal);

            //You can also add some formatting for data types.  In this case, N0 takes integers
            //and adds thousands separators.  "g" is standard short date/time like 6/12/2021 12:45 PM.
            //Here's a reference for date/time formats:https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings
            logger.LogInformation("This message will have three formatted values inserted with the original string.format: " +
                                  "{0}, {1:N0}, {2:g}",
                firstVal, secondVal, thirdVal);

            //.NET supports string interpolation where the compiler will translate variable
            //names at compile time into the relevant insertion point.  However, this does have
            //a minor performance change where the insertions will *always* be done, even if the
            //message is ultimately not logged.
            //https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated
            logger.LogInformation("This message will have three formatted values inserted with string interpolation: " +
                                  $"{firstVal}, {secondVal:N0}, {thirdVal:g}");

            //The preferred approach is to use a Semantic Logging approach where you specify a label
            //for the data being inserted and the values are replaced in order.  In this case we don't
            //use the $ to designate an interpolated string but instead rely on the logging framework
            //to calculate the substitutions.
            logger.LogInformation("This message will have three formatted values inserted as semantic logging: " +
                                  "{First}, {Second:N0}, {Third:g}", firstVal, secondVal, thirdVal);

            //Be sure to use consistent labels for what the data is for the best logging output, like
            //this:
            logger.LogInformation("This message will have three formatted values inserted as semantic logging: " +
                                  "{Order}, {OrderQuantity:N0}, {OrderTimestamp:g}",
                firstVal, secondVal, thirdVal);
            
            //Use carriage return/line feed to format messages for easier readability.
            //Loupe uses these to separate "caption" and "description" - which feeds into how
            //messages are aggregated together and displayed.
            logger.LogInformation("The caption should be a consistent summary of the log message\r\n" +
                                  "Put unique values (like IP addresses, timestamps, etc.) after the" +
                                  "CRLF so they're in the description, like this:" +
                                  "{Order}, {OrderQuantity:N0}, {OrderTimestamp:g}",
                firstVal, secondVal, thirdVal);
        }

        [Fact]
        public void Can_Log_Exceptions_On_Messages()
        {
            //In your code you'll want to inject the logger in the constructor,
            //generally from the .NET Core Dependency Injection framework.
            var logger = GetLogger<Basic_Logging_Tests>();

            var ex = new InvalidOperationException("The App Can't Do That");

            //We can associate an exception with a log message so it's available as data
            //but not necessarily in the message text itself
            logger.LogError(ex, "This message is an Error with an exception attached as data.");

            //Usually you'll want to include some information from the exception in your message.
            logger.LogError(ex, $"Unable to do what you wanted due to {ex.GetType()}\r\n" +
                                $"{ex.Message}");

            //Exceptions can be associated with any severity:  Just because it has
            //an exception as data doesn't necessarily mean it's an error; it could
            //be a retryable error like a network timeout.
            logger.LogDebug(ex, $"Retrying operation due to transient error {ex.GetType()}\r\n" +
                                $"{ex.Message}");
        }

        [Fact]
        public void Can_Log_With_Event_Ids()
        {
            //In your code you'll want to inject the logger in the constructor,
            //generally from the .NET Core Dependency Injection framework.
            var logger = GetLogger<Basic_Logging_Tests>();

            //Event Ids can be specified to provide another method of filtering log
            //messages to find the ones you're interested in.  An Event Id is just
            //a number, you need to manage them so they are applied consistently
            //and are appropriately unique in your application. An Event Id should
            //identify the situation that is being recorded, like a user logging in,
            //and be consistent for each occurrence of that situation.
            logger.LogCritical(1, "This is critical event Id 1");
        }

        [Fact]
        public void Optimize_For_Loupe()
        {
            //In your code you'll want to inject the logger in the constructor,
            //generally from the .NET Core Dependency Injection framework.
            var logger = GetLogger<Basic_Logging_Tests>();
            
            //some insertion variables for our test
            var firstVal = "first";
            var secondVal = 2000;
            var thirdVal = DateTimeOffset.UtcNow;

            //create a fully populated exception with call stack by throwing/catching it.
            Exception ex;
            try
            {

                try
                {
                    throw new IndexOutOfRangeException("This is our inner exception");
                }
                catch (Exception innerEx)
                {
                    throw new InvalidOperationException("This is an outer exception", innerEx);
                }
            }
            catch (Exception outerEx)
            {
                ex = outerEx;
            }

            //When there is only one "message" for a log message, Loupe looks for
            //a first Carriage Return (\r) or Line Feed (\f) and treats the
            //message up to that as the "caption" and everything after that as the
            //"description".

            //This matters because for Critical, Error, and Warning the caption is
            //part of how Loupe groups log messages together into events.  So,
            //if you're using insertion strings you want to be cautious about doing
            //that *before* the first CRLF so you don't have an overly-unique log message.
            //For example, don't insert any of these things before the first CRLF:
            //* Timestamp or Date (Note: Loupe adds the message timestamp automatically)
            //* IP Address
            //* URL
            //* File Name
            //* file size or position

            //Any of these can be put in the description without issue.  Some things make
            //sense to put in the first line because they definitely should be considered
            //as part of the event.
            //* Exception Type (but *not* the message)

            //Note we prefer to use Semantic logging for string insertion instead of traditional string.format
            //or interpolated strings.  This produces the best log data.

            logger.LogCritical(ex, "Unable to keep running this application due to an {Exception}\r\n" +
                                   "Now that we're on the second line, here's more information we can share " +
                                   "that won't cause an overly-unique message: " +
                                   "{Order}, {OrderQuantity:N0}, {OrderTimestamp:g}", ex.GetType().Name,
                firstVal, secondVal, thirdVal);

            logger.LogError(ex, "Unable to complete the action we were performing due to an {Exception}\r\n" +
                                   "Errors are for when the current activity can't complete successfully but the " +
                                   "application can continue to process requests.", ex.GetType().Name);

            logger.LogWarning(ex, "It is very suspicious that we caught an {Exception}\r\n" +
                                   "But because we think we can handle this and continue with the current activity " +
                                   "(perhaps by doing a retry) we won't record it as an error.", ex.GetType().Name);


            logger.LogInformation("For an Informational message or lower we don't need to worry about putting " +
                                  "these items in the caption: {Order}, {OrderQuantity:N0}, {OrderTimestamp:g}\r\n" +
                                  "You can still can have a description field where you go wild with additional detail " +
                                  "but it's often useful to put distinct information in the caption to help in finding " +
                                  "the right activity.",
                firstVal, secondVal, thirdVal);

        }

        private ILogger<T> GetLogger<T>()
        {
            return _fixture.Factory.CreateLogger<T>();
        }
    }
}
