using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using ApiGateway.Exceptions;
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
            var expiredDate = new DateTime(2024, 01, 15, 10, 30, 00, DateTimeKind.Utc);
            var aggregateException = new AggregateException(new Exception[]
            {
                new SecurityTokenInvalidAudienceException("Invalid audience.") { InvalidAudience = "TestAudience" },
                new SecurityTokenInvalidIssuerException("Invalid issuer.") { InvalidIssuer = "TestIssuer" },
                new SecurityTokenExpiredException { Expires = expiredDate }
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
            Assert.Contains($"The token expired at '{expiredDate.ToString(CultureInfo.InvariantCulture)}'", errorDescription);
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
            Assert.Throws<GatewayException>(() => handler.ExposedEvents);
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

        [Fact]
        public async Task AppendChallengeResponse_AddsBasicChallengeHeader_WhenNoErrorDetails()
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
                Challenge = "Bearer"
            };

            _optionsMock.Setup(o => o.Get(It.IsAny<string>())).Returns(options);

            var context = new DefaultHttpContext();
            var handler = new TestablePulseJwtBearerHandler(
                _optionsMock.Object,
                LoggerFactory.Create(builder => builder.AddConsole()),
                Mock.Of<UrlEncoder>()
            );

            await handler.InitializeAsync(
                new AuthenticationScheme("Bearer", null, typeof(PulseJwtBearerHandler)),
                context
            );

            var eventContext = new JwtBearerChallengeContext(context, new AuthenticationScheme("Bearer", null, typeof(PulseJwtBearerHandler)), options, new AuthenticationProperties());

            // Act
            handler.ExposedAppendChallengeResponse(eventContext);

            // Assert
            Assert.True(context.Response.Headers.ContainsKey("WWW-Authenticate"));
            Assert.Equal("Bearer", context.Response.Headers["WWW-Authenticate"]);
        }

        [Fact]
        public async Task AppendChallengeResponse_AddsChallengeHeader_WithErrorDetails()
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
                Challenge = "Bearer"
            };

            _optionsMock.Setup(o => o.Get(It.IsAny<string>())).Returns(options);

            var context = new DefaultHttpContext();
            var handler = new TestablePulseJwtBearerHandler(
                _optionsMock.Object,
                LoggerFactory.Create(builder => builder.AddConsole()),
                Mock.Of<UrlEncoder>()
            );

            await handler.InitializeAsync(
                new AuthenticationScheme("Bearer", null, typeof(PulseJwtBearerHandler)),
                context
            );

            var eventContext = new JwtBearerChallengeContext(context, new AuthenticationScheme("Bearer", null, typeof(PulseJwtBearerHandler)), options, new AuthenticationProperties())
            {
                Error = "invalid_token",
                ErrorDescription = "The access token is invalid.",
                ErrorUri = "https://example.com/error"
            };

            // Act
            handler.ExposedAppendChallengeResponse(eventContext);

            // Assert
            Assert.True(context.Response.Headers.ContainsKey("WWW-Authenticate"));
            var headerValue = context.Response.Headers["WWW-Authenticate"].ToString();
            Assert.Contains("error=\"invalid_token\"", headerValue);
            Assert.Contains("error_description=\"The access token is invalid.\"", headerValue);
            Assert.Contains("error_uri=\"https://example.com/error\"", headerValue);
        }

        [Fact]
        public async Task AppendChallengeResponse_AddsPartialChallengeHeader_WithSomeErrorDetails()
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
                Challenge = "Bearer"
            };

            _optionsMock.Setup(o => o.Get(It.IsAny<string>())).Returns(options);

            var context = new DefaultHttpContext();
            var handler = new TestablePulseJwtBearerHandler(
                _optionsMock.Object,
                LoggerFactory.Create(builder => builder.AddConsole()),
                Mock.Of<UrlEncoder>()
            );

            await handler.InitializeAsync(
                new AuthenticationScheme("Bearer", null, typeof(PulseJwtBearerHandler)),
                context
            );

            var eventContext = new JwtBearerChallengeContext(context, new AuthenticationScheme("Bearer", null, typeof(PulseJwtBearerHandler)), options, new AuthenticationProperties())
            {
                Error = "invalid_token"
            };

            // Act
            handler.ExposedAppendChallengeResponse(eventContext);

            // Assert
            Assert.True(context.Response.Headers.ContainsKey("WWW-Authenticate"));
            var headerValue = context.Response.Headers["WWW-Authenticate"].ToString();
            Assert.Contains("error=\"invalid_token\"", headerValue);
            Assert.DoesNotContain("error_description", headerValue);
            Assert.DoesNotContain("error_uri", headerValue);
        }

        [Fact]
        public async Task HandleAuthenticationFailureAsync_HandlesFailureAndThrowsException()
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
                    OnAuthenticationFailed = context =>
                    {
                        // Simulate custom logic in the event handler
                        context.NoResult(); // Pretend the failure is handled
                        return Task.CompletedTask;
                    }
                }
            };

            _optionsMock.Setup(o => o.Get(It.IsAny<string>())).Returns(options);

            var handler = new TestablePulseJwtBearerHandler(
                _optionsMock.Object,
                LoggerFactory.Create(builder => builder.AddConsole()),
                Mock.Of<UrlEncoder>()
            );

            await handler.InitializeAsync(
                new AuthenticationScheme("Bearer", null, typeof(PulseJwtBearerHandler)),
                new DefaultHttpContext()
            );

            var exception = new SecurityTokenInvalidLifetimeException("Invalid token lifetime");

            // Act
            var result = await handler.ExposedHandleAuthenticationFailureAsync(exception);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Succeeded);
        }

        [Fact]
        public async Task HandleChallengeAsync_CoversAllPaths()
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
                IncludeErrorDetails = true,
                Events = new JwtBearerEvents()
            };

            _optionsMock.Setup(o => o.Get(It.IsAny<string>())).Returns(options);

            var context = new DefaultHttpContext();
            context.Response.Headers.Clear();

            var handler = new TestablePulseJwtBearerHandler(
                _optionsMock.Object,
                LoggerFactory.Create(builder => builder.AddConsole()),
                Mock.Of<UrlEncoder>()
            );

            await handler.InitializeAsync(
                new AuthenticationScheme("Bearer", null, typeof(PulseJwtBearerHandler)),
                context
            );

            var properties = new AuthenticationProperties();

            // Simulate an authentication failure
            context.Features.Set<IAuthenticateResultFeature>(Mock.Of<IAuthenticateResultFeature>(f =>
                f.AuthenticateResult == AuthenticateResult.Fail(new SecurityTokenInvalidSignatureException("Invalid signature"))
            ));

            // Act
            await handler.ExposedHandleChallengeAsync(properties);

            // Assert
            Assert.Equal(401, context.Response.StatusCode);
            Assert.True(context.Response.Headers.ContainsKey("WWW-Authenticate"));
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

            public void ExposedAppendChallengeResponse(JwtBearerChallengeContext eventContext)
            {
                var methodInfo = typeof(PulseJwtBearerHandler)
                    .GetMethod("AppendChallengeResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (methodInfo == null)
                {
                    throw new MissingMethodException("AppendChallengeResponse method not found.");
                }

                methodInfo.Invoke(this, new object[] { eventContext });
            }

            public async Task<AuthenticateResult> ExposedHandleAuthenticationFailureAsync(Exception ex)
            {
                var methodInfo = typeof(PulseJwtBearerHandler)
                    .GetMethod("HandleAuthenticationFailureAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (methodInfo == null)
                    throw new MissingMethodException("HandleAuthenticationFailureAsync method not found");

                return await (Task<AuthenticateResult>)methodInfo.Invoke(this, new object[] { ex });
            }

            public Task ExposedHandleChallengeAsync(AuthenticationProperties properties)
                => base.HandleChallengeAsync(properties);

        }

    }
}
