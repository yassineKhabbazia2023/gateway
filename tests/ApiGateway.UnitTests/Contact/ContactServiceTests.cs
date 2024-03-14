using System.Net;
using System.Text;
using System.Text.Json;
using ApiGateway.Contact;
using ApiGateway.Contact.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq.Protected;

namespace ApiGateway.UnitTests.Contact;

public class ContactServiceTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public ContactServiceTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
    }

    [Fact]
    public async Task GetContactAsync_ShouldReturnContact()
    {
        // Arrange
        int contactId = 2;
        var userEmail = "user@example.com";
        var contactApiUri = "https://contact-domain.api";

        PagingResult pagingResult = new PagingResult()
        {
            Items = new List<ApiGateway.Contact.Models.Contact>
        {
            new ApiGateway.Contact.Models.Contact { Id = contactId },
        }
        };
        var jsonString = JsonSerializer.Serialize(pagingResult);

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
            });

        var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("contact_client")).Returns(mockHttpClient);

        var contactService = new ContactService(_mockHttpClientFactory.Object);

        // Act
        var result = await contactService.GetContactAsync(contactApiUri, userEmail);

        // Assert
        result.Should().Be(contactId.ToString());
    }

    [Fact]
    public async Task GetContactAsync_EmptyResponse_ReturnsNull()
    {
        // Arrange
        var userEmail = "user@example.com";
        var contactApiUri = "https://contact-domain.api";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient("contact_client")).Returns(mockHttpClient);

        var contactService = new ContactService(_mockHttpClientFactory.Object);

        // Act
        var result = await contactService.GetContactAsync(contactApiUri, userEmail);

        // Assert
        result.Should().BeNull();
    }
}
