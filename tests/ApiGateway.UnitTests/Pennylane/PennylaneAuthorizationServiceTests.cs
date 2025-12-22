using System.Net;
using ApiGateway.Authorization;
using ApiGateway.Authorization.Consts;
using ApiGateway.Authorization.Models;
using ApiGateway.Exceptions;
using ApiGateway.Pennylane;
using ApiGateway.Pennylane.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.UnitTests.Pennylane;

public class PennylaneAuthorizationServiceTests
{
    private readonly Mock<IAuthorizationService> _authorizationServiceMock;
    private readonly Mock<IPennylaneService> _pennylaneServiceMock;
    private readonly Mock<ILogger<PennylaneAuthorizationService>> _loggerMock;
    private readonly PennylaneAuthorizationService _service;

    public PennylaneAuthorizationServiceTests()
    {
        _authorizationServiceMock = new Mock<IAuthorizationService>(MockBehavior.Strict);
        _pennylaneServiceMock = new Mock<IPennylaneService>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<PennylaneAuthorizationService>>();

        _service = new PennylaneAuthorizationService(
            _authorizationServiceMock.Object,
            _pennylaneServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HasPennylaneAccessAsync_WhenPermissionExists_ShouldReturnTrue()
    {
        var request = BuildRequest();
        _authorizationServiceMock
            .Setup(x => x.GetContactAuthorizationAsync(request.Authorization!.ContactId, request.Authorization.AccountId))
            .ReturnsAsync(new List<string> { PermissionCodes.PennylaneAccess });

        var result = await _service.HasPennylaneAccessAsync(request);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryHandlePennylaneProvisioningAsync_WhenSuccess_ShouldReturnOk()
    {
        var request = BuildRequest();
        var response = new AuthorizationUpdateResponse();
        var grantResult = new GrantPennylaneAccessResult
        {
            Status = PennylaneAccessStatuses.Created,
            Message = "ok",
            ContactId = request.Authorization!.ContactId,
            AccountId = request.Authorization.AccountId
        };

        _pennylaneServiceMock
            .Setup(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()))
            .ReturnsAsync(grantResult);

        var outcome = await _service.TryHandlePennylaneProvisioningAsync(request, response);

        outcome.Success.Should().BeTrue();
        outcome.Error.Should().BeNull();
        response.ProvisioningResult.Should().NotBeNull();
        response.ProvisioningResult!.Status.Should().Be(PennylaneAccessStatuses.Created);
        response.ProvisioningStepCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task TryHandlePennylaneProvisioningAsync_WhenFailedStatus_ShouldReturnBadRequest()
    {
        var request = BuildRequest();
        var response = new AuthorizationUpdateResponse();
        var grantResult = new GrantPennylaneAccessResult
        {
            Status = PennylaneAccessStatuses.Failed,
            Message = "failed",
            ContactId = request.Authorization!.ContactId,
            AccountId = request.Authorization.AccountId
        };

        _pennylaneServiceMock
            .Setup(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()))
            .ReturnsAsync(grantResult);

        var outcome = await _service.TryHandlePennylaneProvisioningAsync(request, response);

        outcome.Success.Should().BeFalse();
        outcome.Error.Should().NotBeNull();
        outcome.Error!.Result.Should().BeOfType<BadRequestObjectResult>();
        response.ProvisioningStepCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task TryHandlePennylaneProvisioningAsync_WhenApiException_ShouldReturnMappedStatus()
    {
        var request = BuildRequest();
        var response = new AuthorizationUpdateResponse();

        _pennylaneServiceMock
            .Setup(x => x.GrantPennylaneAccessAsync(It.IsAny<PennylaneAuthorizationRequest>()))
            .ThrowsAsync(new PennylaneApiException(HttpStatusCode.BadRequest, "bad", "/api/pennylane/role/create"));

        var outcome = await _service.TryHandlePennylaneProvisioningAsync(request, response);

        outcome.Success.Should().BeFalse();
        outcome.Error.Should().NotBeNull();
        var objectResult = outcome.Error!.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TryUpdatePennylaneRoleAsync_WhenSuccess_ShouldReturnOk()
    {
        var request = BuildRequest();
        var response = new AuthorizationUpdateResponse();
        var updateResult = new GrantPennylaneAccessResult
        {
            Status = PennylaneAccessStatuses.ExistingUserAccessGranted,
            Message = "updated",
            ContactId = request.Authorization!.ContactId,
            AccountId = request.Authorization.AccountId,
            Role = request.Authorization.Role
        };

        _pennylaneServiceMock
            .Setup(x => x.UpdatePennylaneRoleAsync(It.IsAny<PennylaneAuthorizationRequest>()))
            .ReturnsAsync(updateResult);

        var outcome = await _service.TryUpdatePennylaneRoleAsync(request, response);

        outcome.Success.Should().BeTrue();
        response.ProvisioningResult.Should().NotBeNull();
        response.AuthorizationUpdated.Should().BeTrue();
    }

    [Fact]
    public async Task TryUpdatePennylaneRoleAsync_WhenHttpRequestException_ShouldReturnBadGateway()
    {
        var request = BuildRequest();
        var response = new AuthorizationUpdateResponse();

        _pennylaneServiceMock
            .Setup(x => x.UpdatePennylaneRoleAsync(It.IsAny<PennylaneAuthorizationRequest>()))
            .ThrowsAsync(new HttpRequestException("downstream"));

        var outcome = await _service.TryUpdatePennylaneRoleAsync(request, response);

        outcome.Success.Should().BeFalse();
        outcome.Error.Should().NotBeNull();
        var objectResult = outcome.Error!.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }

    private static AuthorizationUpdateRequest BuildRequest()
    {
        return new AuthorizationUpdateRequest
        {
            PermissionsCodes = new List<string> { PermissionCodes.PennylaneAccess },
            Authorization = new PennylaneAuthorizationDetails
            {
                AccountId = 123,
                ContactId = 456,
                Role = "role"
            }
        };
    }
}
