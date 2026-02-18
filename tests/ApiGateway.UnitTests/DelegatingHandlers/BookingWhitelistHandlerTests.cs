using ApiGateway.Booking;
using ApiGateway.DelegatingHandlers;
using ApiGateway.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class BookingWhitelistHandlerTests
{
    private static string GenerateDummyJwtToken(string email)
    {
        var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                System.Text.Json.JsonSerializer.Serialize(new { email })))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{header}.{payload}.";
    }

    private static (HttpMessageInvoker invoker, BookingWhitelistHandler handler) CreateHandler(
        Mock<IBookingExperienceGuards> bookingGuards, HttpResponseMessage? innerResponse = null)
    {
        var handler = new BookingWhitelistHandler(bookingGuards.Object, NullLogger<BookingWhitelistHandler>.Instance);

        if (innerResponse is not null)
        {
            var innerMock = new Mock<HttpMessageHandler>();
            innerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(innerResponse);
            handler.InnerHandler = innerMock.Object;
        }

        return (new HttpMessageInvoker(handler), handler);
    }

    [Fact]
    public async Task Should_Return_401_When_Bearer_Token_Is_Missing()
    {
        // Arrange
        var bookingGuards = new Mock<IBookingExperienceGuards>();
        var (invoker, _) = CreateHandler(bookingGuards);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/booking/api/booking-link/currentuser");

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        var expected = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Unauthorized,
            Content = JsonContent.Create(new { ErrorCode = Errors.XpBookingUnauthorizedCode, ErrorMessage = Errors.XpBookingUnauthorizedMessage })
        };
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_Return_401_When_Token_Is_Malformed()
    {
        // Arrange
        var bookingGuards = new Mock<IBookingExperienceGuards>();
        var (invoker, _) = CreateHandler(bookingGuards);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/booking/api/booking-link/currentuser");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        var expected = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Unauthorized,
            Content = JsonContent.Create(new { ErrorCode = Errors.XpBookingUnauthorizedCode, ErrorMessage = Errors.XpBookingUnauthorizedMessage })
        };
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_Return_401_When_User_Not_In_Whitelist()
    {
        // Arrange
        var bookingGuards = new Mock<IBookingExperienceGuards>();
        bookingGuards.Setup(s => s.HasAccess("notallowed@test.fr")).Returns(false);
        var (invoker, _) = CreateHandler(bookingGuards);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/booking/api/booking-link/currentuser");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken("notallowed@test.fr"));

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        var expected = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Unauthorized,
            Content = JsonContent.Create(new { ErrorCode = Errors.XpBookingUnauthorizedCode, ErrorMessage = Errors.XpBookingUnauthorizedMessage })
        };
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_Forward_Request_When_User_In_Whitelist()
    {
        // Arrange
        var bookingGuards = new Mock<IBookingExperienceGuards>();
        bookingGuards.Setup(s => s.HasAccess("user@test.fr")).Returns(true);
        var innerResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var (invoker, _) = CreateHandler(bookingGuards, innerResponse);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com/booking/api/booking-link/currentuser");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateDummyJwtToken("user@test.fr"));

        // Act
        var result = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
