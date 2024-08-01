using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Ocelot.Configuration;
using Ocelot.Values;
using System.Security.Claims;

namespace Gateway.Specflow.Helper
{
    public static class HttpContextHelper
    {
        public static DefaultHttpContext DummyHttpContext(
        string path,
        string method,
        string contactEmail,
        Dictionary<string, string> requiredClaims,
        bool isCollab = false)
        {
            // Mock HttpContext
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = path;
            httpContext.Request.Method = method;
            httpContext.Request.Headers.Authorization = new StringValues($"Bearer {GenerateDummyJwtToken(contactEmail)}");

            var claims = isCollab
                ? new List<Claim>
                    {
                        new Claim(ClaimTypes.Role, "Collaborator"),
                        new Claim("groups", "collaborators-group")
                    }
                : new List<Claim>
                    {
                       new Claim(ClaimTypes.Role, "Customer"),
                    };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var principal = new ClaimsPrincipal(identity);
            httpContext = new DefaultHttpContext { User = principal };


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
                isAuthenticated: false,
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
                downstreamHttpVersion: null
            );

            httpContext.Items["DownstreamRoute"] = downstreamRoute;

            return httpContext;
        }

        private static string GenerateDummyJwtToken(string userEmail)
        {
            var header = Base64UrlEncode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
            var claims = new Dictionary<string, string>
        {
            { "email", userEmail },
            { "RoleClaimType", "Collaborator" }
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
