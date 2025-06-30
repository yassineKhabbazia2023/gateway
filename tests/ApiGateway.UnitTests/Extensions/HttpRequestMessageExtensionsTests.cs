using ApiGateway.Account;
using ApiGateway.Configuration;
using ApiGateway.Exceptions;
using ApiGateway.Extensions;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.UnitTests.Extensions
{
    public class HttpRequestMessageExtensionsTests
    {
        private readonly Mock<IAccountService> _mockAccountService;
        private readonly string _baseUri;

        public HttpRequestMessageExtensionsTests()
        {
            _mockAccountService = new Mock<IAccountService>();
            _baseUri = "https://contact-domain.api/contacts";
        }

        [Fact]
        public async Task PrepareRequestHeader_BasicCase_ShouldNotModifyHeader()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://domain/api/any");
            var contactEmail = "";
            var expectedRequestUri = request.RequestUri;

            await request.PrepareRequestHeader(contactEmail, null, null, _mockAccountService.Object);

            request.Headers.Should().BeEmpty();
            request.RequestUri.Should().Be(expectedRequestUri);
        }

        [Fact]
        public async Task PrepareRequestHeader_WhenContactIsGiven_ShouldSetContactInHeaders()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _baseUri);
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var contactType = "Collaborateur";
            var expectedRequestUri = request.RequestUri;
            var uriPath = request.RequestUri!.AbsolutePath;

            await request.PrepareRequestHeader(contactEmail, contactId, contactType, _mockAccountService.Object);

            request.Headers.FirstOrDefault(x => x.Key == "ContactEmail").Value.Should().BeEquivalentTo(contactEmail);
            request.Headers.FirstOrDefault(x => x.Key == "CurrentUser").Value.Should().BeEquivalentTo(contactId);
            request.Headers.FirstOrDefault(x => x.Key == "ContactType").Value.Should().BeEquivalentTo(contactType);

            request.RequestUri.Should().BeEquivalentTo(expectedRequestUri);
        }

        [Fact]
        public async Task PrepareRequestHeader_WhenContactIsGivenAndCurrentUser_ShouldSetContactInHeadersAndRequestUri()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUri}/{HttpRequestMessageConstants.CurrentUserUriFragment}?search=firstname");
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var contactType = "Collaborateur";
            var expectedRequestUri = $"{_baseUri}?search=firstname&contactId={contactId}";

            await request.PrepareRequestHeader(contactEmail, contactId, contactType, _mockAccountService.Object);

            request.Headers.FirstOrDefault(x => x.Key == "ContactEmail").Value.Should().BeEquivalentTo(contactEmail);
            request.Headers.FirstOrDefault(x => x.Key == "CurrentUser").Value.Should().BeEquivalentTo(contactId);
            request.Headers.FirstOrDefault(x => x.Key == "ContactType").Value.Should().BeEquivalentTo(contactType);

            request.RequestUri?.ToString().Should().Be(expectedRequestUri);
        }

        [Fact]
        public async Task PrepareRequestHeader_WhenContactIsGivenAndCurrentUser_ShouldSetEmailInRequestUri()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUri}/{HttpRequestMessageConstants.EmailUriFragment}");
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var expectedRequestUri = $"{_baseUri}?email=contact%40email.com";

            await request.PrepareRequestHeader(contactEmail, contactId, null, _mockAccountService.Object);

            request.RequestUri?.ToString().Should().Be(expectedRequestUri);
        }

        [Fact]
        public async Task PrepareRequestHeader_WhenRequestIsNull_ShouldSetEmailInRequestUri()
        {
            HttpRequestMessage? request = null;
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";

            var action = async () => await request!.PrepareRequestHeader(contactEmail, contactId, null, _mockAccountService.Object);

            var result = await Assert.ThrowsAsync<GatewayException>(action);

            Assert.Equal(StatusCodes.Status406NotAcceptable, result.StatusCode);
            Assert.Equal("GTW001", result.Code);
            Assert.Equal("Le paramètre request est null ou vide", result.Message);
        }

        [Fact]
        public async Task PrepareRequestHeader_WhenDeductedAccountNumberInUri_ShouldSetAccountNumber()
        {
            var accountId = 23;
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUri}/{HttpRequestMessageConstants.DownloadStreamUriFragment}?accountId={accountId}");
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var toReturn = new Models.Account
            {
                AccountNumber = "123456"
            };
            var expectedRequestUri = $"{_baseUri}/{toReturn.AccountNumber}?accountId={accountId}";

            _mockAccountService.Setup(x => x.GetAccountAsync(It.IsAny<int>())).ReturnsAsync(toReturn).Verifiable();

            await request.PrepareRequestHeader(contactEmail, contactId, null, _mockAccountService.Object);

            request.RequestUri?.ToString().Should().Be(expectedRequestUri);
        }

        [Fact]
        public async Task PrepareRequestHeader_WhenDeductedAccountNumberInUriWithoutAccountId_ShouldThrowException()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUri}/{HttpRequestMessageConstants.DownloadStreamUriFragment}");
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var expectedRequestUri = $"{_baseUri}?email=contact%40email.com";

            var action = async () => await request.PrepareRequestHeader(contactEmail, contactId, null, _mockAccountService.Object);

            var result = await Assert.ThrowsAsync<GatewayException>(action);

            Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
            Assert.Equal("GTW001", result.Code);
            Assert.Equal("Le paramètre accountId est null ou vide", result.Message);
        }

        [Fact]
        public async Task PrepareRequestHeader_WhenNullAccountService_ShouldThrowGatewayException()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUri}/{HttpRequestMessageConstants.DownloadStreamUriFragment}?accountId={1}");
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var expectedRequestUri = $"{_baseUri}?email=contact%40email.com";

            var action = async () => await request.PrepareRequestHeader(contactEmail, contactId, null, null!);

            var result = await Assert.ThrowsAsync<GatewayException>(action);

            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
            Assert.Equal("GTW002", result.Code);
            Assert.Equal("La configuration accountService est null ou vide", result.Message);
        }

        [Fact]
        public void UriContainsFragment_WhenUriPathContainsFragment_ShouldReturnTrue()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUri}/{HttpRequestMessageConstants.DownloadStreamUriFragment}?accountId={1}");

            var result = request.UriContainsFragment(HttpRequestMessageConstants.DownloadStreamUriFragment);

            Assert.True(result);
        }

        [Fact]
        public void UriContainsFragment_WhenUriPathNotContainsFragment_ShouldReturnFalse()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUri}/{HttpRequestMessageConstants.DownloadStreamUriFragment}?accountId={1}");

            var result = request.UriContainsFragment("toto");

            Assert.False(result);
        }

        [Fact]
        public void ModifyRequestUri_Nominal()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUri}/{HttpRequestMessageConstants.DownloadStreamUriFragment}?accountId={1}");
            request.ModifyRequestUri("accountId", "2", HttpRequestMessageConstants.DownloadStreamUriFragment);

            Assert.Equal("https://contact-domain.api/contacts?accountId=2", request.RequestUri!.ToString());
        }

        [Fact]
        public void ReplaceInRequestUri_Nominal()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUri}/{HttpRequestMessageConstants.DownloadStreamUriFragment}?accountId={1}");
            request.ReplaceInRequestUri(HttpRequestMessageConstants.DownloadStreamUriFragment, "toto");

            Assert.Equal("https://contact-domain.api/contacts/toto?accountId=1", request.RequestUri!.ToString());
        }
    }
}
