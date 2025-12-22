using System.Net;

namespace ApiGateway.Pennylane
{
    public class PennylaneApiException : Exception
    {
        public PennylaneApiException(HttpStatusCode statusCode, string responseContent, string url)
            : base($"Pennylane API call to {url} returned {(int)statusCode} ({statusCode}). Response: {responseContent}")
        {
            StatusCode = statusCode;
            ResponseContent = responseContent;
            Url = url;
        }

        public HttpStatusCode StatusCode { get; }

        public string ResponseContent { get; }

        public string Url { get; }
    }
}
