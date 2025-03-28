using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text;
using ApiGateway.Exceptions;
using IdentityModel;

namespace ApiGateway.Identity.Handlers
{
    public class PulseJwtBearerHandler : AuthenticationHandler<JwtBearerOptions>
    {
        private OpenIdConnectConfiguration? configuration;

        public PulseJwtBearerHandler(IOptionsMonitor<JwtBearerOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected new JwtBearerEvents Events
        {
            get => base.Events as JwtBearerEvents ?? throw(new GatewayException(StatusCodes.Status400BadRequest, Errors.NullArgumentCode, string.Format(Errors.NullArgumentMessage, nameof(JwtBearerEvents))));
            set => base.Events = value;
        }

        protected override Task<object> CreateEventsAsync() => Task.FromResult<object>(new JwtBearerEvents());

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return AuthenticateResult.NoResult();
                }

                await EnsureConfigurationAsync();

                if (!IsValidToken(token, out var validationParameters))
                {
                    return AuthenticateResult.NoResult();
                }

                return await ValidateTokenAsync(token, validationParameters);
            }
            catch (Exception ex)
            {
                return await HandleAuthenticationFailureAsync(ex);
            }
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            var authResult = await HandleAuthenticateOnceSafeAsync();
            var eventContext = new JwtBearerChallengeContext(Context, Scheme, Options, properties)
            {
                AuthenticateFailure = authResult?.Failure
            };

            if (Options.IncludeErrorDetails && eventContext.AuthenticateFailure != null)
            {
                eventContext.Error = "invalid_token";
                eventContext.ErrorDescription = CreateErrorDescription(eventContext.AuthenticateFailure);
            }

            await Events.Challenge(eventContext);
            if (eventContext.Handled)
            {
                return;
            }

