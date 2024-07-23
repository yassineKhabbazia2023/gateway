using ApiGateway.Identity.Options;
using ApiGateway.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq.Protected;
using System.Net;
using System.Security.Claims;
using ApiGateway.Identity.Exceptions;
using ApiGateway.Identity.Models;
using Newtonsoft.Json;
using System.Net.Http;
using ApiGateway.Contact.Exceptions;
using Ocelot.Values;

namespace ApiGateway.UnitTests.Identity
{
    public class IdentityServiceTests
    {
        private readonly Mock<HttpClient> mockHttpClient;
        private readonly Mock<IOptions<IdentityServiceOptions>> mockOptions;
        private readonly IdentityService identityService;
        private readonly HttpContext httpContext;

        public IdentityServiceTests()
        {
            mockHttpClient = new Mock<HttpClient>();
            mockOptions = new Mock<IOptions<IdentityServiceOptions>>();
            var optionsValue = new IdentityServiceOptions
            {
                GigyaApiUri = "https://gigya.api.endpoint",
                CollaboratorRole = "Collaborator",
                CustomerRole = "Customer",
                CollaboratorsSecurityGroup = "collaborators-group",
                GigyaApiKey = "api-key",
                GigyaSecret = "secret",
                GigyaUserKey = "user-key"
            };
            mockOptions.Setup(o => o.Value).Returns(optionsValue);
            identityService = new IdentityService(mockHttpClient.Object, mockOptions.Object);

            // Setup HttpContext with a user and claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, "Collaborator"),
                new Claim("groups", "collaborators-group")
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var principal = new ClaimsPrincipal(identity);
            httpContext = new DefaultHttpContext { User = principal };
        }

        [Fact]
        public void IsCollaborator_ShouldReturnTrue_WhenUserIsCollaborator()
        {
            // Act
            var result = identityService.IsCollaborator(httpContext);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsCustomer_ShouldReturnTrue_WhenUserIsCustomer()
        {
            // Setup
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { new Claim(ClaimTypes.Role, "Customer") }, "TestAuthType"));

            // Act
            var result = identityService.IsCustomer(httpContext);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateCollaboratorAsync_ShouldReturnTrue_WhenUserIsInCollaboratorsSecurityGroup()
        {
            // Act
            var result = identityService.ValidateCollaborator(httpContext);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateCustomerAsync_ShouldReturnTrue_WhenUserExistsInGigya()
        {
            // Setup mock HTTP response
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"totalCount\":1}")
                });

            var httpClient = new HttpClient(handlerMock.Object);
            httpClient.BaseAddress = new Uri("https://gigya.api.endpoint");

            var identityService = new IdentityService(httpClient, mockOptions.Object);

            // Act
            var result = await identityService.ValidateCustomerAsync("test@example.com");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateCustomerAsync_ShouldReturnFalse_WhenUserDoesNotExistInGigya()
        {
            // Setup mock HTTP response
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"totalCount\":0}")
                });

            var httpClient = new HttpClient(handlerMock.Object);
            httpClient.BaseAddress = new Uri("https://gigya.api.endpoint");

            var identityService = new IdentityService(httpClient, mockOptions.Object);

            // Act
            var result = await identityService.ValidateCustomerAsync("test@example.com");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CheckUserExistsInGigyaAsync_ShouldThrowException_WhenDeserializationFails()
        {
            // Arrange
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("INVALID")
                });

            var httpClient = new HttpClient(handlerMock.Object);
            httpClient.BaseAddress = new Uri("https://gigya.api.endpoint");


            var identityService = new IdentityService(httpClient, mockOptions.Object);

            // Act
            Func<Task> act = async () => await identityService.ValidateCustomerAsync("test@example.com");

            // Assert
            await act.Should().ThrowAsync<GigyaOperationException>().WithMessage("Error deserializing Gigya response.");
        }

        [Fact]
        public async Task CheckUserExistsInGigyaAsync_ShouldThrowException_WhenGigyaReturnsAnomaly()
        {
            // Arrange
            var responseContent = JsonConvert.SerializeObject(new GigyaResponse
            {
                ErrorCode = 123,
                ErrorDetails = "An error occurred"
            });

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent(responseContent)
                });

            var httpClient = new HttpClient(handlerMock.Object);
            httpClient.BaseAddress = new Uri("https://gigya.api.endpoint");


            var identityService = new IdentityService(httpClient, mockOptions.Object);

            // Act
            Func<Task> act = async () => await identityService.ValidateCustomerAsync("test@example.com");

            // Assert
            await act.Should().ThrowAsync<GigyaOperationException>().WithMessage("Something went wrong while communicating with Gigya, details: Anomaly { errorCode = 123, errorDetails = An error occurred }");
        }
    }
}
