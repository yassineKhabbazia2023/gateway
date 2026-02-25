using System.Net;
using Polly;
using Polly.Extensions.Http;
using ApiGateway.Configuration;

namespace ApiGateway.UnitTests.Extensions;

/// <summary>
/// Tests for HTTP retry policy behavior.
/// Validates that the retry policy only retries transient errors (5xx, 408)
/// and NOT client errors like 404 NotFound or 400 BadRequest.
/// </summary>
public class RetryPolicyTests
{
    /// <summary>
    /// Creates the same retry policy used in ServiceExtensions.GetRetryPolicy()
    /// This mirrors the production code to ensure consistent behavior.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(ConfigConstants.HttpClientRetryAttempt,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    [Fact]
    public async Task RetryPolicy_ShouldNotRetry_WhenStatusCodeIs404NotFound()
    {
        // Arrange
        var retryCount = 0;
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(1),
                onRetry: (_, _, _, _) => retryCount++);

        // Act
        var result = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        retryCount.Should().Be(0, "404 NotFound should NOT trigger a retry");
    }

    [Fact]
    public async Task RetryPolicy_ShouldNotRetry_WhenStatusCodeIs400BadRequest()
    {
        // Arrange
        var retryCount = 0;
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(1),
                onRetry: (_, _, _, _) => retryCount++);

        // Act
        var result = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        retryCount.Should().Be(0, "400 BadRequest should NOT trigger a retry");
    }

    [Fact]
    public async Task RetryPolicy_ShouldNotRetry_WhenStatusCodeIs401Unauthorized()
    {
        // Arrange
        var retryCount = 0;
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(1),
                onRetry: (_, _, _, _) => retryCount++);

        // Act
        var result = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        retryCount.Should().Be(0, "401 Unauthorized should NOT trigger a retry");
    }

    [Fact]
    public async Task RetryPolicy_ShouldNotRetry_WhenStatusCodeIs403Forbidden()
    {
        // Arrange
        var retryCount = 0;
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(1),
                onRetry: (_, _, _, _) => retryCount++);

        // Act
        var result = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        retryCount.Should().Be(0, "403 Forbidden should NOT trigger a retry");
    }

    [Fact]
    public async Task RetryPolicy_ShouldRetry_WhenStatusCodeIs500InternalServerError()
    {
        // Arrange
        var retryCount = 0;
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(1),
                onRetry: (_, _, _, _) => retryCount++);

        // Act
        var result = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        retryCount.Should().Be(3, "500 InternalServerError SHOULD trigger retries");
    }

    [Fact]
    public async Task RetryPolicy_ShouldRetry_WhenStatusCodeIs502BadGateway()
    {
        // Arrange
        var retryCount = 0;
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(1),
                onRetry: (_, _, _, _) => retryCount++);

        // Act
        var result = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        retryCount.Should().Be(3, "502 BadGateway SHOULD trigger retries");
    }

    [Fact]
    public async Task RetryPolicy_ShouldRetry_WhenStatusCodeIs503ServiceUnavailable()
    {
        // Arrange
        var retryCount = 0;
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(1),
                onRetry: (_, _, _, _) => retryCount++);

        // Act
        var result = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        retryCount.Should().Be(3, "503 ServiceUnavailable SHOULD trigger retries");
    }

    [Fact]
    public async Task RetryPolicy_ShouldRetry_WhenStatusCodeIs408RequestTimeout()
    {
        // Arrange
        var retryCount = 0;
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(1),
                onRetry: (_, _, _, _) => retryCount++);

        // Act
        var result = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestTimeout)));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.RequestTimeout);
        retryCount.Should().Be(3, "408 RequestTimeout SHOULD trigger retries");
    }

    [Fact]
    public async Task RetryPolicy_ShouldNotRetry_WhenStatusCodeIs200OK()
    {
        // Arrange
        var retryCount = 0;
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(1),
                onRetry: (_, _, _, _) => retryCount++);

        // Act
        var result = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        retryCount.Should().Be(0, "200 OK should NOT trigger a retry");
    }

    [Fact]
    public async Task RetryPolicy_ShouldEventuallySucceed_WhenTransientErrorThenSuccess()
    {
        // Arrange
        var callCount = 0;
        var retryCount = 0;
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(1),
                onRetry: (_, _, _, _) => retryCount++);

        // Act - First 2 calls return 500, third call returns 200
        var result = await policy.ExecuteAsync(() =>
        {
            callCount++;
            return Task.FromResult(callCount < 3
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK));
        });

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        callCount.Should().Be(3, "Should have made 3 total calls");
        retryCount.Should().Be(2, "Should have retried 2 times before succeeding");
    }
}
