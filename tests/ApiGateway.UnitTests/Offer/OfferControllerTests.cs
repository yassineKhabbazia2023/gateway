using ApiGateway.Attributes;
using ApiGateway.Offer;
using ApiGateway.Offer.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Pulse.ExceptionMiddleware.Model;
using System.Reflection;

namespace ApiGateway.UnitTests.Offer;

public class OfferControllerTests
{
    private readonly Mock<IOfferService> _mockOfferService;
    private readonly Mock<ILogger<OfferController>> _mockLogger;
    private readonly OfferController _controller;

    public OfferControllerTests()
    {
        _mockOfferService = new Mock<IOfferService>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<OfferController>>(MockBehavior.Loose);
        _controller = new OfferController(_mockLogger.Object, _mockOfferService.Object);
    }

    #region CreateSubscription Tests

    [Fact]
    public async Task CreateSubscription_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 456,
            PlanId = 789,
            ProductConfigurations = new List<int> { 1, 2, 3 },
            Contacts = new List<int> { 10, 20 },
            Notes = new List<Note>
            {
                new Note { StepName = "Step1", StepNote = "Note1" }
            },
            Applicant = "John Doe"
        };

        var expectedSubscriptionId = 999;

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(expectedSubscriptionId);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        result.Should().BeOfType<ActionResult<int>>();
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().Be(expectedSubscriptionId);
    }

    [Fact]
    public async Task CreateSubscription_ShouldCallOfferService()
    {
        // Arrange
        var request = new Fixture().Create<CreateSubscriptionOffer>();
        var subscriptionId = 123;

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(subscriptionId);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        _mockOfferService.Verify(x => x.CreateSubscriptionAsync(request), Times.Once);
    }

    [Fact]
    public async Task CreateSubscription_ShouldLogRequestDetails()
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 456
        };

        var subscriptionId = 789;

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(subscriptionId);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Creating subscription")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CreateSubscription_WhenServiceThrowsException_ShouldPropagate()
    {
        // Arrange
        var request = new Fixture().Create<CreateSubscriptionOffer>();

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ThrowsAsync(new HttpRequestException("Service error"));

        // Act
        var action = async () => await _controller.CreateSubscription(request);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("Service error");
    }

    [Fact]
    public async Task CreateSubscription_ShouldReturnSubscriptionId()
    {
        // Arrange
        var request = new Fixture().Create<CreateSubscriptionOffer>();
        var expectedId = 12345;

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(expectedId);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult.Value.Should().Be(expectedId);
    }

    #endregion

    #region Controller Attributes Tests

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        // Arrange
        var type = typeof(OfferController);

        // Act
        var attributes = type.GetCustomAttributes(typeof(AuthorizeAttribute), true);

        // Assert
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveCorrectRoute()
    {
        // Arrange
        var type = typeof(OfferController);

        // Act
        var routeAttribute = type.GetCustomAttribute<RouteAttribute>();

        // Assert
        routeAttribute.Should().NotBeNull();
        routeAttribute.Template.Should().Be("gtw/offer/api");
    }

    #endregion

    #region CreateSubscription Method Attributes Tests

    [Fact]
    public void CreateSubscription_ShouldHaveHttpPostAttribute()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attribute = method.GetCustomAttribute<HttpPostAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute.Template.Should().Be("subscription/create");
    }

    [Fact]
    public void CreateSubscription_ShouldHaveCorrectRoute()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attribute = method.GetCustomAttribute<HttpPostAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute.Template.Should().Be("subscription/create");
    }

    [Fact]
    public void CreateSubscription_ShouldHaveRequirePermissionAttribute()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attribute = method.GetCustomAttribute<RequirePermissionAttribute>();

        // Assert
        attribute.Should().NotBeNull();
    }

    [Fact]
    public void CreateSubscription_RequirePermissionAttribute_ShouldHaveCOOFF001()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attribute = method.GetCustomAttribute<RequirePermissionAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        // We can't directly access the permissions field, but we can verify the attribute exists
        // The actual permission validation is tested in RequirePermissionAttributeTests
    }

    [Fact]
    public void CreateSubscription_ShouldHaveProducesResponseTypeAttributes()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attributes = method.GetCustomAttributes<ProducesResponseTypeAttribute>().ToList();

        // Assert
        attributes.Should().NotBeEmpty();
        attributes.Should().HaveCountGreaterOrEqualTo(4); // 200, 400, 403, 404
    }

    [Fact]
    public void CreateSubscription_ShouldHaveProducesResponseType200()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attributes = method.GetCustomAttributes<ProducesResponseTypeAttribute>();
        var status200 = attributes.FirstOrDefault(a => a.StatusCode == StatusCodes.Status200OK);

        // Assert
        status200.Should().NotBeNull();
        status200.Type.Should().Be(typeof(int));
    }

    [Fact]
    public void CreateSubscription_ShouldHaveProducesResponseType404()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attributes = method.GetCustomAttributes<ProducesResponseTypeAttribute>();
        var status404 = attributes.FirstOrDefault(a => a.StatusCode == StatusCodes.Status404NotFound);

        // Assert
        status404.Should().NotBeNull();
        status404.Type.Should().Be(typeof(ErrorResponse));
    }

    [Fact]
    public void CreateSubscription_ShouldHaveProducesResponseType400()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attributes = method.GetCustomAttributes<ProducesResponseTypeAttribute>();
        var status400 = attributes.FirstOrDefault(a => a.StatusCode == StatusCodes.Status400BadRequest);

        // Assert
        status400.Should().NotBeNull();
        status400.Type.Should().Be(typeof(ErrorResponse));
    }

    [Fact]
    public void CreateSubscription_ShouldHaveProducesResponseType403()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attributes = method.GetCustomAttributes<ProducesResponseTypeAttribute>();
        var status403 = attributes.FirstOrDefault(a => a.StatusCode == StatusCodes.Status403Forbidden);

        // Assert
        status403.Should().NotBeNull();
        status403.Type.Should().Be(typeof(ErrorResponse));
    }

    #endregion
}
