using Gateway.Specflow.Helper;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;

namespace Gateway.Specflow.StepDefinitions
{
    [Binding, Scope(Feature = "Collab")]
    public class CollabStepDefinitions : IClassFixture<WebApplicationFactory<Program>>
    {
        private string? token;
        private readonly HttpClient httpClient;
        private HttpResponseMessage httpResponse;

        public CollabStepDefinitions(WebApplicationFactory<Program> app)
        {
            httpClient = app.CreateClient();
        }

        [Given("collab with valid token")]
        public async Task GivenCollabWithValidToken()
        {
            token = await TokenHelper.GetCollabTokenAsync();
        }

        [Given("collab with invalid or expired token")]
        public void GivenCollabWithInvalidOrExpiredToken()
        {
            token = TokenHelper.GenerateToken();
        }


        [When("collab send a request to ApiGateway")]
        public async Task WhenCollabSendARequestToApiGateway()
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            try
            {
                httpResponse = await httpClient.GetAsync("/gtw/account/api/accounts/currentuser");
            }
            catch(Exception ex) { } // Bypass exception when Gateway call api
        }

        [Then("It return code {int}")]
        public void ThenItReturnCode(int p0)
        {
            Assert.Equal(p0, (int)httpResponse.StatusCode);
        }


        [Then("It send a request to api")]
        public void ThenItSendARequestToApi()
        {
            Assert.Contains("account/api/accounts/currentuser", httpResponse.RequestMessage!.RequestUri!.ToString());
        }

    }
}