            Response.StatusCode = 401;
            AppendChallengeResponse(eventContext);
        }

        private async Task<string?> GetTokenAsync()
        {
            var messageReceivedContext = new MessageReceivedContext(Context, Scheme, Options);
            await Events.MessageReceived(messageReceivedContext);
            if (messageReceivedContext.Result != null)
            {
                return messageReceivedContext.Token;
            }

            string authorization = Request.Headers[HeaderNames.Authorization];
            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return authorization.Substring("Bearer ".Length).Trim();
        }

        private async Task EnsureConfigurationAsync()
        {
            if (configuration == null && Options.ConfigurationManager != null)
            {
                configuration = await Options.ConfigurationManager.GetConfigurationAsync(Context.RequestAborted);
            }
        }

        private bool IsValidToken(string token, out TokenValidationParameters validationParameters)
        {
            validationParameters = Options.TokenValidationParameters.Clone();
            if (configuration != null)
            {
                validationParameters.ValidIssuers = validationParameters.ValidIssuers?.Concat(new[] { configuration.Issuer }) ?? new[] { configuration.Issuer };
                validationParameters.IssuerSigningKeys = validationParameters.IssuerSigningKeys?.Concat(configuration.SigningKeys) ?? configuration.SigningKeys;
            }

            try
            {
                var jwtHandler = new JwtSecurityTokenHandler();
                var jwtToken = jwtHandler.ReadJwtToken(token);
                return jwtToken.Issuer == Options.TokenValidationParameters.ValidIssuers?.FirstOrDefault();
            }
            catch
            {
                return false;
            }
        }

        private async Task<AuthenticateResult> ValidateTokenAsync(string token, TokenValidationParameters validationParameters)
        {
            var validationFailures = new List<Exception>();
            foreach (var validator in Options.SecurityTokenValidators)
            {
                if (validator.CanReadToken(token))
                {
                    try
                    {
                        var principal = validator.ValidateToken(token, validationParameters, out var validatedToken);
                        return await HandleTokenValidatedAsync(principal, validatedToken, token);
                    }
                    catch (Exception ex)
                    {
                        validationFailures.Add(ex);
                        HandleKeyRollover(ex);
                    }
                }
            }

            return await HandleValidationFailuresAsync(validationFailures);
        }

        private void HandleKeyRollover(Exception ex)
        {
            if (Options.RefreshOnIssuerKeyNotFound && Options.ConfigurationManager != null && ex is SecurityTokenSignatureKeyNotFoundException)
            {
                Options.ConfigurationManager.RequestRefresh();
            }
        }

        private async Task<AuthenticateResult> HandleTokenValidatedAsync(ClaimsPrincipal principal, SecurityToken validatedToken, string token)
        {
            var tokenValidatedContext = new TokenValidatedContext(Context, Scheme, Options)
            {
                Principal = principal,
                SecurityToken = validatedToken
            };

            await Events.TokenValidated(tokenValidatedContext);
            if (tokenValidatedContext.Result != null)
            {
                return tokenValidatedContext.Result;
            }

            if (Options.SaveToken)
            {
                tokenValidatedContext.Properties.StoreTokens(new[] { new AuthenticationToken { Name = "access_token", Value = token } });
            }

            tokenValidatedContext.Success();
            return tokenValidatedContext.Result;
        }

        private async Task<AuthenticateResult> HandleValidationFailuresAsync(List<Exception> validationFailures)
        {
            if (validationFailures.Count > 0)
            {
                var authenticationFailedContext = new AuthenticationFailedContext(Context, Scheme, Options)
                {
                    Exception = validationFailures.Count == 1 ? validationFailures[0] : new AggregateException(validationFailures)
                };

                await Events.AuthenticationFailed(authenticationFailedContext);
                return authenticationFailedContext.Result ?? AuthenticateResult.Fail(authenticationFailedContext.Exception);
            }

            return AuthenticateResult.Fail("No SecurityTokenValidator available for token.");
        }

        private async Task<AuthenticateResult> HandleAuthenticationFailureAsync(Exception ex)
        {
            var authenticationFailedContext = new AuthenticationFailedContext(Context, Scheme, Options)
            {
                Exception = ex
            };

            await Events.AuthenticationFailed(authenticationFailedContext);
            return authenticationFailedContext.Result ?? throw new GatewayException(StatusCodes.Status400BadRequest, Errors.NullArgumentCode, string.Format(Errors.NullArgumentMessage, nameof(authenticationFailedContext.Result)));
        }

        private void AppendChallengeResponse(JwtBearerChallengeContext eventContext)
        {
            if (string.IsNullOrEmpty(eventContext.Error) && string.IsNullOrEmpty(eventContext.ErrorDescription) && string.IsNullOrEmpty(eventContext.ErrorUri))
            {
                Response.Headers.Append(HeaderNames.WWWAuthenticate, Options.Challenge);
            }
            else
            {
                var builder = new StringBuilder(Options.Challenge);
                if (Options.Challenge.IndexOf(' ') > 0)
                {
                    builder.Append(',');
                }

                if (!string.IsNullOrEmpty(eventContext.Error))
                {
                    builder.Append($" error=\"{eventContext.Error}\"");
                }

                if (!string.IsNullOrEmpty(eventContext.ErrorDescription))
                {
                    if (!string.IsNullOrEmpty(eventContext.Error))
                    {
                        builder.Append(",");
                    }
                    builder.Append($" error_description=\"{eventContext.ErrorDescription}\"");
                }

                if (!string.IsNullOrEmpty(eventContext.ErrorUri))
                {
                    if (!string.IsNullOrEmpty(eventContext.Error) || !string.IsNullOrEmpty(eventContext.ErrorDescription))
                    {
                        builder.Append(",");
                    }
                    builder.Append($" error_uri=\"{eventContext.ErrorUri}\"");
                }

                Response.Headers.Append(HeaderNames.WWWAuthenticate, builder.ToString());
            }
        }

        private static string CreateErrorDescription(Exception authFailure)
        {
            IEnumerable<Exception> exceptions = authFailure is AggregateException agEx ? agEx.InnerExceptions : new[] { authFailure };
            var messages = exceptions.Select(ex => ex switch
            {
                SecurityTokenInvalidAudienceException stia => $"The audience '{stia.InvalidAudience ?? "(null)"}' is invalid",
                SecurityTokenInvalidIssuerException stii => $"The issuer '{stii.InvalidIssuer ?? "(null)"}' is invalid",
                SecurityTokenNoExpirationException => "The token has no expiration",
                SecurityTokenInvalidLifetimeException stil => $"The token lifetime is invalid; NotBefore: '{stil.NotBefore?.ToString(CultureInfo.InvariantCulture) ?? "(null)"}', Expires: '{stil.Expires?.ToString(CultureInfo.InvariantCulture) ?? "(null)"}'",
                SecurityTokenNotYetValidException stnyv => $"The token is not valid before '{stnyv.NotBefore.ToString(CultureInfo.InvariantCulture)}'",
                SecurityTokenExpiredException ste => $"The token expired at '{ste.Expires.ToString(CultureInfo.InvariantCulture)}'",
                SecurityTokenSignatureKeyNotFoundException => "The signature key was not found",
                SecurityTokenInvalidSignatureException => "The signature is invalid",
                _ => ex.Message
            });

            return string.Join("; ", messages);
        }
    }
}
