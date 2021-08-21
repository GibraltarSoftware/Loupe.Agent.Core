using System;
using Gibraltar.Agent;

namespace Loupe.Agent.AspNetCore.DetailBuilders
{
    internal class RequestBlockBuilder : DetailsBuilderBase
    {
        private readonly IRequestDetailBuilder _requestDetailBuilder;

        const string DetailsFormat = "<UserAgent>{0}</UserAgent>\r\n" +
                                     "<ContentType>{1}</ContentType>\r\n" +
                                     "<ContentLength>{2}</ContentLength>\r\n" +
                                     "<IsLocal>{3}</IsLocal>\r\n" +
                                     "<IsSecureConnection>{4}</IsSecureConnection>\r\n" +
                                     "<UserHostAddress>{5}</UserHostAddress>\r\n" +
                                     "<UserHostName>{6}</UserHostName>";

        public RequestBlockBuilder(IRequestDetailBuilder requestDetailBuilder)
        {
            _requestDetailBuilder = requestDetailBuilder;
        }

        public string Build(string? requestBody = null)
        {
            DetailBuilder.Clear();

            DetailBuilder.Append("<Request>");

            try
            {
                var details = _requestDetailBuilder.GetDetails();

                DetailBuilder.AppendFormat(DetailsFormat, 
                    details.Browser,
                    details.ContentType,
                    details.ContentLength,
                    details.IsLocal,
                    details.IsSecureConnection,
                    details.UserHostAddress,
                    details.UserHostName);

            }
            catch (System.Exception ex)
            {
                GC.KeepAlive(ex);
#if DEBUG
                Log.Error(ex, "Loupe.Internal", "Unable to build standard Request details block due to " + ex.GetType(),
                    "Exception occurred whilst trying to build the standard Request details block, no request will be added to detail\r\n{0}",
                    ex.Message);
#endif
                DetailBuilder.Append(
                    "We were unable to record details from the Request itself due to an exception occurring whilst extracting information from the Request.");
            }

            if (requestBody != null)
            {
                DetailBuilder.Append("<RequestBody>" + requestBody + "</RequestBody>");
            }

            DetailBuilder.Append("</Request>");

            return DetailBuilder.ToString();
        }         
    }
}