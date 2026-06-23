using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Ocelot.Configuration;
using Ocelot.Configuration.File;
using Ocelot.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiGateway.UnitTests
{
    public static class Dummies
    {
        public static DefaultHttpContext DummyHttpContext(
        string path,
        string method,
        string contactEmail,
        Dictionary<string, string> requiredClaims,
        Dictionary<string, string> headers = default,
        bool isAnonymous = false,
        Dictionary<string, string> routeValues = null,
        Dictionary<string, string> queryParameters = null
        )
        {
            if (headers == null) headers = new Dictionary<string, string>();
            // Mock HttpContext
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = path;
            httpContext.Request.Method = method;
            httpContext.Request.Headers.Authorization = new StringValues($"Bearer {GenerateDummyJwtToken(contactEmail)}");
            if (routeValues != null && routeValues.Count() > 0)
            {
                foreach (var keyValue in routeValues)
                {
                    httpContext.Request.RouteValues.Add(keyValue.Key, keyValue.Value);
                }
            }

            if (queryParameters != null && queryParameters.Count() > 0)
            {
                foreach (var keyValue in queryParameters)
                {
                    httpContext.Request.QueryString.Add(keyValue.Key, keyValue.Value);
                }
            }

            foreach (var kv in headers)
            {
                httpContext.Request.Headers[kv.Key] = kv.Value;
            }

            var downstreamRoute = GenerateDownStream(requiredClaims, isAnonymous);

            httpContext.Items["DownstreamRoute"] = downstreamRoute;

            return httpContext;
        }


        public static DownstreamRoute GenerateDownStream(Dictionary<string, string> requiredClaims, bool isAnonymous)
        {
            var downstreamRoute = new DownstreamRoute(
                key: "key",
                upstreamPathTemplate: new UpstreamPathTemplate("template", 1, true, ""),
                upstreamHeadersFindAndReplace: null,
                downstreamHeadersFindAndReplace: null,
                downstreamAddresses: null,
                serviceName: "serviceName",
                serviceNamespace: "serviceNamespace",
                httpHandlerOptions: null,
                useServiceDiscovery: false,
                enableEndpointEndpointRateLimiting: false,
                qosOptions: null,
                downstreamScheme: "http",
                requestIdKey: null,
                isCached: false,
                cacheOptions: null,
                loadBalancerOptions: null,
                rateLimitOptions: null,
                routeClaimsRequirement: requiredClaims,
                claimsToQueries: null,
                claimsToHeaders: null,
                claimsToClaims: null,
                claimsToPath: null,
                isAuthenticated: !isAnonymous,
                isAuthorized: false,
                authenticationOptions: null,
                downstreamPathTemplate: null,
                loadBalancerKey: null,
                delegatingHandlers: null,
                addHeadersToDownstream: null,
                addHeadersToUpstream: null,
                dangerousAcceptAnyServerCertificateValidator: false,
                securityOptions: null,
                downstreamHttpMethod: null,
                downstreamHttpVersion: null,
                downstreamHttpVersionPolicy: default,
                upstreamHeaders: null,
                metadataOptions: new MetadataOptions(new FileMetadataOptions())
            );
            return downstreamRoute;
        }
        private static string GenerateDummyJwtToken(string userEmail)
        {
            var header = Base64UrlEncode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
            var claims = new Dictionary<string, string>
        {
            {"email", userEmail}
        };
            var payload = Base64UrlEncode(System.Text.Json.JsonSerializer.Serialize(claims));
            var signature = "";

            return $"{header}.{payload}.{signature}";
        }

        private static string Base64UrlEncode(string input)
        {
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(inputBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
