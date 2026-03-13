using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiGateway.Booking;
using ApiGateway.Booking.Models;
using Microsoft.Extensions.Logging;
using Moq.Protected;

namespace ApiGateway.UnitTests.Booking;

public class BookingProvisioningServiceTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<ILogger<BookingProvisioningService>> _mockLogger;
    private readonly BookingProvisioningService _service;

    public BookingProvisioningServiceTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<BookingProvisioningService>>();

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://local.booking/"),
        };

        _mockHttpClientFactory
            .Setup(factory => factory.CreateClient("BookingClient"))
            .Returns(httpClient);

        _service = new BookingProvisioningService(
            _mockHttpClientFactory.Object,
            _mockLogger.Object
        );
    }

    #region CreateBusinessAsync Tests

    [Fact]
    public async Task CreateBusinessAsync_WhenSuccess_ShouldReturnResponse()
    {
        // Arrange
        var businessResponse = new BookingBusinessResponse { Id = 42 };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Created,
            Content = JsonContent.Create(businessResponse),
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post
                    && req.RequestUri!.ToString().Contains("/booking/businesses")
                ),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.CreateBusinessAsync(123, "test-token");

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = JsonSerializer.Deserialize<BookingBusinessResponse>(
            result.Content,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );
        body!.Id.Should().Be(42);
    }

    [Fact]
    public async Task CreateBusinessAsync_ShouldSendCorrectHeaders()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Created,
            Content = JsonContent.Create(new BookingBusinessResponse()),
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _service.CreateBusinessAsync(456, "my-bearer-token");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be("my-bearer-token");
        capturedRequest.Headers.GetValues("CurrentUser").Should().Contain("456");
    }

    [Fact]
    public async Task CreateBusinessAsync_WhenAlreadyConfigured_ShouldReturn200()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(new BookingBusinessResponse { Id = 10 }),
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.CreateBusinessAsync(123, "token");

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateBusinessAsync_WhenBadRequest_ShouldReturnErrorResponse()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent("Graph API error"),
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.CreateBusinessAsync(123, "token");

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region CreateServiceAsync Tests

    [Fact]
    public async Task CreateServiceAsync_WhenSuccess_ShouldReturnCreated()
    {
        // Arrange
        var request = new BookingProvisioningRequest
        {
            DisplayName = "Consultation fiscale",
            Description = "30 minutes",
            IsOnline = true,
            Duration = 30,
            Availability =
            [
                new BookingServiceAvailabilityRequest
                {
                    Day = "monday",
                    TimeSlots =
                    [
                        new BookingServiceTimeSlotRequest { Start = "09:00", End = "12:00" },
                    ],
                },
            ],
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Created,
            Content = new StringContent("{}"),
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post
                    && req.RequestUri!.ToString().Contains("/booking/businesses/42/services")
                ),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.CreateServiceAsync("42", 123, request, "token");

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateServiceAsync_ShouldSendCorrectHeadersAndUrl()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        var request = new BookingProvisioningRequest { DisplayName = "Test", Duration = 30 };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Created,
            Content = new StringContent("{}"),
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _service.CreateServiceAsync("99", 789, request, "my-token");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Contain("/booking/businesses/99/services");
        capturedRequest.RequestUri.ToString().Should().Contain("ContactId=789");
        capturedRequest.Headers.Authorization!.Parameter.Should().Be("my-token");
        capturedRequest.Headers.GetValues("CurrentUser").Should().Contain("789");
    }

    [Fact]
    public async Task CreateServiceAsync_WhenAlreadyExists_ShouldReturn200()
    {
        // Arrange
        var request = new BookingProvisioningRequest { DisplayName = "Test", Duration = 30 };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{}"),
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.CreateServiceAsync("42", 123, request, "token");

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateServiceAsync_WhenBadRequest_ShouldReturnError()
    {
        // Arrange
        var request = new BookingProvisioningRequest { DisplayName = "Test", Duration = 30 };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent("Validation error"),
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.CreateServiceAsync("42", 123, request, "token");

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateServiceAsync_ShouldSerializeRequestPayload()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        var request = new BookingProvisioningRequest
        {
            DisplayName = "Consultation fiscale",
            Description = "30 minutes",
            IsOnline = true,
            Duration = 30,
            Availability =
            [
                new BookingServiceAvailabilityRequest
                {
                    Day = "monday",
                    TimeSlots =
                    [
                        new BookingServiceTimeSlotRequest { Start = "09:00", End = "12:00" },
                        new BookingServiceTimeSlotRequest { Start = "14:00", End = "17:00" },
                    ],
                },
            ],
        };

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Created,
            Content = new StringContent("{}"),
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _service.CreateServiceAsync("42", 123, request, "token");

        // Assert
        capturedRequest.Should().NotBeNull();
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        content.Should().Contain("Consultation fiscale");
        content.Should().Contain("monday");
        content.Should().Contain("09:00");
        content.Should().Contain("14:00");
    }

    #endregion

    #region DeleteBusinessAsync Tests

    [Fact]
    public async Task DeleteBusinessAsync_WhenSuccess_ShouldReturnNoContent()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NoContent,
            Content = new StringContent(""),
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Delete
                    && req.RequestUri!.ToString().Contains("/api/booking/businesses/email")
                ),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.DeleteBusinessAsync("test@example.com", 123, "token");

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteBusinessAsync_ShouldSendCorrectUrlAndHeaders()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.NoContent,
            Content = new StringContent(""),
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _service.DeleteBusinessAsync("biz@kpmg.fr", 456, "my-token");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Delete);
        capturedRequest.RequestUri!.ToString().Should().Contain("/api/booking/businesses/email");
        capturedRequest.RequestUri.ToString().Should().Contain("businessId=biz@kpmg.fr");
        capturedRequest.RequestUri.ToString().Should().NotContain("ContactId=");
        capturedRequest.Headers.Authorization!.Parameter.Should().Be("my-token");
        capturedRequest.Headers.GetValues("CurrentUser").Should().Contain("456");
    }

    [Fact]
    public async Task DeleteBusinessAsync_WhenError_ShouldReturnErrorResponse()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent("Error message"),
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.DeleteBusinessAsync("test@example.com", 123, "token");

        // Assert
        result.IsSuccessStatusCode.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Content.Should().Be("Error message");
    }

    #endregion

}
