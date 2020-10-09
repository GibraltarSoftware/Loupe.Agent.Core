using System.Collections.Generic;

namespace Loupe.Agent.AspNetCore.Models
{
    public class ClientSideException
    {
        /// <summary>
        /// The message associated with the error
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The URL upon which the error occurred
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// The stack trace
        /// </summary>
        public List<string> StackTrace { get; set; }

        /// <summary>
        /// Optional. The cause of the error
        /// </summary>
        public string Cause { get; set; }

        /// <summary>
        /// Optional. The line number upon which the error occurred
        /// </summary>
        public int? Line { get; set; }

        /// <summary>
        /// Optional. The column number upon which the error occurred
        /// </summary>
        public int? Column { get; set; }


        /// <summary>
        /// Indicates if the class is in fact empty i.e created but with no values
        /// </summary>
        /// <remarks>This method is only used by the code creating a <see cref="JavaScriptException"/>
        /// as such it only needs to know if message and stack trace are not null.
        /// We need this method as if a request is received with an empty object rather
        /// than null for Error then JSON.net will create a new empty object with no data
        /// which we don't want to log.</remarks>
        /// <returns>true if necessary properties not null; otherwise false</returns>
        public bool IsEmpty()
        {
            return Message == null && StackTrace != null;
        }
    }
}