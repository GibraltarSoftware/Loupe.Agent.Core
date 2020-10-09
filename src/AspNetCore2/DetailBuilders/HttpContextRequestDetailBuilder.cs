using Microsoft.AspNetCore.Http;

namespace Loupe.Agent.AspNetCore.DetailBuilders
{
    public class HttpContextRequestDetailBuilder : IRequestDetailBuilder
    {
        private readonly HttpContext _context;

        public HttpContextRequestDetailBuilder(HttpContext context)
        {
            _context = context;
        }

        public RequestBlockDetail GetDetails()
        {
            var userAgent = _context.Request.Headers.TryGetValue("User-Agent", out var values) ? values[0] : "";
            return new RequestBlockDetail(userAgent,
                _context.Request.ContentType,
                _context.Request.ContentLength ?? 0,
                _context.Request.IsLocal(),
                _context.Request.IsHttps,
                _context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                string.Empty); // No way to get Remote Address except reverse DNS lookup
        }
    }
}