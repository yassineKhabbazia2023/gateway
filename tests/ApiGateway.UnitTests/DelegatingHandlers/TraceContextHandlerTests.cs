using System.Diagnostics;
using System.Net;
using ApiGateway.DelegatingHandlers;
using FluentAssertions;
using Xunit;

namespace ApiGateway.UnitTests.DelegatingHandlers;

public class TraceContextHandlerTests
{
    [Fact]
    public async Task SendAsync_WithActiveActivity_ShouldAddTraceparentHeader()
    {
        // Arrange
        using var activitySource = new ActivitySource("Test.Gateway");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = activitySource.StartActivity("TestRequest");
        activity.Should().NotBeNull();

        var innerHandler = new FakeInnerHandler();
        var handler = new TraceContextHandler { InnerHandler = innerHandler };
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        request.Headers.Contains("traceparent").Should().BeTrue();
        request.Headers.GetValues("traceparent").First().Should().Be(activity!.Id);
    }

    [Fact]
    public async Task SendAsync_WithTraceState_ShouldAddTracestateHeader()
    {
        // Arrange
        using var activitySource = new ActivitySource("Test.Gateway");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = activitySource.StartActivity("TestRequest");
        activity.Should().NotBeNull();
        activity!.TraceStateString = "vendor=value";

        var innerHandler = new FakeInnerHandler();
        var handler = new TraceContextHandler { InnerHandler = innerHandler };
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        request.Headers.Contains("tracestate").Should().BeTrue();
        request.Headers.GetValues("tracestate").First().Should().Be("vendor=value");
    }

    [Fact]
    public async Task SendAsync_WithNoActivity_ShouldNotAddHeaders()
    {
        // Arrange
        Activity.Current = null;

        var innerHandler = new FakeInnerHandler();
        var handler = new TraceContextHandler { InnerHandler = innerHandler };
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        request.Headers.Contains("traceparent").Should().BeFalse();
        request.Headers.Contains("tracestate").Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WithExistingTraceparent_ShouldNotOverwrite()
    {
        // Arrange
        using var activitySource = new ActivitySource("Test.Gateway");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = activitySource.StartActivity("TestRequest");

        var innerHandler = new FakeInnerHandler();
        var handler = new TraceContextHandler { InnerHandler = innerHandler };
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");
        var existingTraceparent = "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01";
        request.Headers.TryAddWithoutValidation("traceparent", existingTraceparent);

        // Act
        await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        request.Headers.GetValues("traceparent").First().Should().Be(existingTraceparent);
    }

    private class FakeInnerHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
