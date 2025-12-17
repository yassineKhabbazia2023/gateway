using ApiGateway.Authorization;
using ApiGateway.Authorization.Models;
using ApiGateway.Pennylane;
using ApiGateway.Pennylane.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.Authorization;

public class AuthorizationControllerTests
{
    private readonly Mock<IAuthorizationService> _authorizationServiceMock;
    private readonly Mock<IPennylaneService> _pennylaneServiceMock;
    private readonly Mock<ILogger<AuthorizationController>> _loggerMock;
    private readonly AuthorizationController _controller;

    public AuthorizationControllerTests()
    {
        _authorizationServiceMock = new Mock<IAuthorizationService>(MockBehavior.Strict);
        _pennylaneServiceMock = new Mock<IPennylaneService>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<AuthorizationController>>();

        _controller = new AuthorizationController(
            _authorizationServiceMock.Object,
            _pennylaneServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithPennylanePermissionAndSuccess_ShouldGrantPennylaneAndUpdateAuthorization()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);
        var pennylaneResult = new GrantPennylaneAccessResult
        {
            Status = PennylaneAccessStatuses.Created,
            Message = "created",
            ContactId = request.Authorization.ContactId,
            AccountId = request.Authorization.AccountId,
        };

        _authorizationServiceMock
            .Setup(x => x.GetContactAuthorizationAsync(
                request.Authorization.ContactId,
                request.Authorization.AccountId))
            .ReturnsAsync(new List<string>());

        _pennylaneServiceMock
            .Setup(x => x.GrantPennylaneAccessAsync(It.Is<PennylaneAuthorizationRequest>(r =>
                r.AccountId == request.Authorization.AccountId &&
                r.ContactId == request.Authorization.ContactId &&
                r.Role == request.Authorization.Role)))
            .ReturnsAsync(pennylaneResult);

        _authorizationServiceMock
            .Setup(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
                request.Authorization.ContactId,
                request.Authorization.AccountId,
                request.PermissionsCodes))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CreateAuthorizationsAsync(request);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();

        var payload = okResult!.Value as AuthorizationUpdateResponse;
        payload.Should().NotBeNull();
        payload!.ProvisioningResult.Should().NotBeNull();
        payload!.ProvisioningResult!.Status.Should().Be(pennylaneResult.Status);
        payload.AuthorizationUpdated.Should().BeTrue();
        payload.ProvisioningStepCompleted.Should().BeTrue();

