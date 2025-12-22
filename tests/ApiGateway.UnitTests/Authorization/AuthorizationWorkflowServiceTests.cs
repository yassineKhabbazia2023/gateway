using ApiGateway.Authorization;
using ApiGateway.Authorization.Models;
using ApiGateway.Exceptions;
using ApiGateway.Pennylane.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Pulse.ExceptionMiddleware.Model;

namespace ApiGateway.UnitTests.Authorization;

public class AuthorizationWorkflowServiceTests
{
    private readonly Mock<IAuthorizationService> _authorizationServiceMock;
    private readonly Mock<ILogger<AuthorizationWorkflowService>> _loggerMock;
    private readonly AuthorizationWorkflowService _service;

    public AuthorizationWorkflowServiceTests()
    {
        _authorizationServiceMock = new Mock<IAuthorizationService>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<AuthorizationWorkflowService>>();
        _service = new AuthorizationWorkflowService(
            _authorizationServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task TryUpdateAuthorizationsAsync_WhenSuccess_ShouldReturnOk()
    {
        var request = BuildRequest();
        var response = new AuthorizationUpdateResponse();

        _authorizationServiceMock
            .Setup(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
                request.Authorization!.ContactId,
                request.Authorization.AccountId,
                request.PermissionsCodes))
            .ReturnsAsync(true);

        var result = await _service.TryUpdateAuthorizationsAsync(request, response);

        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        response.AuthorizationUpdated.Should().BeTrue();
    }

    [Fact]
    public async Task TryUpdateAuthorizationsAsync_WhenServiceReturnsFalse_ShouldReturnBadRequest()
    {
        var request = BuildRequest();
        var response = new AuthorizationUpdateResponse();

        _authorizationServiceMock
            .Setup(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
                request.Authorization!.ContactId,
                request.Authorization.AccountId,
                request.PermissionsCodes))
            .ReturnsAsync(false);

        var result = await _service.TryUpdateAuthorizationsAsync(request, response);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TryUpdateAuthorizationsAsync_WhenHttpRequestException_ShouldReturnBadGateway()
    {
        var request = BuildRequest();
        var response = new AuthorizationUpdateResponse();

        _authorizationServiceMock
            .Setup(x => x.CreateOrUpdateContactAccountAuthorizationAsync(
                request.Authorization!.ContactId,
                request.Authorization.AccountId,
                request.PermissionsCodes))
            .ThrowsAsync(new HttpRequestException("downstream"));

        var result = await _service.TryUpdateAuthorizationsAsync(request, response);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        var objectResult = result.Error!.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }

    private static AuthorizationUpdateRequest BuildRequest()
    {
        return new AuthorizationUpdateRequest
        {
            PermissionsCodes = new List<string> { "CODE" },
            Authorization = new PennylaneAuthorizationDetails
            {
                AccountId = 123,
                ContactId = 456,
                Role = "role"
            }
        };
    }
}
