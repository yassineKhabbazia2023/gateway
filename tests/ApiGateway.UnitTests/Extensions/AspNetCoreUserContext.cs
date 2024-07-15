namespace ApiGateway.UnitTests.Extensions;

using ApiGateway.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

public class AspNetCoreUserContextTest
{
    [Fact]
    public void User_Get()
    {
        var user = new System.Security.Claims.ClaimsPrincipal();

        var httpContext = new Mock<HttpContext>(MockBehavior.Strict);
        httpContext.SetupGet(c => c.User).Returns(user);

        var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
        accessor.SetupGet(a => a.HttpContext).Returns(httpContext.Object);

        var context = new AspNetCoreUserContext(accessor.Object);

        var userResult = context.User;
        userResult.Should().BeSameAs(user);
    }
}

