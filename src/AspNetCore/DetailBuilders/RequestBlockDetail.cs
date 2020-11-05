namespace Loupe.Agent.AspNetCore.DetailBuilders
{
    internal class RequestBlockDetail
    {
        public RequestBlockDetail(string browser, string contentType, long contentLength, bool isLocal, bool isSecureConnection, string userHostAddress, string userHostName)
        {
            Browser = browser;
            ContentType = contentType;
            ContentLength = contentLength;
            IsLocal = isLocal;
            IsSecureConnection = isSecureConnection;
            UserHostAddress = userHostAddress;
            UserHostName = userHostName;
        }

        public string Browser { get; private set; }

        public string ContentType { get; private set; }

        public long ContentLength { get; private set; }

        public bool IsLocal { get; private set; }

        public bool IsSecureConnection { get; private set; }

        public string UserHostAddress { get; private set; }

        public string UserHostName { get; private set; }
    }
}