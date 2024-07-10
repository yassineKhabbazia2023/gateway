using ApiGateway.UserOrigin;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ApiGateway.UnitTests.UserOrigin;

public class UserOriginControllerTest
{
    [Fact]
    public async Task Index_WhenRemoteIsKPMG_ShouldReturnCollab()
    {
        // Arrange
        var values = new Dictionary<string, string>
        {
            {"KPMG_IP", "199.199.199.199" }
        };
        var confBuilder = new ConfigurationBuilder().AddInMemoryCollection(values);
        var configuration = confBuilder.Build();

        var controller = new UserOriginController(configuration);
        var ct = new DefaultHttpContext();
        ct.Request.Headers["X-REAL-IP"] = "199.199.199.199";
        controller.ControllerContext = new ControllerContext()
        {
            HttpContext = ct,
        };

        // Act
        var result = await controller.Index();

        // Assert
        result.Result.Should().BeAssignableTo<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult.Value.Should().BeSameAs("COLLAB");
    }
    [Fact]
    public async Task Index_WhenRemoteIsNotKPMG_ShouldReturnClient()
    {
        // Arrange
        var values = new Dictionary<string, string>
        {
            {"KPMG_IP", "199.199.199.199" }
        };
        var confBuilder = new ConfigurationBuilder().AddInMemoryCollection(values);
        var configuration = confBuilder.Build();

        var controller = new UserOriginController(configuration);
        var ct = new DefaultHttpContext();
        ct.Request.Headers["X-REAL-IP"] = "199.199.199.192";
        controller.ControllerContext = new ControllerContext()
        {
            HttpContext = ct,
        };


        // Act
        var result = await controller.Index();

        // Assert
        result.Result.Should().BeAssignableTo<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult.Value.Should().BeSameAs("CLIENT");

    }
}