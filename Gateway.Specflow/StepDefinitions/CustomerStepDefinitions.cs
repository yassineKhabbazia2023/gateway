using Gateway.Specflow.Helper;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;

namespace Gateway.Specflow.StepDefinitions
{
    [Binding, Scope(Feature = "Customer")]
    public class CustomerStepDefinitions : IClassFixture<WebApplicationFactory<Program>>
    {
        private string? token;
        private HttpClient httpClient;
        private HttpResponseMessage httpResponse;

        public CustomerStepDefinitions(WebApplicationFactory<Program> app)
        {
            httpClient = app.CreateClient();
        }

        [Given("customer with invalid or expired token")]
        public void GivenCustomerWithInvalidOrExpiredToken()
        {
            token = TokenHelper.GenerateToken();
        }

        [Given("customer with valid token")]
        public async Task GivenCollabWithValidToken()
        {
            token = await TokenHelper.GetCustomerToken();
        }

        [When("customer send a request to ApiGateway")]
        public async Task WhenCustomerSendARequestToApiGateway()
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            try
            {
                httpResponse = await httpClient.GetAsync("/gtw/account/api/accounts/currentuser");
            }
            catch (Exception ex) { }
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
