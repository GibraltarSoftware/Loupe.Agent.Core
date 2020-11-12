#pragma warning disable 1591

using System.ComponentModel;
using Gibraltar.Agent;

namespace Loupe.Agent.AspNetCore.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class JavaScriptMessageSource : IMessageSourceProvider
    {
        /// <summary>
        /// The name of the class in which the message occurred
        /// </summary>
        public string? ClassName { get; set; }

        /// <summary>
        /// The name of the file in which the message occurred
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// The line number upon which the message occurred
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// The name of the method in which the message occurred
        /// </summary>
        public string? MethodName { get; set; }
    }
}