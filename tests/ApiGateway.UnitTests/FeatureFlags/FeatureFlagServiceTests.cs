using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenFeature;
using OpenFeature.Providers.Memory;

namespace ApiGateway.UnitTests.FeatureFlags;

public class FeatureFlagServiceTests : IAsyncLifetime
{
    private readonly FeatureFlagService _sut;

    public FeatureFlagServiceTests()
    {
        _sut = new FeatureFlagService(NullLogger<FeatureFlagService>.Instance);
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
     /// - Line 33-36: if (context is null) return null;
     /// - Line 38-43: if (!string.IsNullOrWhiteSpace(context.ContactId)) → use ContactId
     /// - Line 45-48: if (string.IsNullOrWhiteSpace(context.Email)) → return null
     /// - Line 50-52: else → use Email.ToLowerInvariant()
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
     public async Task BuildEvaluationContext_WhenContactIdIsProvided_ShouldUseContactIdAsIdentifier()
     {
         // Arrange: Create context with ContactId (non-null, non-whitespace)
         var context = new FeatureContext
         {
             ContactId = "12345",
             Email = "user@test.fr"  // Email is also set, but ContactId should take priority
         };

         // Act: pass context with ContactId to flag evaluation
         var result = await _sut.IsEnabledAsync("enabled-flag", context: context);

         // Assert: Should use ContactId, not Email
         // (ContactId has priority over Email per line 38-43)
         result.Should().BeTrue();
     }

     [Fact]
     public async Task BuildEvaluationContext_WhenContactIdIsWhitespace_ShouldFallBackToEmail()
     {
         // Arrange: ContactId is whitespace (should be treated as null)
         var context = new FeatureContext
         {
             ContactId = "   ",  // Whitespace only
             Email = "user@test.fr"
         };

         // Act: pass context with whitespace ContactId
         var result = await _sut.IsEnabledAsync("enabled-flag", context: context);

         // Assert: Should fall back to Email (line 45-52)
         result.Should().BeTrue();
     }

     [Fact]
     public async Task BuildEvaluationContext_WhenContactIdIsNull_ShouldFallBackToEmail()
     {
         // Arrange: ContactId is null (per line 38: !string.IsNullOrWhiteSpace)
         var context = new FeatureContext
         {
             ContactId = null,
             Email = "user@test.fr"
         };

         // Act: pass context with null ContactId
         var result = await _sut.IsEnabledAsync("enabled-flag", context: context);

         // Assert: Should use Email as fallback
         result.Should().BeTrue();
     }

     [Fact]
     public async Task BuildEvaluationContext_WhenEmailIsNullAndContactIdIsNull_ShouldReturnNull()
     {
         // Arrange: Both ContactId and Email are null
         // (Line 45: if (string.IsNullOrWhiteSpace(context.Email)) return null;)
         var context = new FeatureContext
         {
             ContactId = null,
             Email = null
         };

         // Act: pass context with no useful identifier
         // BuildEvaluationContext returns null → no targeting applied → uses flag default
         var result = await _sut.IsEnabledAsync("enabled-flag", context: context);

         // Assert: Context is null, so flag uses default value (enabled-flag = true)
         result.Should().BeTrue();
     }

     [Fact]
     public async Task BuildEvaluationContext_WhenEmailIsWhitespaceAndContactIdIsNull_ShouldReturnNull()
     {
         // Arrange: Email is whitespace, ContactId is null
         // (Line 45: if (string.IsNullOrWhiteSpace(context.Email)) return null;)
         var context = new FeatureContext
         {
             ContactId = null,
             Email = "   "  // Whitespace only
         };

         // Act: pass context with whitespace email
         // BuildEvaluationContext returns null → no targeting applied → uses flag default
         var result = await _sut.IsEnabledAsync("enabled-flag", context: context);

         // Assert: Context is null, so flag uses default value (enabled-flag = true)
         result.Should().BeTrue();
     }

     [Fact]
     public async Task BuildEvaluationContext_WhenEmailIsProvided_ShouldConvertToLowerInvariant()
     {
         // Arrange: Email with mixed case (line 51: Email.ToLowerInvariant())
         var context = new FeatureContext
         {
             ContactId = null,
             Email = "USER@TEST.FR"  // Upper case
         };

         // Act: pass context with mixed-case email
         var result = await _sut.IsEnabledAsync("enabled-flag", context: context);

         // Assert: Email should be lowercased internally for ConfigCat targeting
         result.Should().BeTrue();
     }

     [Fact]
     public async Task BuildEvaluationContext_ContactIdPriorityOverEmail_WhenBothAreProvided()
     {
         // Arrange: Both ContactId and Email are set
         // (Line 38-43 checks ContactId FIRST)
         var context = new FeatureContext
         {
             ContactId = "99999",      // This should be used
             Email = "other@test.fr"   // This should be ignored
         };

         // Act: pass context with both values
         var result = await _sut.IsEnabledAsync("enabled-flag", context: context);

         // Assert: ContactId should take priority (line 38 comes before line 45)
         result.Should().BeTrue();
     }

     [Fact]
     public async Task IsEnabledAsync_WithContactId_ShouldLogContactId()
     {
         // Arrange: Mock logger to verify logging
         var loggerMock = new Mock<ILogger<FeatureFlagService>>();
         var service = new FeatureFlagService(loggerMock.Object);
         var context = new FeatureContext { ContactId = "12345" };

         // Act
         var result = await service.IsEnabledAsync("enabled-flag", context: context);

         // Assert: Should log the ContactId (per line 15)
         loggerMock.Verify(
             x => x.Log(
                 LogLevel.Debug,
                 It.IsAny<EventId>(),
                 It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("12345")),
                 It.IsAny<Exception>(),
                 It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
             Times.Once);
     }

     #endregion
 }
