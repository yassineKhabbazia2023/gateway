using ApiGateway.Identity.Extensions;
using System.Security.Claims;

namespace ApiGateway.UnitTests.Identity.Extensions
{
    public class PulseClaimsPrincipalExtensionsTest
    {

        [Fact]
        public void GetEmail_WhenPrincipalIsNull_ShouldThrowArgumentNullException()
        {
            // Arrange
            ClaimsPrincipal principal = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => principal.GetEmail());
        }

        [Fact]
        public void GetEmail_WhenPrincipalDoesNotContainEmailClaim_ShouldThrowArgumentException()
        {
            // Arrange
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>()));

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => principal.GetEmail());
            Assert.Equal("The principal does not contains an email claim (http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress) (Parameter 'principal')", exception.Message);
        }

        [Fact]
        public void GetEmail_WhenPrincipalContainsEmailClaim_ShouldReturnEmail()
        {
            // Arrange
            var email = "user@example.com";
            var claims = new List<Claim> { new Claim(ClaimTypes.Email, email) };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = principal.GetEmail();

            // Assert
            Assert.Equal(email, result);
        }

        [Fact]
        public void IsCollaborator_WhenPrincipalIsNull_ShouldThrowArgumentNullException()
        {
            // Arrange
            ClaimsPrincipal principal = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => principal.IsCollaborator());
        }

        [Fact]
        public void IsCollaborator_WhenPrincipalHasCollaboratorRole_ShouldReturnTrue()
        {
            // Arrange
            var claims = new List<Claim> { new Claim(ClaimTypes.Role, "Collaborator") };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = principal.IsCollaborator();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsCollaborator_WhenPrincipalDoesNotHaveCollaboratorRole_ShouldReturnFalse()
        {
            // Arrange
            var claims = new List<Claim> { new Claim(ClaimTypes.Role, "SomeOtherRole") };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = principal.IsCollaborator();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsCustomer_WhenPrincipalIsNull_ShouldThrowArgumentNullException()
        {
            // Arrange
            ClaimsPrincipal principal = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => principal.IsCustomer());
        }

        [Fact]
        public void IsCustomer_WhenPrincipalHasCustomerRole_ShouldReturnTrue()
        {
            // Arrange
            var claims = new List<Claim> { new Claim(ClaimTypes.Role, "Customer") };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = principal.IsCustomer();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsCustomer_WhenPrincipalDoesNotHaveCustomerRole_ShouldReturnFalse()
        {
            // Arrange
            var claims = new List<Claim> { new Claim(ClaimTypes.Role, "SomeOtherRole") };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = principal.IsCustomer();

            // Assert
            Assert.False(result);
        }
    }
}
