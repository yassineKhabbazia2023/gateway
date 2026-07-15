using System.Net;
using System.Text;
using ApiGateway.Contact;
using ApiGateway.Contact.Models;
using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Models.Requests;
using Microsoft.AspNetCore.Http;
using Moq.Protected;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ApiGateway.UnitTests.Contact;

public class ContactServiceTests
{
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetContactAsync_WhenContactApiResponseNotSuccessfull_ShouldThrowGatewayException(HttpStatusCode statusCode)
    {
        var contactEmail = "jdoe@test.fr";
        var httpResponseMessage = new HttpResponseMessage()
        {
            StatusCode = statusCode
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

        Func<Task> act = async () => await contactService.GetContactAsync(contactEmail);

        (await act.Should().ThrowAsync<GatewayException>())
            .Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        mockContactOperationHandler.Verify();
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

    /// <summary>
    /// Ensures the Contact client forwards the create-new-password payload without entityType by default.
    /// </summary>
    [Fact]
    public async Task CreateNewPasswordAsync_WithoutEntityType_CallsAuthenticationEndpointWithoutQueryAndForwardsPayload()
    {
        // Arrange
        var request = new CreateNewPasswordRequest("reset-token", "NewPassword123", true);
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        string? bodyJson = null;
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, c) =>
            {
                req.Method.Should().Be(HttpMethod.Post);
                req.RequestUri?.PathAndQuery.Should().Be("/authentication/createNewPassword");
                bodyJson = req.Content?.ReadAsStringAsync(c).GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://xyz.fr/")
        };
        var contactService = new ContactService(httpClient);

        // Act
        var result = await contactService.CreateNewPasswordAsync(request, entityType: null, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(bodyJson!);
        json.RootElement.GetProperty("token").GetString().Should().Be(request.token);
        json.RootElement.GetProperty("newPassword").GetString().Should().Be(request.newPassword);
        json.RootElement.GetProperty("isCreatePasswordAction").GetBoolean().Should().BeTrue();
        json.RootElement.TryGetProperty("contactId", out _).Should().BeFalse();
    }

    /// <summary>
    /// Ensures the Contact client sends the entityType query value when the Prospect flow is selected.
    /// </summary>
    [Fact]
    public async Task CreateNewPasswordAsync_WithEntityType_CallsAuthenticationEndpointWithEntityTypeQuery()
    {
        // Arrange
        var request = new CreateNewPasswordRequest("reset-token", "NewPassword123", true);
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, c) =>
            {
                req.Method.Should().Be(HttpMethod.Post);
                req.RequestUri?.PathAndQuery.Should().Be("/authentication/createNewPassword?entityType=PROSPECT");
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://xyz.fr/")
        };
        var contactService = new ContactService(httpClient);

        // Act
        var result = await contactService.CreateNewPasswordAsync(request, "PROSPECT", CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendEmailAsync_WithoutEntityType_CallsBulkInviteEndpointWithBodyAndHeader()
    {
        // Arrange
        const int currentUserId = 123;
        const string accountNumber = "0000000001";
        var customerIds = new[] { 10, 20, 30 };
        var responsePayload = new[] { 1, 2 };
        string? bodyJson = null;

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, c) =>
            {
                req.Method.Should().Be(HttpMethod.Post);
                req.RequestUri?.PathAndQuery.Should().Be($"/customers/bulk-invite/{accountNumber}");
                req.Headers.Contains("CurrentUser").Should().BeTrue();
                req.Headers.GetValues("CurrentUser").Should().ContainSingle().Which.Should().Be(currentUserId.ToString());
                bodyJson = req.Content?.ReadAsStringAsync(c).GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(responsePayload), Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://xyz.fr/")
        };
        var contactService = new ContactService(httpClient);

        // Act
        var result = await contactService.SendEmailAsync(currentUserId, accountNumber, customerIds);

        // Assert
        result.Should().BeEquivalentTo(responsePayload);
        using var json = JsonDocument.Parse(bodyJson!);
        json.RootElement[0].GetInt32().Should().Be(10);
        json.RootElement[1].GetInt32().Should().Be(20);
        json.RootElement[2].GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task SendEmailAsync_WithEntityType_CallsBulkInviteEndpointWithEntityTypeQuery()
    {
        // Arrange
        const int currentUserId = 123;
        const string accountNumber = "0000000001";
        var customerIds = new[] { 10 };
        const string entityType = "PROSPECT FLOW";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, c) =>
            {
                req.Method.Should().Be(HttpMethod.Post);
                req.RequestUri?.PathAndQuery.Should().Be($"/customers/bulk-invite/{accountNumber}?entityType=PROSPECT%20FLOW");
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://xyz.fr/")
        };
        var contactService = new ContactService(httpClient);

        // Act
        var result = await contactService.SendEmailAsync(currentUserId, accountNumber, customerIds, entityType);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SendEmailAsync_WhenResponseIsNotSuccess_ShouldReturnEmptyCollection()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://xyz.fr/")
        };
        var contactService = new ContactService(httpClient);

        // Act
        var result = await contactService.SendEmailAsync(123, "0000000001", [10, 20]);

        // Assert
        result.Should().BeEmpty();
    }
}
