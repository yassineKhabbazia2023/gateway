using System.Net;
using System.Text;
using ApiGateway.ProspectExperience.Controllers;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.UnitTests.ProspectExperience.Controllers;

/// <summary>
/// Unit tests for the Prospect create-password experience controller.
/// </summary>
public class CreatePasswordExperienceControllerTests
{
    private readonly Mock<ICreatePasswordExperienceService> createPasswordExperienceService = new(MockBehavior.Strict);
    private readonly CreatePasswordExperienceController controller;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePasswordExperienceControllerTests"/> class.
    /// </summary>
    public CreatePasswordExperienceControllerTests()
    {
        controller = new CreatePasswordExperienceController(createPasswordExperienceService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    /// <summary>
    /// Ensures the controller forwards the request to the service and returns Contact status codes.
    /// </summary>
    [Fact]
    public async Task CreateNewPassword_ForwardsRequestToServiceAndReturnsContactStatus()
    {
        // Arrange
        var request = new CreatePasswordExperienceRequest(42, "reset-token", "NewPassword123", true);
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        createPasswordExperienceService
            .Setup(s => s.CreateNewPasswordAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await controller.CreateNewPassword(request, ct: CancellationToken.None);

        // Assert
        var statusCodeResult = result.Should().BeOfType<StatusCodeResult>().Subject;
        statusCodeResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
        createPasswordExperienceService.Verify(
            s => s.CreateNewPasswordAsync(request, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Ensures downstream error response status, body, and content type are preserved.
    /// </summary>
    [Fact]
    public async Task CreateNewPassword_WhenContactReturnsErrorBody_PreservesStatusBodyAndContentType()
    {
        // Arrange
        var request = new CreatePasswordExperienceRequest(42, "reset-token", "NewPassword123", true);
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"errorCode\":\"ERR\"}", Encoding.UTF8, "application/json")
        };
        createPasswordExperienceService
            .Setup(s => s.CreateNewPasswordAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await controller.CreateNewPassword(request, ct: CancellationToken.None);

        // Assert
        var contentResult = result.Should().BeOfType<ContentResult>().Subject;
        contentResult.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
        contentResult.Content.Should().Be("{\"errorCode\":\"ERR\"}");
        contentResult.ContentType.Should().Be("application/json; charset=utf-8");
    }

    /// <summary>
    /// Ensures the controller exposes the selected Prospect experience route.
    /// </summary>
    [Fact]
    public void CreateNewPassword_ShouldUseProspectExperienceRoute()
    {
        // Arrange
        var controllerRoute = typeof(CreatePasswordExperienceController)
            .GetCustomAttributes(typeof(RouteAttribute), false)
            .OfType<RouteAttribute>()
            .Single();
        var method = typeof(CreatePasswordExperienceController).GetMethod(nameof(CreatePasswordExperienceController.CreateNewPassword));

        // Act
        var httpPost = method!
            .GetCustomAttributes(typeof(HttpPostAttribute), false)
            .OfType<HttpPostAttribute>()
            .Single();

        // Assert
        controllerRoute.Template.Should().Be("gtw/password-experience/api");
        httpPost.Template.Should().Be("createNewPassword");
    }

    /// <summary>
    /// Ensures the Gateway endpoint does not accept contactId as a query parameter.
    /// </summary>
    [Fact]
    public void CreateNewPassword_ShouldNotAcceptContactIdFromQuery()
    {
        // Arrange
        var method = typeof(CreatePasswordExperienceController).GetMethod(nameof(CreatePasswordExperienceController.CreateNewPassword));

        // Act
        var parameters = method!.GetParameters();

        // Assert
        parameters.Should().ContainSingle(parameter => parameter.ParameterType == typeof(CreatePasswordExperienceRequest));
        parameters.Should().NotContain(parameter => parameter.GetCustomAttributes(typeof(FromQueryAttribute), false).Any());
    }
}
