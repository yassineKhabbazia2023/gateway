using ApiGateway.FeatureFlags;
using OpenFeature;
using OpenFeature.Model;

namespace ApiGateway.UnitTests.FeatureFlags;

public class FeatureFlagServiceTests
{
    private readonly Mock<IFeatureClient> _featureClientMock = new();
    private readonly FeatureFlagService _sut;

    public FeatureFlagServiceTests()
    {
        _sut = new FeatureFlagService(_featureClientMock.Object);
    }

    [Fact]
    public async Task IsEnabledAsync_WhenFlagIsEnabled_ShouldReturnTrue()
    {
        _featureClientMock
            .Setup(x => x.GetBooleanValueAsync(
                "my-flag",
                false,
                It.IsAny<EvaluationContext?>(),
                It.IsAny<FlagEvaluationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.IsEnabledAsync("my-flag");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_WhenFlagIsDisabled_ShouldReturnFalse()
    {
        _featureClientMock
            .Setup(x => x.GetBooleanValueAsync(
                "my-flag",
                false,
                It.IsAny<EvaluationContext?>(),
                It.IsAny<FlagEvaluationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.IsEnabledAsync("my-flag");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_WhenFlagDoesNotExist_ShouldReturnDefaultFalse()
    {
        _featureClientMock
            .Setup(x => x.GetBooleanValueAsync(
                "nonexistent-flag",
                false,
                It.IsAny<EvaluationContext?>(),
                It.IsAny<FlagEvaluationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.IsEnabledAsync("nonexistent-flag");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_WithUserEmail_ShouldPassEvaluationContextWithHashedTargetingKey()
    {
        const string email = "user@example.com";
        var expectedHash = FeatureFlagService.HashEmail(email);
        EvaluationContext? capturedContext = null;

        _featureClientMock
            .Setup(x => x.GetBooleanValueAsync(
                "my-flag",
                false,
                It.IsAny<EvaluationContext?>(),
                It.IsAny<FlagEvaluationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, bool, EvaluationContext?, FlagEvaluationOptions?, CancellationToken>(
                (_, _, ctx, _, _) => capturedContext = ctx)
            .ReturnsAsync(true);

        await _sut.IsEnabledAsync("my-flag", email);

        capturedContext.Should().NotBeNull();
        capturedContext!.TargetingKey.Should().Be(expectedHash);
        capturedContext.Invoking(c => c.GetValue("email")).Should().Throw<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("User@Example.com")]
    [InlineData("USER@EXAMPLE.COM")]
    [InlineData("user@example.com")]
    public void HashEmail_ShouldProduceSameHash_RegardlessOfCase(string email)
    {
        var expected = FeatureFlagService.HashEmail("user@example.com");

        var result = FeatureFlagService.HashEmail(email);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task IsEnabledAsync_WithNullEmail_ShouldPassNullContext()
    {
        EvaluationContext? capturedContext = EvaluationContext.Builder().Build();

        _featureClientMock
            .Setup(x => x.GetBooleanValueAsync(
                "my-flag",
                false,
                It.IsAny<EvaluationContext?>(),
                It.IsAny<FlagEvaluationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, bool, EvaluationContext?, FlagEvaluationOptions?, CancellationToken>(
                (_, _, ctx, _, _) => capturedContext = ctx)
            .ReturnsAsync(true);

        await _sut.IsEnabledAsync("my-flag", null);

        capturedContext.Should().BeNull();
    }

    [Fact]
    public async Task IsEnabledAsync_WithEmptyEmail_ShouldPassNullContext()
    {
        EvaluationContext? capturedContext = EvaluationContext.Builder().Build();

        _featureClientMock
            .Setup(x => x.GetBooleanValueAsync(
                "my-flag",
                false,
                It.IsAny<EvaluationContext?>(),
                It.IsAny<FlagEvaluationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, bool, EvaluationContext?, FlagEvaluationOptions?, CancellationToken>(
                (_, _, ctx, _, _) => capturedContext = ctx)
            .ReturnsAsync(true);

        await _sut.IsEnabledAsync("my-flag", "");

        capturedContext.Should().BeNull();
    }
}
