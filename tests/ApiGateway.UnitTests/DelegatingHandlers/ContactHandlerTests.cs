using System.Net;
using ApiGateway.Account;
using ApiGateway.Cache;
using ApiGateway.Contact;
using ApiGateway.Contact.Models;
using ApiGateway.DelegatingHandlers;
using ApiGateway.UnitTests.Mocks;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class ContactHandlerTests
{
    private readonly TestableContactHandler _middleware;
    private readonly Mock<IContactService> _mockContactService;
    private readonly Mock<ILogger<ContactHandler>> _mockLogger;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<IServiceScopeFactory> _mockServiceProviderFactory;
    private readonly Mock<IAccountService> _mockAccountService;

    public ContactHandlerTests()
    {
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Test response")
        };

        var mockHandler = new MockHttpMessageHandler(expectedResponse);
        _mockContactService = new Mock<IContactService>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<ContactHandler>>(MockBehavior.Strict);
        _mockCacheService = new Mock<ICacheService>(MockBehavior.Strict);
        _mockServiceProviderFactory = new Mock<IServiceScopeFactory>(MockBehavior.Loose);
        _mockAccountService = new Mock<IAccountService>(MockBehavior.Strict);
        _middleware = new TestableContactHandler(
            _mockServiceProviderFactory.Object,
            _mockLogger.Object,
            _mockAccountService.Object,
            mockHandler
        );
    }

    [Fact]
    public async Task SendAsync_WhenTokenIsValidAndRequestNotContainsCurrentuserSegement_ShouldAddCurrentContactIdToHeaderRequest()
    {
        // Arrange
        var userEmail = "user@example.com";
        int contactId = 2;
        IEnumerable<KeyValuePair<string, string?>> inMemorySettings =
        new List<KeyValuePair<string, string?>>()
        {
                    new KeyValuePair<string, string?> ("ContactApiUri", "https://contact-domain.api"),
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(IContactService))).Returns(_mockContactService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(ICacheService))).Returns(_mockCacheService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(IConfiguration))).Returns(configuration);

        var mockServiceScope = new Mock<IServiceScope>();
        mockServiceScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockServiceProviderFactory.Setup(x => x.CreateScope()).Returns(mockServiceScope.Object);

        _mockCacheService.Setup(x => x.GetAsync(It.IsAny<string>()))
                .ReturnsAsync((ApiGateway.Contact.Models.Contact?)null)
                .Verifiable();

        _mockCacheService.Setup(x => x.SetContactAsync(userEmail, It.Is<ApiGateway.Contact.Models.Contact>(c=>c.Id == contactId)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _mockContactService.Setup(x => x.GetContactAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApiGateway.Contact.Models.Contact() { Id = contactId })
            .Verifiable();

        _mockLogger
            .Setup(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Passing the following headers to downstream")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)))
            .Verifiable();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://contact-domain.api/contacts?search=firstname");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken(userEmail));

        // Act
        await _middleware.TestSendAsync(request, CancellationToken.None);

        // Assert
        request.RequestUri.Should().Be("https://contact-domain.api/contacts?search=firstname");
        _mockCacheService.Verify(cache => cache.SetContactAsync(userEmail, It.IsAny<ApiGateway.Contact.Models.Contact>()), Times.Once);

        request.Headers.Contains("currentUser").Should().BeTrue("the header 'currentUser' should be present");
        request.Headers.GetValues("currentUser").FirstOrDefault().Should().Be(contactId.ToString(), "the 'currentUser' header should match the specified contactId");
        request.Headers.Contains("ContactEmail").Should().BeTrue("the uploader email must be forwarded downstream");
        request.Headers.GetValues("ContactEmail").Single().Should().Be(userEmail);
    }

    [Fact]
    public async Task SendAsync_WhenTokenIsValidAndRequestContainsCurrentuserSegement_ShouldUpdateRequestAndCachesContactId()
    {
        // Arrange
        var userEmail = "user@example.com";
        int contactId = 2;
        IEnumerable<KeyValuePair<string, string?>> inMemorySettings =
        new List<KeyValuePair<string, string?>>()
        {
                    new KeyValuePair<string, string?> ("ContactApiUri", "https://contact-domain.api"),
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(x => x.GetService(typeof(IContactService))).Returns(_mockContactService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(ICacheService))).Returns(_mockCacheService.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(IConfiguration))).Returns(configuration);

        var mockServiceScope = new Mock<IServiceScope>();
        mockServiceScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockServiceProviderFactory.Setup(x => x.CreateScope()).Returns(mockServiceScope.Object);

        _mockCacheService.Setup(x => x.GetAsync(It.IsAny<string>()))
                .ReturnsAsync((ApiGateway.Contact.Models.Contact?)null)
                .Verifiable();

        _mockCacheService.Setup(x => x.SetContactAsync(userEmail, It.Is<ApiGateway.Contact.Models.Contact>(c => c.Id == contactId)))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _mockContactService.Setup(x => x.GetContactAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApiGateway.Contact.Models.Contact() { Id = contactId })
            .Verifiable();

        _mockLogger
            .Setup(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Passing the following headers to downstream")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)))
            .Verifiable();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://contact-domain.api/contacts/currentuser?search=firstname");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken(userEmail));

        // Act
        await _middleware.TestSendAsync(request, CancellationToken.None);

        // Assert
        request.RequestUri.Should().Be("https://contact-domain.api/contacts?search=firstname&contactId=2");
        _mockCacheService.Verify(cache => cache.SetContactAsync(userEmail, It.IsAny<ApiGateway.Contact.Models.Contact>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenTokenIsValidAndContactIsCached_ShouldUpdatesRequestWithCachedId()
    {
        // Arrange
        var userEmail = "user@example.com";
        int contactId = 2;
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockServiceProviderFactory.Setup(x => x.CreateScope()).Returns(mockServiceScope.Object);
        mockServiceProvider.Setup(x => x.GetService(typeof(ICacheService))).Returns(_mockCacheService.Object);
        _mockCacheService.Setup(x => x.GetAsync(It.IsAny<string>()))
                .ReturnsAsync(new ApiGateway.Contact.Models.Contact() { Id = contactId })
                .Verifiable();

        _mockLogger
            .Setup(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Passing the following headers to downstream")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)))
            .Verifiable();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://contact-domain.api/contacts/currentuser?search=firstname");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken(userEmail));

        // Act
        await _middleware.TestSendAsync(request, CancellationToken.None);

        // Assert
        request.RequestUri.Should().Be("https://contact-domain.api/contacts?search=firstname&contactId=2");
        _mockCacheService.Verify(cache => cache.GetAsync(userEmail), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenTokenIsMissing_ShouldThrowException()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://contact-domain.api/contacts/currentuser?search=firstname");

        // Act
        Func<Task> act = async () => await _middleware.TestSendAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendAsync_WhenClaimEmailIsMissing_ShouldThrowException()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "https://contact-domain.api/contacts/currentuser?search=firstname");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken(string.Empty));

        // Act
        Func<Task> act = async () => await _middleware.TestSendAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>();
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
