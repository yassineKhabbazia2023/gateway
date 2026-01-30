using System.Net;
using System.Text;
using ApiGateway.Contact;
using ApiGateway.Contact.Exceptions;
using ApiGateway.Contact.Models;
using ApiGateway.Exceptions;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ApiGateway.UnitTests.Contact;

public class ContactServiceTests
{
    [Fact]
    public async Task GetContactAsync_WhenContactReturnNotFoundError_ShouldReturnNull()
    {
        var contactEmail = "jdoe@test.fr";
        var httpResponseMessage = new HttpResponseMessage()
        {
            StatusCode = HttpStatusCode.NotFound
        };

        var mockContactOperationHandler = new Mock<HttpMessageHandler>();

        var client = new HttpClient(mockContactOperationHandler.Object);
        client.BaseAddress = new Uri("http://xyz.fr");

        mockContactOperationHandler.Protected().Setup<Task<HttpResponseMessage>>(
           "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
           .Callback<HttpRequestMessage, CancellationToken>((req, c) =>
           {
               var encodedEmail = WebUtility.UrlEncode(contactEmail);
               req.Method.Should().Be(HttpMethod.Get);
               req?.RequestUri?.PathAndQuery.Should().Be($"/contacts?Email={encodedEmail}");
           }).ReturnsAsync(httpResponseMessage).Verifiable();

        var contactService = new ContactService(client);

        var result = await contactService.GetContactAsync(contactEmail);

        mockContactOperationHandler.Verify();
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetContactAsync_WhenContactApiResponseNotSuccessfull_ShouldReturnNull()
    {
        var contactEmail = "jdoe@test.fr";
        var httpResponseMessage = new HttpResponseMessage()
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = null
        };

        var mockContactOperationHandler = new Mock<HttpMessageHandler>();

        var client = new HttpClient(mockContactOperationHandler.Object);
        client.BaseAddress = new Uri("http://xyz.fr");

        mockContactOperationHandler.Protected().Setup<Task<HttpResponseMessage>>(
           "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
           .Callback<HttpRequestMessage, CancellationToken>((req, c) =>
           {
               var encodedEmail = WebUtility.UrlEncode(contactEmail);
               req.Method.Should().Be(HttpMethod.Get);
               req?.RequestUri?.PathAndQuery.Should().Be($"/contacts?Email={encodedEmail}");
           }).ReturnsAsync(httpResponseMessage).Verifiable();

        var contactService = new ContactService(client);

        var result = await contactService.GetContactAsync(contactEmail);

        mockContactOperationHandler.Verify();
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetContactAsync_WhenContactApiReturnNull_ShouldReturnNull()
    {
        var contactEmail = "jdoe@test.fr";

        var httpResponseMessage = new HttpResponseMessage()
        {
            StatusCode = HttpStatusCode.OK,
            Content = null
        };

        var mockContactOperationHandler = new Mock<HttpMessageHandler>();

        var client = new HttpClient(mockContactOperationHandler.Object);
        client.BaseAddress = new Uri("http://xyz.fr");

        mockContactOperationHandler.Protected().Setup<Task<HttpResponseMessage>>(
           "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
           .Callback<HttpRequestMessage, CancellationToken>((req, c) =>
           {
               var encodedEmail = WebUtility.UrlEncode(contactEmail);
               req.Method.Should().Be(HttpMethod.Get);
               req?.RequestUri?.PathAndQuery.Should().Be($"/contacts?Email={encodedEmail}");
           }).ReturnsAsync(httpResponseMessage).Verifiable();

        var contactService = new ContactService(client);

        var result = await contactService.GetContactAsync(contactEmail);

        mockContactOperationHandler.Verify();
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetContactAsync_WhenContactNotFound_ShouldReturnNull()
    {
        var contactEmail = "jdoe@test.fr";
        var pagedContact = new PagingResult()
        {
            Items = new List<ApiGateway.Contact.Models.Contact>()
        };

        var httpResponseMessage = new HttpResponseMessage()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonConvert.SerializeObject(pagedContact, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }), Encoding.UTF8, "application/json")
        };

        var mockContactOperationHandler = new Mock<HttpMessageHandler>();

        var client = new HttpClient(mockContactOperationHandler.Object);
        client.BaseAddress = new Uri("http://xyz.fr");

        mockContactOperationHandler.Protected().Setup<Task<HttpResponseMessage>>(
           "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
           .Callback<HttpRequestMessage, CancellationToken>((req, c) =>
           {
               var encodedEmail = WebUtility.UrlEncode(contactEmail);
               req.Method.Should().Be(HttpMethod.Get);
               req?.RequestUri?.PathAndQuery.Should().Be($"/contacts?Email={encodedEmail}");
           }).ReturnsAsync(httpResponseMessage).Verifiable();

        var contactService = new ContactService(client);

        Func<Task> act = async () =>
        {
            await contactService.GetContactAsync(contactEmail);
        };

        await act.Should().ThrowAsync<GatewayException>();

        mockContactOperationHandler.Verify();
    }

    [Fact]
    public async Task GetContactAsync_ShouldReturnContact()
    {
        var contactEmail = "jdoe@test.fr";
        var contact = new ApiGateway.Contact.Models.Contact()
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = contactEmail
        };

        var pagedContact = new PagingResult()
        {
            Items = new List<ApiGateway.Contact.Models.Contact>()
            {
                contact
            }
        };

        var httpResponseMessage = new HttpResponseMessage()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonConvert.SerializeObject(pagedContact, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }), Encoding.UTF8, "application/json")
        };

        var mockContactOperationHandler = new Mock<HttpMessageHandler>();

