using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ApiGateway.Helpers
{
    public static class JwtHelper
    {
        public static string ExtractBearerToken(HttpRequest request)
        {
            if (request == null || request.Headers == null)
            {
                return string.Empty;
            }

            if (request.Headers.TryGetValue("Authorization", out var extractedToken))
            {
                return extractedToken.ToString().Substring("Bearer ".Length).Trim();
            }

            return string.Empty;
        }

        public static string ExtractBearerToken(HttpRequestMessage request)
        {
            if (request == null || request.Headers == null)
            {
                return string.Empty;
            }

            if (request.Headers.TryGetValues("Authorization", out var headerValues))
            {
                string? authorizationHeader = headerValues?.FirstOrDefault();
                if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return authorizationHeader.Substring("Bearer ".Length).Trim();
                }
            }

            return string.Empty;
        }

        public static string ExtractUserEmailFromToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

            if (jwtToken != null)
            {
                var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type.Equals("email", StringComparison.OrdinalIgnoreCase));
                if (emailClaim != null)
                {
                    return emailClaim.Value;
                }
            }

            return string.Empty;
        }
    }
}
