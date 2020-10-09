using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Principal;

namespace Loupe.Agent.AspNetCore.Models
{
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public class LogRequest
    {
        public ClientSession? Session { get; set; }

        public List<LogMessage>? LogMessages { get; set; }

        public IPrincipal? User { get; set; }
    }
}