        var client = new HttpClient(mockContactOperationHandler.Object);
        client.BaseAddress = new Uri("http://xyz.fr");

        mockContactOperationHandler.Protected().Setup<Task<HttpResponseMessage>>(
           "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
           .Callback<HttpRequestMessage, CancellationToken>((req, c) =>
           {
               var encodedEmail = WebUtility.UrlEncode(contactEmail);
               req.Method.Should().Be(HttpMethod.Get);
               req?.RequestUri?.PathAndQuery.Should().Be($"/contacts?Email={encodedEmail}");
           }).ReturnsAsync(httpResponseMessage).Verifiable();

        var contactService = new ContactService(client);
                
        var result = await contactService.GetContactAsync(contactEmail);

        mockContactOperationHandler.Verify();
        result.Should().BeEquivalentTo(contact);
    }

    [Fact]
    public async Task GetContactIdAsync_ShouldReturnContact()
    {
        // Arrange
        int contactId = 2;
        var userEmail = "user@example.com";

        PagingResult pagingResult = new PagingResult()
        {
            Items = new List<ApiGateway.Contact.Models.Contact>
        {
            new ApiGateway.Contact.Models.Contact { Id = contactId },
        }
        };
        var jsonString = System.Text.Json.JsonSerializer.Serialize(pagingResult);

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
        mockHttpClient.BaseAddress = new Uri("http://xyz.fr");

        var contactService = new ContactService(mockHttpClient);

        // Act
        var result = await contactService.GetContactIdAsync(userEmail);

        // Assert
        result.Should().Be(contactId.ToString());
    }

    [Fact]
    public async Task GetContactIdAsync_WhenContactNotFound_ShouldReturnNull()
    {
        // Arrange
        var userEmail = "user@example.com";

        PagingResult pagingResult = new PagingResult()
        {
            Items = new List<ApiGateway.Contact.Models.Contact>()
        };
        var jsonString = System.Text.Json.JsonSerializer.Serialize(pagingResult);

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
        mockHttpClient.BaseAddress = new Uri("http://xyz.fr");

        var contactService = new ContactService(mockHttpClient);

        // Act
        var result = await contactService.GetContactIdAsync(userEmail);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetContactIdAsync_WhenContactApiResponseNotSuccessfull_ShouldReturnNull()
    {
        // Arrange
        var userEmail = "user@example.com";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = null
            });

        var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);
        mockHttpClient.BaseAddress = new Uri("http://xyz.fr");

        var contactService = new ContactService(mockHttpClient);

        // Act
        var result = await contactService.GetContactIdAsync(userEmail);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetContactIdAsync_WhenContactApiReturnNull_ShouldReturnNull()
    {
        // Arrange
        var userEmail = "user@example.com";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = null
            });

        var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);
        mockHttpClient.BaseAddress = new Uri("http://xyz.fr");

        var contactService = new ContactService(mockHttpClient);

        // Act
        var result = await contactService.GetContactIdAsync(userEmail);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetContactIdAsync_WhenContactReturnNotFoundError_ShouldReturnNull()
    {
        // Arrange
        var userEmail = "user@example.com";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);
        mockHttpClient.BaseAddress = new Uri("http://xyz.fr");

        var contactService = new ContactService(mockHttpClient);

        // Act
        var result = await contactService.GetContactIdAsync(userEmail);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetContactByIdAsync_ShouldReturnContact()
    {
        // Arrange
        var contactId = 123;
        var contact = new ApiGateway.Contact.Models.Contact
        {
            Id = contactId,
            FirstName = "John",
            LastName = "Doe",
            Email = "jdoe@test.fr",
            Type = "Customer",
            MobilePhone = "+33612345678"
        };

        var jsonString = System.Text.Json.JsonSerializer.Serialize(contact);

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, c) =>
            {
                req.Method.Should().Be(HttpMethod.Get);
                req.RequestUri?.PathAndQuery.Should().Be($"/contact/{contactId}");
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
            });

        var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);
        mockHttpClient.BaseAddress = new Uri("http://xyz.fr");

        var contactService = new ContactService(mockHttpClient);

        // Act
        var result = await contactService.GetContactByIdAsync(contactId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(contactId);
        result.FirstName.Should().Be("John");
        result.Type.Should().Be("Customer");
        result.MobilePhone.Should().Be("+33612345678");
    }

    [Fact]
    public async Task GetContactByIdAsync_WhenContactNotFound_ShouldReturnNull()
    {
        // Arrange
        var contactId = 999;

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);
        mockHttpClient.BaseAddress = new Uri("http://xyz.fr");

        var contactService = new ContactService(mockHttpClient);

        // Act
        var result = await contactService.GetContactByIdAsync(contactId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetContactByIdAsync_WhenApiReturnsEmptyContent_ShouldReturnNull()
    {
        // Arrange
        var contactId = 123;

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
                Content = new StringContent("", Encoding.UTF8, "application/json")
            });

        var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);
        mockHttpClient.BaseAddress = new Uri("http://xyz.fr");

        var contactService = new ContactService(mockHttpClient);

        // Act
        var result = await contactService.GetContactByIdAsync(contactId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetContactByIdAsync_WhenApiBadRequest_ShouldReturnNull()
    {
        // Arrange
        var contactId = 123;

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest
            });

        var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);
        mockHttpClient.BaseAddress = new Uri("http://xyz.fr");

        var contactService = new ContactService(mockHttpClient);

        // Act
        var result = await contactService.GetContactByIdAsync(contactId);

        // Assert
        result.Should().BeNull();
    }
}
