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
        private readonly Mock<IAuthenticationSchemeProvider> _schemeProviderMock;

        public PulseJwtBearerHandlerTests()
        {
            _optionsMock = new Mock<IOptionsMonitor<JwtBearerOptions>>();
            _loggerFactoryMock = new Mock<ILoggerFactory>();
            _urlEncoderMock = new Mock<UrlEncoder>();
            _schemeProviderMock = new Mock<IAuthenticationSchemeProvider>();
        }

        [Fact]
        public async Task HandleAuthenticateAsync_ReturnsAuthenticateResult_WhenTokenIsValid()
        {
            // Arrange
            var validToken = GenerateJwtToken(); // Generate a valid JWT token
            var claims = new[] { new Claim(ClaimTypes.Name, "TestUser") };
            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            var options = new JwtBearerOptions
            {
                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a_secure_key_that_is_at_least_16_bytes"))
                },
                Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Simulate extracting the token from the request
                        context.Token = validToken;
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        // Simulate successful token validation
                        context.Principal = principal;
                        return Task.CompletedTask;
                    }
                }
            };

            // Add the JwtSecurityTokenHandler as the default token validator
            options.SecurityTokenValidators.Clear();
            options.SecurityTokenValidators.Add(new JwtSecurityTokenHandler());

            var optionsMonitorMock = new Mock<IOptionsMonitor<JwtBearerOptions>>();
            optionsMonitorMock.Setup(o => o.Get(It.IsAny<string>())).Returns(options);

            var context = new DefaultHttpContext();
            context.Request.Headers["Authorization"] = $"Bearer {validToken}";

            var scheme = new AuthenticationScheme("Bearer", null, typeof(PulseJwtBearerHandler));

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole()); // Create a test logger

            var handler = new PulseJwtBearerHandler(
                optionsMonitorMock.Object,
                loggerFactory,
                Mock.Of<UrlEncoder>()
            );

            // Ensure the handler is properly initialized with scheme and context
            await handler.InitializeAsync(scheme, context);

            // Act
            var result = await handler.AuthenticateAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Principal);
            Assert.Equal("TestUser", result.Principal.Identity.Name);
        }

        private string GenerateJwtToken()
        {
            // Create a valid JWT token for testing
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a_secure_key_that_is_at_least_16_bytes"));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "TestUser") }),
                Expires = DateTime.UtcNow.AddMinutes(30),
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        [Fact]
        public async Task HandleAuthenticateAsync_ReturnsNoResult_WhenTokenIsMissing()
        {
            // Arrange
            var options = new JwtBearerOptions
            {
                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a_secure_key_that_is_at_least_16_bytes"))
                },
                Events = new JwtBearerEvents() // Ensure events are initialized
            };

            var optionsMonitorMock = new Mock<IOptionsMonitor<JwtBearerOptions>>();
            optionsMonitorMock.Setup(o => o.Get(It.IsAny<string>())).Returns(options);

            var context = new DefaultHttpContext();

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

            var handler = new PulseJwtBearerHandler(
                optionsMonitorMock.Object,
                loggerFactory,
                Mock.Of<UrlEncoder>()
            );

            await handler.InitializeAsync(
                new AuthenticationScheme("Bearer", null, typeof(PulseJwtBearerHandler)),
                context
            );

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
            var options = new JwtBearerOptions
            {
                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a_secure_key_that_is_at_least_16_bytes"))
                },
                Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Simulate extracting the invalid token from the request
                        context.Token = "invalid_token";
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        // Log or modify failure context if necessary
                        return Task.CompletedTask;
                    }
                }
            };

            var optionsMonitorMock = new Mock<IOptionsMonitor<JwtBearerOptions>>();
            optionsMonitorMock.Setup(o => o.Get(It.IsAny<string>())).Returns(options);

            var context = new DefaultHttpContext();
            context.Request.Headers["Authorization"] = "Bearer invalid_token";

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

            var handler = new PulseJwtBearerHandler(
                optionsMonitorMock.Object,
                loggerFactory,
                Mock.Of<UrlEncoder>()
            );

            await handler.InitializeAsync(
                new AuthenticationScheme("Bearer", null, typeof(PulseJwtBearerHandler)),
                context
            );

            // Act
            var result = await handler.AuthenticateAsync();

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Succeeded);
        }

    }
}
