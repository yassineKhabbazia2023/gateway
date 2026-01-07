using System.Net;
using ApiGateway.Authorization;
using ApiGateway.Authorization.Consts;
using ApiGateway.Authorization.Models;
using ApiGateway.Authorization.Validators;
using ApiGateway.Exceptions;
using ApiGateway.Pennylane;
using ApiGateway.Pennylane.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.UnitTests.Authorization;

public class AuthorizationControllerTests
{
    private readonly Mock<IPennylaneAuthorizationService> _pennylaneAuthServiceMock;
    private readonly Mock<IAuthorizationWorkflowService> _authorizationWorkflowServiceMock;
    private readonly IAuthorizationRequestValidator _validator;
    private readonly Mock<ILogger<AuthorizationController>> _loggerMock;
    private readonly AuthorizationController _controller;

    public AuthorizationControllerTests()
    {
        _pennylaneAuthServiceMock = new Mock<IPennylaneAuthorizationService>(MockBehavior.Strict);
        _authorizationWorkflowServiceMock = new Mock<IAuthorizationWorkflowService>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<AuthorizationController>>();

        var validatorLogger = new Mock<ILogger<AuthorizationRequestValidator>>();
        _validator = new AuthorizationRequestValidator(validatorLogger.Object);

        _controller = new AuthorizationController(
            _validator,
            _pennylaneAuthServiceMock.Object,
            _authorizationWorkflowServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithPennylanePermissionAndSuccess_ShouldGrantPennylaneAndUpdateAuthorization()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(false);

        _pennylaneAuthServiceMock
            .Setup(x => x.TryHandlePennylaneProvisioningAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .Callback<AuthorizationUpdateRequest, AuthorizationUpdateResponse>((_, response) =>
            {
                response.ProvisioningResult = new AccessProvisioningResult { Status = PennylaneAccessStatuses.Created, Message = "ok" };
                response.ProvisioningStepCompleted = true;
            })
            .ReturnsAsync(OperationResult.Ok());

        _authorizationWorkflowServiceMock
            .Setup(x => x.TryUpdateAuthorizationsAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .Callback<AuthorizationUpdateRequest, AuthorizationUpdateResponse>((_, response) => response.AuthorizationUpdated = true)
            .ReturnsAsync(OperationResult.Ok());

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();

        var payload = okResult!.Value as AuthorizationUpdateResponse;
        payload.Should().NotBeNull();
        payload!.ProvisioningResult.Should().NotBeNull();
        payload.ProvisioningResult!.Status.Should().Be(PennylaneAccessStatuses.Created);
        payload.AuthorizationUpdated.Should().BeTrue();
        payload.ProvisioningStepCompleted.Should().BeTrue();

        _pennylaneAuthServiceMock.Verify(x => x.HasPennylaneAccessAsync(request), Times.Once);
        _pennylaneAuthServiceMock.Verify(x => x.TryHandlePennylaneProvisioningAsync(request, It.IsAny<AuthorizationUpdateResponse>()), Times.Once);
        _pennylaneAuthServiceMock.Verify(x => x.TryUpdatePennylaneRoleAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
        _authorizationWorkflowServiceMock.Verify(x => x.TryUpdateAuthorizationsAsync(request, It.IsAny<AuthorizationUpdateResponse>()), Times.Once);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithPennylanePermissionAndFailedStatus_ShouldReturnBadRequestAndSkipAuthorization()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);
        var badRequest = OperationResult.Fail(new BadRequestObjectResult(new ErrorResponse()));

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(false);

        _pennylaneAuthServiceMock
            .Setup(x => x.TryHandlePennylaneProvisioningAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(badRequest);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        _authorizationWorkflowServiceMock.Verify(x => x.TryUpdateAuthorizationsAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithoutPennylanePermission_AndNoExistingAccess_ShouldOnlyCallAuthorizationService()
    {
        // Arrange
        var request = BuildRequest(["OTHER_CODE"]);

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(false);

        _authorizationWorkflowServiceMock
            .Setup(x => x.TryUpdateAuthorizationsAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .Callback<AuthorizationUpdateRequest, AuthorizationUpdateResponse>((_, response) => response.AuthorizationUpdated = true)
            .ReturnsAsync(OperationResult.Ok());

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        _pennylaneAuthServiceMock.Verify(x => x.HasPennylaneAccessAsync(request), Times.Once);
        _pennylaneAuthServiceMock.Verify(x => x.TryHandlePennylaneProvisioningAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
        _pennylaneAuthServiceMock.Verify(x => x.TryRevokePennylaneAccessAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
        _authorizationWorkflowServiceMock.Verify(x => x.TryUpdateAuthorizationsAsync(request, It.IsAny<AuthorizationUpdateResponse>()), Times.Once);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WhenAuthorizationUpdateFailsAfterPennylane_ShouldReturnBadRequest()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);
        var badRequest = OperationResult.Fail(new BadRequestObjectResult(new ErrorResponse()));

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(false);

        _pennylaneAuthServiceMock
            .Setup(x => x.TryHandlePennylaneProvisioningAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(OperationResult.Ok());

        _authorizationWorkflowServiceMock
            .Setup(x => x.TryUpdateAuthorizationsAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(badRequest);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WhenAuthorizationUpdateFailsWithoutPennylane_ShouldReturnBadRequest()
    {
        // Arrange
        var request = BuildRequest(["OTHER_CODE"]);
        var badRequest = OperationResult.Fail(new BadRequestObjectResult(new ErrorResponse()));

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(false);

        _authorizationWorkflowServiceMock
            .Setup(x => x.TryUpdateAuthorizationsAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(badRequest);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        _pennylaneAuthServiceMock.Verify(x => x.TryHandlePennylaneProvisioningAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
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
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithPennylanePermissionAndEmptyRole_ShouldReturnBadRequestBeforeProvisioning()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);
        request.Authorization!.Role = string.Empty;

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        _pennylaneAuthServiceMock.Verify(x => x.TryHandlePennylaneProvisioningAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
        _authorizationWorkflowServiceMock.Verify(x => x.TryUpdateAuthorizationsAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithExistingAccessAndEmptyRole_ShouldReturnBadRequestBeforeRoleUpdate()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);
        request.Authorization!.Role = "   ";

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        _pennylaneAuthServiceMock.Verify(x => x.TryUpdatePennylaneRoleAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithPennylanePermissionAndExistingAccess_ShouldSkipProvisioningAndReturnOk()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(true);

        _pennylaneAuthServiceMock
            .Setup(x => x.TryUpdatePennylaneRoleAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .Callback<AuthorizationUpdateRequest, AuthorizationUpdateResponse>((_, response) =>
            {
                response.ProvisioningResult = new AccessProvisioningResult { Status = PennylaneAccessStatuses.ExistingUserAccessGranted, Message = "ok" };
                response.ProvisioningStepCompleted = true;
                response.AuthorizationUpdated = true;
            })
            .ReturnsAsync(OperationResult.Ok());

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();

        var payload = okResult!.Value as AuthorizationUpdateResponse;
        payload.Should().NotBeNull();
        payload!.ProvisioningResult.Should().NotBeNull();
        payload.ProvisioningResult!.Status.Should().Be(PennylaneAccessStatuses.ExistingUserAccessGranted);
        payload.AuthorizationUpdated.Should().BeTrue();
        payload.ProvisioningStepCompleted.Should().BeTrue();

        _pennylaneAuthServiceMock.Verify(x => x.TryUpdatePennylaneRoleAsync(request, It.IsAny<AuthorizationUpdateResponse>()), Times.Once);
        _pennylaneAuthServiceMock.Verify(x => x.TryHandlePennylaneProvisioningAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
        _authorizationWorkflowServiceMock.Verify(x => x.TryUpdateAuthorizationsAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WhenPennylaneProvisioningReturnsBadGateway_ShouldReturnBadGateway()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);
        var badGateway = OperationResult.Fail(new ObjectResult(new ErrorResponse { ErrorCode = Errors.BadRequestDownstreamCode }) { StatusCode = StatusCodes.Status502BadGateway });

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(false);

        _pennylaneAuthServiceMock
            .Setup(x => x.TryHandlePennylaneProvisioningAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(badGateway);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);

        _authorizationWorkflowServiceMock.Verify(x => x.TryUpdateAuthorizationsAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithExistingAccess_WhenPennylaneRoleUpdateReturnsBadGateway_ShouldReturnBadGateway()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);
        var badGateway = OperationResult.Fail(new ObjectResult(new ErrorResponse { ErrorCode = Errors.BadRequestDownstreamCode }) { StatusCode = StatusCodes.Status502BadGateway });

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(true);

        _pennylaneAuthServiceMock
            .Setup(x => x.TryUpdatePennylaneRoleAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(badGateway);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WhenAuthorizationServiceThrowsHttpRequestException_ShouldReturnBadGateway()
    {
        // Arrange
        var request = BuildRequest(["OTHER_CODE"]);
        var badGateway = OperationResult.Fail(new ObjectResult(new ErrorResponse { ErrorCode = Errors.BadRequestDownstreamCode }) { StatusCode = StatusCodes.Status502BadGateway });

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(false);

        _authorizationWorkflowServiceMock
            .Setup(x => x.TryUpdateAuthorizationsAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(badGateway);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WhenPennylaneProvisioningThrowsApiError_ShouldReturnDownstreamStatus()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);
        var downstream = OperationResult.Fail(new ObjectResult(new ErrorResponse { ErrorCode = Errors.BadRequestDownstreamCode }) { StatusCode = (int)HttpStatusCode.BadRequest });

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(false);

        _pennylaneAuthServiceMock
            .Setup(x => x.TryHandlePennylaneProvisioningAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(downstream);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithExistingAccess_WhenRoleUpdateThrowsApiError_ShouldReturnDownstreamStatus()
    {
        // Arrange
        var request = BuildRequest([PermissionCodes.PennylaneAccess]);
        var downstream = OperationResult.Fail(new ObjectResult(new ErrorResponse { ErrorCode = Errors.BadRequestDownstreamCode }) { StatusCode = (int)HttpStatusCode.BadRequest });

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(true);

        _pennylaneAuthServiceMock
            .Setup(x => x.TryUpdatePennylaneRoleAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(downstream);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithoutPennylanePermission_ButWithExistingAccess_ShouldRevokeAndUpdateAuthorization()
    {
        // Arrange
        var request = BuildRequest(["OTHER_CODE"]);

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(true);

        _pennylaneAuthServiceMock
            .Setup(x => x.TryRevokePennylaneAccessAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .Callback<AuthorizationUpdateRequest, AuthorizationUpdateResponse>((_, response) =>
            {
                response.RevocationResult = new AccessRevocationResult { Status = PennylaneAccessStatuses.Revoked, Message = "ok" };
                response.RevocationStepCompleted = true;
            })
            .ReturnsAsync(OperationResult.Ok());

        _authorizationWorkflowServiceMock
            .Setup(x => x.TryUpdateAuthorizationsAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .Callback<AuthorizationUpdateRequest, AuthorizationUpdateResponse>((_, response) => response.AuthorizationUpdated = true)
            .ReturnsAsync(OperationResult.Ok());

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();

        var payload = okResult!.Value as AuthorizationUpdateResponse;
        payload.Should().NotBeNull();
        payload!.RevocationResult.Should().NotBeNull();
        payload.RevocationResult!.Status.Should().Be(PennylaneAccessStatuses.Revoked);
        payload.AuthorizationUpdated.Should().BeTrue();
        payload.RevocationStepCompleted.Should().BeTrue();

        _pennylaneAuthServiceMock.Verify(x => x.HasPennylaneAccessAsync(request), Times.Once);
        _pennylaneAuthServiceMock.Verify(x => x.TryRevokePennylaneAccessAsync(request, It.IsAny<AuthorizationUpdateResponse>()), Times.Once);
        _pennylaneAuthServiceMock.Verify(x => x.TryHandlePennylaneProvisioningAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
        _authorizationWorkflowServiceMock.Verify(x => x.TryUpdateAuthorizationsAsync(request, It.IsAny<AuthorizationUpdateResponse>()), Times.Once);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithoutPennylanePermission_WhenRevocationFails_ShouldReturnBadRequestAndSkipAuthorization()
    {
        // Arrange
        var request = BuildRequest(["OTHER_CODE"]);
        var badRequest = OperationResult.Fail(new BadRequestObjectResult(new ErrorResponse()));

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(true);

        _pennylaneAuthServiceMock
            .Setup(x => x.TryRevokePennylaneAccessAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(badRequest);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        _pennylaneAuthServiceMock.Verify(x => x.TryRevokePennylaneAccessAsync(request, It.IsAny<AuthorizationUpdateResponse>()), Times.Once);
        _authorizationWorkflowServiceMock.Verify(x => x.TryUpdateAuthorizationsAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WithoutPennylanePermission_WhenRevocationReturnsBadGateway_ShouldReturnBadGateway()
    {
        // Arrange
        var request = BuildRequest(["OTHER_CODE"]);
        var badGateway = OperationResult.Fail(new ObjectResult(new ErrorResponse { ErrorCode = Errors.BadRequestDownstreamCode }) { StatusCode = StatusCodes.Status502BadGateway });

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(true);

        _pennylaneAuthServiceMock
            .Setup(x => x.TryRevokePennylaneAccessAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(badGateway);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);

        _authorizationWorkflowServiceMock.Verify(x => x.TryUpdateAuthorizationsAsync(It.IsAny<AuthorizationUpdateRequest>(), It.IsAny<AuthorizationUpdateResponse>()), Times.Never);
    }

    [Fact]
    public async Task CreateAuthorizationsAsync_WhenAuthorizationUpdateFailsAfterRevocation_ShouldReturnBadRequest()
    {
        // Arrange
        var request = BuildRequest(["OTHER_CODE"]);
        var badRequest = OperationResult.Fail(new BadRequestObjectResult(new ErrorResponse()));

        _pennylaneAuthServiceMock
            .Setup(x => x.HasPennylaneAccessAsync(request))
            .ReturnsAsync(true);

        _pennylaneAuthServiceMock
            .Setup(x => x.TryRevokePennylaneAccessAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(OperationResult.Ok());

        _authorizationWorkflowServiceMock
            .Setup(x => x.TryUpdateAuthorizationsAsync(request, It.IsAny<AuthorizationUpdateResponse>()))
            .ReturnsAsync(badRequest);

        // Act
        var result = await _controller.CreateOrUpdateAuthorizationsAsync(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static AuthorizationUpdateRequest BuildRequest(IList<string> permissions)
    {
        return new AuthorizationUpdateRequest
        {
            PermissionsCodes = permissions,
            Authorization = new PennylaneAuthorizationDetails
            {
                AccountId = 123,
                ContactId = 456,
                Role = "role"
            }
        };
    }
}
