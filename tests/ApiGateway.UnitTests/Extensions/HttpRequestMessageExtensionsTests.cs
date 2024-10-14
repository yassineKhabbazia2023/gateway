using ApiGateway.Account;
using ApiGateway.Configuration;
using ApiGateway.Extensions;

namespace ApiGateway.UnitTests.Extensions
{
    public class HttpRequestMessageExtensionsTests
    {
        [Fact]
        public void PrepareRequestHeader_BasicCase_ShouldNotModifyHeader()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://domain/api/any");
            var contactEmail = "";
            var expectedRequestUri = request.RequestUri;

            request.PrepareRequestHeader(contactEmail, null);

            request.Headers.Should().BeEmpty();
            request.RequestUri.Should().Be(expectedRequestUri);
        }

        [Fact]
        public void PrepareRequestHeader_WhenContactIsGiven_ShouldSetContactInHeaders()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://contact-domain.api/contacts");
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var expectedRequestUri = request.RequestUri;
            var uriPath = request.RequestUri!.AbsolutePath;

            request.PrepareRequestHeader(contactEmail, contactId);

            request.Headers.FirstOrDefault(x => x.Key == "ContactEmail").Value.Should().BeEquivalentTo(contactEmail);
            request.Headers.FirstOrDefault(x => x.Key == "CurrentUser").Value.Should().BeEquivalentTo(contactId);

            request.RequestUri.Should().BeEquivalentTo(expectedRequestUri);
        }

        [Fact]
        public void PrepareRequestHeader_WhenContactIsGivenAndCurrentUser_ShouldSetContactInHeadersAndRequestUri()
        {
            var baseUri = "https://contact-domain.api/contacts";
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUri}/{HttpRequestMessageConstants.CurrentUserUriFragment}?search=firstname");
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var expectedRequestUri = $"{baseUri}?search=firstname&contactId={contactId}";

            request.PrepareRequestHeader(contactEmail, contactId);

            request.Headers.FirstOrDefault(x => x.Key == "ContactEmail").Value.Should().BeEquivalentTo(contactEmail);
            request.Headers.FirstOrDefault(x => x.Key == "CurrentUser").Value.Should().BeEquivalentTo(contactId);

            request.RequestUri?.ToString().Should().Be(expectedRequestUri);
        }

        [Fact]
        public void PrepareRequestHeader_WhenContactIsGivenAndCurrentUser_ShouldSetEmailInRequestUri()
        {
            var baseUri = "https://contact-domain.api/contacts";
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUri}/{HttpRequestMessageConstants.EmailUriFragment}");
            var contactEmail = "contact@email.com";
            var contactId = "contact-id";
            var expectedRequestUri = $"{baseUri}?email=contact%40email.com";

            request.PrepareRequestHeader(contactEmail, contactId);

            request.RequestUri?.ToString().Should().Be(expectedRequestUri);
        }
    }
}
