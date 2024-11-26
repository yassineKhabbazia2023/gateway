using ApiGateway.Account;
using ApiGateway.Configuration;
using ApiGateway.Extensions;
using ApiGateway.Models;
using Microsoft.Identity.Client;

namespace ApiGateway.UnitTests.Extensions
{
    public class HttpRequestMessageExtensionsTests
    {
        private readonly Mock<IAccountService> _mockAccountService;

        public HttpRequestMessageExtensionsTests()
        {
            _mockAccountService = new Mock<IAccountService>();
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
            var request = new HttpRequestMessage(HttpMethod.Get, "https://contact-domain.api/contacts");
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
            var baseUri = "https://contact-domain.api/contacts";
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUri}/{HttpRequestMessageConstants.CurrentUserUriFragment}?search=firstname");
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var contactType = "Collaborateur";
            var expectedRequestUri = $"{baseUri}?search=firstname&contactId={contactId}";

            await request.PrepareRequestHeader(contactEmail, contactId, contactType, _mockAccountService.Object);

            request.Headers.FirstOrDefault(x => x.Key == "ContactEmail").Value.Should().BeEquivalentTo(contactEmail);
            request.Headers.FirstOrDefault(x => x.Key == "CurrentUser").Value.Should().BeEquivalentTo(contactId);
            request.Headers.FirstOrDefault(x => x.Key == "ContactType").Value.Should().BeEquivalentTo(contactType);

            request.RequestUri?.ToString().Should().Be(expectedRequestUri);
        }

        [Fact]
        public async Task PrepareRequestHeader_WhenContactIsGivenAndCurrentUser_ShouldSetEmailInRequestUri()
        {
            var baseUri = "https://contact-domain.api/contacts";
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUri}/{HttpRequestMessageConstants.EmailUriFragment}");
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var expectedRequestUri = $"{baseUri}?email=contact%40email.com";

            await request.PrepareRequestHeader(contactEmail, contactId, null, _mockAccountService.Object);

            request.RequestUri?.ToString().Should().Be(expectedRequestUri);
        }

        [Fact]
        public async Task PrepareRequestHeader_WhenRequestIsNull_ShouldSetEmailInRequestUri()
        {
            HttpRequestMessage? request = null;
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";

            var action = async () => await request.PrepareRequestHeader(contactEmail, contactId, null, _mockAccountService.Object);

            await action.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task PrepareRequestHeader_WhenDeductedAccountNumberInUri_ShouldSetAccountNumber()
        {
            var baseUri = "https://contact-domain.api/contacts";
            var accountId = 23;
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUri}/{HttpRequestMessageConstants.DownloadStreamUriFragment}?accountId={accountId}");
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var toReturn = new Models.Account
            {
                AccountNumber = "123456"
            };
            var expectedRequestUri = $"{baseUri}/{toReturn.AccountNumber}?accountId={accountId}";

            _mockAccountService.Setup(x => x.GetAccountAsync(It.IsAny<int>())).ReturnsAsync(toReturn).Verifiable();

            await request.PrepareRequestHeader(contactEmail, contactId, null, _mockAccountService.Object);

            request.RequestUri?.ToString().Should().Be(expectedRequestUri);
        }

        [Fact]
        public async Task PrepareRequestHeader_WhenDeductedAccountNumberInUriWithoutAccountId_ShouldThrowException()
        {
            var baseUri = "https://contact-domain.api/contacts";
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUri}/{HttpRequestMessageConstants.DownloadStreamUriFragment}");
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var expectedRequestUri = $"{baseUri}?email=contact%40email.com";

            var action = async () => await request.PrepareRequestHeader(contactEmail, contactId, null, _mockAccountService.Object);

            await action.Should().ThrowAsync<ArgumentException>();
        }
    }
}
