#region File Header

// <copyright file="JavaScriptSourceProvider.cs" company="Gibraltar Software Inc.">
// Gibraltar Software Inc. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Linq;
using System.Text.RegularExpressions;
using Gibraltar.Agent;
using Loupe.Agent.AspNetCore.Models;

#endregion

namespace Loupe.Agent.AspNetCore.Infrastructure
{
    /// <summary>
    /// Message source provider for JavaScript exceptions
    /// </summary>
    internal class JavaScriptSourceProvider 
    {
        // This isn't perfect, but it's close; JavaScript stack traces aren't consistent across browsers or frameworks
        private readonly Regex _sourcePattern =
            new Regex(@"(?<function>.*)(?<file>http://localhost:\d+/[^:]*|http://[^:]*):(?<line>\d+)",
                RegexOptions.IgnoreCase);


        /// <summary>
        /// Creates a new source provider
        /// </summary>
        /// <param name="message">The JavaScript message</param>
        public IMessageSourceProvider ProcessMessage(LogMessage message)
        {
            var sourceProvider = new JavaScriptMessageSource();

            if (message.MethodSourceInfo != null && !message.MethodSourceInfo.IsEmpty())
            {
                sourceProvider.FileName = message.MethodSourceInfo.File;
                sourceProvider.ClassName = message.MethodSourceInfo.Class;
                sourceProvider.MethodName = message.MethodSourceInfo.Method;
                if (message.MethodSourceInfo.Line.HasValue)
                {
                    sourceProvider.LineNumber = message.MethodSourceInfo.Line.Value;
                }

                return sourceProvider;
            }

            if (message.Exception != null && !message.Exception.IsEmpty())
            {
                if (message.Exception.StackTrace != null && message.Exception.StackTrace.Any())
                {
                    // search for the actual message line; the stack trace could be in reverse order
                    foreach (var line in message.Exception.StackTrace)
                    {
                        var match = _sourcePattern.Match(line);

                        if (!match.Success)
                        {
                            continue;
                        }

                        sourceProvider.MethodName = match.Groups["function"].ToString()
                            .Replace("at new", "")
                            .Replace("at ", "")
                            .Replace("/<@", "")
                            .Replace(" (", "")
                            .Replace("([arguments not available])@", "")
                            .Replace("@", "")
                            .Trim();

                        sourceProvider.FileName = match.Groups["file"].ToString();

                        if (message.Exception.Line.HasValue)
                        {
                            sourceProvider.LineNumber = message.Exception.Line.Value;
                        }
                        else
                        {
                            int lineNumber;
                            int.TryParse(match.Groups["line"].ToString(), out lineNumber);
                            sourceProvider.LineNumber = lineNumber;
                        }
                        break;
                    }
                }
            }

            return sourceProvider;
        }

    }
}