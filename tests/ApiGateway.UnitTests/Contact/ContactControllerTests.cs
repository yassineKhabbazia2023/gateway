using ApiGateway.Contact;
using ApiGateway.Contact.Exceptions;
using ApiGateway.Identity.context;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;

namespace ApiGateway.UnitTests.Contact;

public class ContactControllerTests
{
    [Fact]
    public async Task GetContactById_ContactFound_ReturnOk()
    {
        // Arrange
        var contact = new ApiGateway.Contact.Models.Contact()
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "jdoe@test.fr",
            LandPhone = "123",
            MobilePhone = "456",
            OldId = "00000000-0000-0000-0000-000000000000",
            Type = "Customer"
        };

        var response = new ApiGateway.Contact.Models.ContactMeViewModel(1, "John", "Doe", "jdoe@test.fr", "123", "456", "00000000-0000-0000-0000-000000000000", "Customer");

        var contactService = new Mock<IContactService>(MockBehavior.Strict);
        contactService.Setup(c => c.GetContactAsync("jdoe@test.fr")).ReturnsAsync(contact);

        Claim claimContext = new Claim(ClaimValueTypes.Email, "jdoe@test.fr");

        var userClaimPrincipal = new Mock<ClaimsPrincipal>(MockBehavior.Strict);
        var userContext = new Mock<IUserContext>(MockBehavior.Strict);
        userContext.SetupGet(uc => uc.User).Returns(userClaimPrincipal.Object);
        userContext.Setup(f => f.User.FindFirst(It.IsAny<string>())).Returns(claimContext);

        var controller = new ContactController(userContext.Object, contactService.Object);

        // Act
        var actionResult = await controller.Me() as ObjectResult;

        // Assert
        contactService.Verify(s => s.GetContactAsync("jdoe@test.fr"), Times.Once);
        
        actionResult!.StatusCode.Should().Be((int)HttpStatusCode.OK);
        actionResult!.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task GetContactById_ContactNotFound_ReturnNotFound()
    {
        // Arrange
        var contact = new ApiGateway.Contact.Models.Contact()
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "jdoe@test.fr",
            LandPhone = "123",
            MobilePhone = "456"
        };

        var contactService = new Mock<IContactService>(MockBehavior.Strict);
        contactService.Setup(c => c.GetContactAsync("jdoe@test.fr")).Throws(new ContactNotFoundException());

        Claim claimContext = new Claim(ClaimValueTypes.Email, "jdoe@test.fr");

        var userClaimPrincipal = new Mock<ClaimsPrincipal>(MockBehavior.Strict);
        var userContext = new Mock<IUserContext>(MockBehavior.Strict);
        userContext.SetupGet(uc => uc.User).Returns(userClaimPrincipal.Object);
        userContext.Setup(f => f.User.FindFirst(It.IsAny<string>())).Returns(claimContext);

        var controller = new ContactController(userContext.Object, contactService.Object);

        // Act
        var actionResult = await controller.Me() as NotFoundResult;

        // Assert
        contactService.Verify(s => s.GetContactAsync("jdoe@test.fr"), Times.Once);
        actionResult!.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetContactById_MethodReturnNull_ReturnBadRequest()
    {
        // Arrange
        var contact = new ApiGateway.Contact.Models.Contact()
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "jdoe@test.fr",
            LandPhone = "123",
            MobilePhone = "456"
        };

        var contactService = new Mock<IContactService>(MockBehavior.Strict);
        contactService.Setup(c => c.GetContactAsync("jdoe@test.fr")).ReturnsAsync((ApiGateway.Contact.Models.Contact?)null);

        Claim claimContext = new Claim(ClaimValueTypes.Email, "jdoe@test.fr");

        var userClaimPrincipal = new Mock<ClaimsPrincipal>(MockBehavior.Strict);
        var userContext = new Mock<IUserContext>(MockBehavior.Strict);
        userContext.SetupGet(uc => uc.User).Returns(userClaimPrincipal.Object);
        userContext.Setup(f => f.User.FindFirst(It.IsAny<string>())).Returns(claimContext);

        var controller = new ContactController(userContext.Object, contactService.Object);

        // Act
        var actionResult = await controller.Me() as BadRequestResult;

        // Assert
        contactService.Verify(s => s.GetContactAsync("jdoe@test.fr"), Times.Once);
        actionResult!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }
}
