using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using ApiGateway.Identity.Handlers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.UnitTests.Identity.Handlers
{
    public class PulseJwtBearerHandlerTests
    {
        private readonly Mock<IOptionsMonitor<JwtBearerOptions>> _optionsMock;
        private readonly Mock<ILoggerFactory> _loggerFactoryMock;
        private readonly Mock<UrlEncoder> _urlEncoderMock;

        public PulseJwtBearerHandlerTests()
        {
            _optionsMock = new Mock<IOptionsMonitor<JwtBearerOptions>>();
            _loggerFactoryMock = new Mock<ILoggerFactory>();
            _urlEncoderMock = new Mock<UrlEncoder>();
        }

        private static JwtBearerOptions CreateJwtBearerOptions(Action<JwtBearerOptions> configure = null)
        {
            var options = new JwtBearerOptions
            {
                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a_secure_key_that_is_at_least_16_bytes"))
                },
                Events = new JwtBearerEvents()
            };
            configure?.Invoke(options);
            return options;
        }

        private TestablePulseJwtBearerHandler CreateHandler(JwtBearerOptions options, HttpContext context)
        {
            _optionsMock.Setup(o => o.Get(It.IsAny<string>())).Returns(options);

            var handler = new TestablePulseJwtBearerHandler(
                _optionsMock.Object,
                LoggerFactory.Create(builder => builder.AddConsole()),
                Mock.Of<UrlEncoder>()
            );

            handler.InitializeAsync(
                new AuthenticationScheme("Bearer", null, typeof(PulseJwtBearerHandler)),
                context
            ).Wait();

            return handler;
        }

        [Fact]
        public async Task HandleAuthenticateAsync_ReturnsAuthenticateResult_WhenTokenIsValid()
        {
            // Arrange
            var validToken = GenerateJwtToken();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "TestUser") }, "Bearer"));

            var options = CreateJwtBearerOptions(o =>
            {
                o.Events.OnMessageReceived = context =>
                {
                    context.Token = validToken;
                    return Task.CompletedTask;
                };
                o.Events.OnTokenValidated = context =>
                {
                    context.Principal = principal;
                    return Task.CompletedTask;
                };
            });

            var context = new DefaultHttpContext();
            context.Request.Headers["Authorization"] = $"Bearer {validToken}";

            var handler = CreateHandler(options, context);

            // Act
            var result = await handler.AuthenticateAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Succeeded);
            Assert.Equal("TestUser", result.Principal.Identity.Name);
        }

        private string GenerateJwtToken()
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a_secure_key_that_is_at_least_16_bytes"));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "TestUser") }),
                Expires = DateTime.UtcNow.AddMinutes(30),
                SigningCredentials = credentials
            };

            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(tokenDescriptor));
        }

        [Fact]
        public async Task HandleAuthenticateAsync_ReturnsNoResult_WhenTokenIsMissing()
        {
            // Arrange
            var options = CreateJwtBearerOptions();
            var context = new DefaultHttpContext();
            var handler = CreateHandler(options, context);

            // Act
            var result = await handler.AuthenticateAsync();

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Succeeded);
            Assert.Equal(AuthenticateResult.NoResult().Failure, result.Failure);
        }

        [Fact]
        public async Task HandleAuthenticateAsync_ReturnsFailure_WhenTokenIsInvalid()
        {
            // Arrange
            var options = CreateJwtBearerOptions(o =>
            {
                o.Events.OnMessageReceived = context =>
                {
                    context.Token = "invalid_token";
                    return Task.CompletedTask;
                };
            });

            var context = new DefaultHttpContext();
            var handler = CreateHandler(options, context);

            // Act
            var result = await handler.AuthenticateAsync();

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Succeeded);
        }

        [Fact]
        public void CreateErrorDescription_ReturnsCorrectErrorMessages()
        {
            // Arrange
            var aggregateException = new AggregateException(new Exception[]
            {
                new SecurityTokenInvalidAudienceException("Invalid audience.") { InvalidAudience = "TestAudience" },
                new SecurityTokenInvalidIssuerException("Invalid issuer.") { InvalidIssuer = "TestIssuer" },
                new SecurityTokenExpiredException { Expires = DateTime.UtcNow.AddMinutes(-5) }
            });

            var methodInfo = typeof(PulseJwtBearerHandler)
                .GetMethod("CreateErrorDescription", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.NotNull(methodInfo);

            // Act
            var errorDescription = methodInfo?.Invoke(null, new object[] { aggregateException }) as string;

            // Assert
            Assert.NotNull(errorDescription);
            Assert.Contains("The audience 'TestAudience' is invalid", errorDescription);
            Assert.Contains("The issuer 'TestIssuer' is invalid", errorDescription);
            Assert.Contains($"The token expired at '{DateTime.UtcNow.AddMinutes(-5).ToString(CultureInfo.InvariantCulture)}'", errorDescription);
        }

        [Fact]
        public void EventsProperty_ThrowsArgumentNullException_WhenEventsNotSet()
        {
            // Arrange
            var handler = new TestablePulseJwtBearerHandler(
                _optionsMock.Object,
                LoggerFactory.Create(builder => builder.AddConsole()),
                Mock.Of<UrlEncoder>()
            );

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => handler.ExposedEvents);
        }

        [Fact]
        public async Task HandleValidationFailuresAsync_ReturnsAuthenticateResult_WithSingleValidationFailure()
        {
            // Arrange
            var options = CreateJwtBearerOptions();
            var context = new DefaultHttpContext();
            var handler = CreateHandler(options, context);

            var validationFailures = new List<Exception>
            {
                new SecurityTokenInvalidAudienceException("Invalid audience")
            };

            // Act
            var result = await handler.ExposedHandleValidationFailuresAsync(validationFailures);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Succeeded);
            Assert.Equal("Invalid audience", result.Failure.Message);
        }

        // Test subclass
        private class TestablePulseJwtBearerHandler : PulseJwtBearerHandler
        {
            public TestablePulseJwtBearerHandler(IOptionsMonitor<JwtBearerOptions> options, ILoggerFactory logger, UrlEncoder encoder)
                : base(options, logger, encoder)
            {
            }

            public new JwtBearerEvents ExposedEvents => base.Events as JwtBearerEvents ?? throw new ArgumentNullException(nameof(JwtBearerEvents));

            public async Task<AuthenticateResult> ExposedHandleValidationFailuresAsync(List<Exception> validationFailures)
            {
                var methodInfo = typeof(PulseJwtBearerHandler)
                    .GetMethod("HandleValidationFailuresAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (methodInfo == null)
                    throw new MissingMethodException("HandleValidationFailuresAsync not found");

                return await (Task<AuthenticateResult>)methodInfo.Invoke(this, new object[] { validationFailures });
            }
        }
    }
}