        _pennylaneServiceMock.Verify(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()), Times.Once);
        _authorizationServiceMock.Verify(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
            request.Authorization.ContactId,
            request.Authorization.AccountId,
            request.PermissionsCodes), Times.Once);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithPennylanePermissionAndFailedStatus_ShouldReturnBadRequestAndSkipAuthorization()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);
        var pennylaneResult = new GrantPennylaneAccessResult
        {
            Status = PennylaneAccessStatuses.Failed,
            Message = "failed",
            ContactId = request.Authorization.ContactId,
            AccountId = request.Authorization.AccountId,
        };

        _authorizationServiceMock
            .Setup(x => x.GetContactAuthorizationAsync(
                request.Authorization.ContactId,
                request.Authorization.AccountId))
            .ReturnsAsync(new List<string>());

        _pennylaneServiceMock
            .Setup(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()))
            .ReturnsAsync(pennylaneResult);

        // Act
        var result = await _controller.CreateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        _pennylaneServiceMock.Verify(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()), Times.Once);
        _authorizationServiceMock.Verify(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<IList<string>>()), Times.Never);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithoutPennylanePermission_ShouldOnlyCallAuthorizationService()
    {
        // Arrange
        var request = BuildRequest(["OTHER_CODE"]);

        _authorizationServiceMock
            .Setup(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
                request.Authorization.ContactId,
                request.Authorization.AccountId,
                request.PermissionsCodes))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CreateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        _pennylaneServiceMock.Verify(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()), Times.Never);
        _authorizationServiceMock.Verify(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
            request.Authorization.ContactId,
            request.Authorization.AccountId,
            request.PermissionsCodes), Times.Once);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WhenAuthorizationUpdateFailsAfterPennylane_ShouldReturnBadRequest()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);
        var pennylaneResult = new GrantPennylaneAccessResult
        {
            Status = PennylaneAccessStatuses.Created,
            Message = "ok",
            ContactId = request.Authorization.ContactId,
            AccountId = request.Authorization.AccountId,
        };

        _authorizationServiceMock
            .Setup(x => x.GetContactAuthorizationAsync(
                request.Authorization.ContactId,
                request.Authorization.AccountId))
            .ReturnsAsync(new List<string>());

        _pennylaneServiceMock
            .Setup(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()))
            .ReturnsAsync(pennylaneResult);

        _authorizationServiceMock
            .Setup(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
                request.Authorization.ContactId,
                request.Authorization.AccountId,
                request.PermissionsCodes))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CreateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        _pennylaneServiceMock.Verify(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()), Times.Once);
        _authorizationServiceMock.Verify(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
            request.Authorization.ContactId,
            request.Authorization.AccountId,
            request.PermissionsCodes), Times.Once);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WhenAuthorizationUpdateFailsWithoutPennylane_ShouldReturnBadRequest()
    {
        // Arrange
        var request = BuildRequest(["OTHER_CODE"]);

        _authorizationServiceMock
            .Setup(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
                request.Authorization.ContactId,
                request.Authorization.AccountId,
                request.PermissionsCodes))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CreateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        _pennylaneServiceMock.Verify(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()), Times.Never);
        _authorizationServiceMock.Verify(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
            request.Authorization.ContactId,
            request.Authorization.AccountId,
            request.PermissionsCodes), Times.Once);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithInvalidRequest_ShouldReturnBadRequest()
    {
        // Arrange
        AuthorizationUpdateRequest request = new AuthorizationUpdateRequest
        {
            PermissionsCodes = null!,
            Authorization = null!
        };

        // Act
        var result = await _controller.CreateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithPennylanePermissionAndExistingAccess_ShouldSkipProvisioningAndReturnOk()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);

        _authorizationServiceMock
            .Setup(x => x.GetContactAuthorizationAsync(
                request.Authorization.ContactId,
                request.Authorization.AccountId))
            .ReturnsAsync(new List<string> { PermissionCodes.PennylaneAccess });

        // Act
        var result = await _controller.CreateAuthorizationsAsync(request);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();

        var payload = okResult!.Value as AuthorizationUpdateResponse;
        payload.Should().NotBeNull();
        payload!.ProvisioningResult.Should().NotBeNull();
        payload.ProvisioningResult!.Status.Should().Be(PennylaneAccessStatuses.AlreadyHasAccess);
        payload.AuthorizationUpdated.Should().BeTrue();
        payload.ProvisioningStepCompleted.Should().BeNull();

        _pennylaneServiceMock.Verify(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()), Times.Never);
        _authorizationServiceMock.Verify(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<IList<string>>()), Times.Never);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WhenPennylaneCallThrowsHttpRequestException_ShouldReturnBadGateway()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);

        _authorizationServiceMock
            .Setup(x => x.GetContactAuthorizationAsync(
                request.Authorization.ContactId,
                request.Authorization.AccountId))
            .ReturnsAsync(new List<string>());

        _pennylaneServiceMock
            .Setup(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()))
            .ThrowsAsync(new HttpRequestException("downstream"));

        // Act
        var result = await _controller.CreateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);

        _authorizationServiceMock.Verify(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<IList<string>>()), Times.Never);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WhenPennylaneCallThrowsInvalidOperationException_ShouldReturnInternalServerError()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);

        _authorizationServiceMock
            .Setup(x => x.GetContactAuthorizationAsync(
                request.Authorization.ContactId,
                request.Authorization.AccountId))
            .ReturnsAsync(new List<string>());

        _pennylaneServiceMock
            .Setup(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()))
            .ThrowsAsync(new InvalidOperationException("deserialize"));

        // Act
        var result = await _controller.CreateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        _authorizationServiceMock.Verify(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<IList<string>>()), Times.Never);
    }

    private static AuthorizationUpdateRequest BuildRequest(IList<string> permissions)
    {
        return new AuthorizationUpdateRequest
        {
            PermissionsCodes = permissions,
            Authorization = new AuthorizationTarget
            {
                AccountId = 123,
                ContactId = 456,
                Role = "role"
            }
        };
    }
}
