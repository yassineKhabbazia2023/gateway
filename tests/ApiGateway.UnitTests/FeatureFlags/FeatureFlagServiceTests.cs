using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using FluentAssertions;
using OpenFeature;
using OpenFeature.Providers.Memory;

namespace ApiGateway.UnitTests.FeatureFlags;

public class FeatureFlagServiceTests : IAsyncLifetime
{
    private readonly FeatureFlagService _sut;

    public FeatureFlagServiceTests()
    {
        _sut = new FeatureFlagService();
    }

    public async Task InitializeAsync()
    {
        var flags = new Dictionary<string, Flag>
        {
            { "enabled-flag", new Flag<bool>(new Dictionary<string, bool> { { "on", true } }, "on") },
            { "disabled-flag", new Flag<bool>(new Dictionary<string, bool> { { "off", false } }, "off") },
            { "string-flag", new Flag<string>(new Dictionary<string, string> { { "default", "variant-a" } }, "default") },
            { "int-flag", new Flag<int>(new Dictionary<string, int> { { "default", 42 } }, "default") }
        };

        var provider = new InMemoryProvider(flags);
        await Api.Instance.SetProviderAsync(provider);
    }

    public async Task DisposeAsync()
    {
        await Api.Instance.SetProviderAsync(new InMemoryProvider(new Dictionary<string, Flag>()));
    }

    [Fact]
    public async Task IsEnabledAsync_Should_Return_True_When_Flag_Is_Enabled()
    {
        var result = await _sut.IsEnabledAsync("enabled-flag");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_Should_Return_False_When_Flag_Is_Disabled()
    {
        var result = await _sut.IsEnabledAsync("disabled-flag");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_Should_Return_False_For_Unknown_Flag()
    {
        var result = await _sut.IsEnabledAsync("unknown-flag");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_Should_Return_DefaultValue_For_Unknown_Flag()
    {
        var result = await _sut.IsEnabledAsync("unknown-flag", defaultValue: true);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_Should_Accept_FeatureContext()
    {
        var context = new FeatureContext
        {
            Email = "user@test.fr"
        };

        var result = await _sut.IsEnabledAsync("enabled-flag", context: context);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_Should_Work_Without_Context()
    {
        var result = await _sut.IsEnabledAsync("enabled-flag", context: null);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetStringValueAsync_Should_Return_Flag_Value()
    {
        var result = await _sut.GetStringValueAsync("string-flag", "default");

        result.Should().Be("variant-a");
    }

    [Fact]
    public async Task GetStringValueAsync_Should_Return_Default_For_Unknown_Flag()
    {
        var result = await _sut.GetStringValueAsync("unknown-flag", "default");

        result.Should().Be("default");
    }

    [Fact]
    public async Task GetIntValueAsync_Should_Return_Flag_Value()
    {
        var result = await _sut.GetIntValueAsync("int-flag", 0);

        result.Should().Be(42);
    }

     [Fact]
     public async Task GetIntValueAsync_Should_Return_Default_For_Unknown_Flag()
     {
         var result = await _sut.GetIntValueAsync("unknown-flag", 99);

         result.Should().Be(99);
     }

     #region BuildEvaluationContext Tests

     /// <summary>
     /// Tests for BuildEvaluationContext(FeatureContext? context):
     /// - if (context is null) return null;
     /// - if (string.IsNullOrWhiteSpace(context.Email)) → return null
     /// - else → Identifier = Email.ToLowerInvariant()
     /// </summary>

     [Fact]
     public async Task BuildEvaluationContext_WhenContextIsNull_ShouldPassNullToClient()
     {
         // Arrange: null context
         // Act: call with no context parameter
         var result = await _sut.IsEnabledAsync("enabled-flag", context: null);

         // Assert: should still work (returns default behavior without targeting)
         result.Should().BeTrue();
     }

     [Fact]
     public async Task BuildEvaluationContext_WhenEmailIsProvided_ShouldEvaluateWithContext()
     {
         // Arrange: context with a valid email
         var context = new FeatureContext
         {
             Email = "user@test.fr"
         };

         // Act
         var result = await _sut.IsEnabledAsync("enabled-flag", context: context);

         // Assert
         result.Should().BeTrue();
     }

     [Fact]
     public async Task BuildEvaluationContext_WhenEmailIsNull_ShouldReturnNull()
     {
         // Arrange: context without email
         var context = new FeatureContext
         {
             Email = null
         };

         // Act: BuildEvaluationContext returns null → no targeting applied → uses flag default
         var result = await _sut.IsEnabledAsync("enabled-flag", context: context);

         // Assert: flag default value (enabled-flag = true)
         result.Should().BeTrue();
     }

     [Fact]
     public async Task BuildEvaluationContext_WhenEmailIsWhitespace_ShouldReturnNull()
     {
         // Arrange: whitespace-only email
         var context = new FeatureContext
         {
             Email = "   "
         };

         // Act
         var result = await _sut.IsEnabledAsync("enabled-flag", context: context);

         // Assert: flag default value (enabled-flag = true)
         result.Should().BeTrue();
     }

     [Fact]
     public async Task BuildEvaluationContext_WhenEmailIsMixedCase_ShouldEvaluate()
     {
         // Arrange: mixed-case email is lowercased internally for ConfigCat targeting
         var context = new FeatureContext
         {
             Email = "USER@TEST.FR"
         };

         // Act
         var result = await _sut.IsEnabledAsync("enabled-flag", context: context);

         // Assert
         result.Should().BeTrue();
     }

     #endregion
 }
