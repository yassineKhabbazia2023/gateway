using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Gateway.Specflow.Helper
{
    public static class TokenHelper
    {
        public static async Task<string> GetCollabTokenAsync()
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://login.microsoftonline.com/prekpmgnetfr.onmicrosoft.com/oauth2/v2.0/token");
            request.Headers.Add("Cookie", "fpc=AqlM62J6AoxFgUr8HCfJ_EuKAWvMAQAAAOtZ49oOAAAA; fpc=AqlM62J6AoxFgUr8HCfJ_EteMmmCAQAAAL_fTdsOAAAA; stsservicecookie=estsfd; x-ms-gateway-slice=estsfd");
            var collection = new List<KeyValuePair<string, string>>()
            {
                new("client_id", "e90c4fff-87c7-4c05-b3fd-a2a06650b380"),
                new("client_secret", "Q2vq5CTpXuXFovHZjQfMWTj92x1E9AApZgm6vWZeZws="),
                new("grant_type", "client_credentials"),
                new("scope", "https://prekpmgnetfr.onmicrosoft.com/CSTD2API/.default")
            };
            var content = new FormUrlEncodedContent(collection);
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var jObj = JObject.Parse(await response.Content.ReadAsStringAsync());
            return (string)jObj["access_token"]!;
        }

        public static string GenerateToken()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor()
            {
                Expires = DateTime.Now.AddMinutes(10),
            };
            var stoken = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(stoken);
        }

        public async static Task<string> GetCustomerToken()
        {
            using (var client = new HttpClient())
            {
                var url = "https://api-itg01.itg.kpmg-pulse.fr/contact/api/authentication/refreshToken?userId=992aa5372f8442e58789c8118dcd9f0b";
                var response = await client.PostAsync(url, null);
                return await response.Content.ReadAsStringAsync();
            }
        }
    }
}
