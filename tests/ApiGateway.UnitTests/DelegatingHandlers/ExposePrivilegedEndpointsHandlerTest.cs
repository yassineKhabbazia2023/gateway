using ApiGateway.DelegatingHandlers;
using ApiGateway.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class ExposePrivilegedEndpointsHandlerTest
{
    [Fact]
    public async Task Should_Respond_With_403_When_ExposePrivilegedEndpointsFlag_IsFalse()
    {
        var expected = new HttpResponseMessage 
        { 
            StatusCode=HttpStatusCode.Forbidden, 
            Content = JsonContent.Create(new 
                { 
                    ErrorMessage = Errors.UnauthorizedExposePrivilegedEndpointsCode, 
                    ErrorCode = Errors.UnauthorizedExposePrivilegedEndpointsMessage
                })
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var sut = new ExposePrivilegedEndpointsHandler(configuration, NullLogger<ExposePrivilegedEndpointsHandler>.Instance);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://dummy");
        var invoker = new HttpMessageInvoker(sut);

        var result = await invoker.SendAsync(request, CancellationToken.None);
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_Respond_With_200_When_ExposePrivilegedEndpointsFlag_IsTrue()
    {
        var dictionary = new Dictionary<string, string?> {
            {ExposePrivilegedEndpointsHandler.ExposePrivilegedEndpoints, "true"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(dictionary)
            .Build();

        var request = new HttpRequestMessage(HttpMethod.Get, "https://dummy");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { message = "Yeah!" })
        };

        var innerHandlerMock = new Mock<HttpMessageHandler>();
        innerHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var sut = new ExposePrivilegedEndpointsHandler(configuration, NullLogger<ExposePrivilegedEndpointsHandler>.Instance)
        {
            InnerHandler = innerHandlerMock.Object
        };

        var invoker = new HttpMessageInvoker(sut);

        var result = await invoker.SendAsync(request, CancellationToken.None);
        result.Should().BeEquivalentTo(response);
    }
}
