using ApiGateway.Exceptions;
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
            Assert.Throws<GatewayException>(() => principal.GetEmail());
        }

        [Fact]
        public void GetEmail_WhenPrincipalDoesNotContainEmailClaim_ShouldThrowArgumentException()
        {
            // Arrange
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>()));

            // Act & Assert
            var exception = Assert.Throws<GatewayException>(() => principal.GetEmail());
            Assert.Equal("Le paramètre emailClaim est null ou vide", exception.Message);
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

        public static TheoryData<List<Claim>, string> GetEmailResolutionCases => new()
        {
            // AAD : UPN prime sur email (alias externe ≠ identité réelle)
            {
                [new(ClaimTypes.Upn, "toto@rydge.fr"), new(ClaimTypes.Email, "toto-ext@rydge.fr")],
                "toto@rydge.fr"
            },
            // AAD sans claim email
            {
                [new(ClaimTypes.Upn, "toto@rydge.fr")],
                "toto@rydge.fr"
            },
            // Gigya : pas de UPN, fallback sur email
            {
                [new(ClaimTypes.Email, "toto@gmail.com")],
                "toto@gmail.com"
            },
        };

        [Theory]
        [MemberData(nameof(GetEmailResolutionCases))]
        public void GetEmail_ResolvesUpnThenEmail(List<Claim> claims, string expectedEmail)
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

            Assert.Equal(expectedEmail, principal.GetEmail());
        }

        [Fact]
        public void IsCollaborator_WhenPrincipalIsNull_ShouldThrowArgumentNullException()
        {
            // Arrange
            ClaimsPrincipal principal = null;

            // Act & Assert
            Assert.Throws<GatewayException>(() => principal.IsCollaborator());
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
            Assert.Throws<GatewayException>(() => principal.IsCustomer());
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

        [Fact]
        public void IsAdministrator_WhenPrincipalIsNull_ShouldThrowArgumentNullException()
        {
            // Arrange
            ClaimsPrincipal principal = null!;

            // Act & Assert
            Assert.Throws<GatewayException>(() => principal.IsAdministrator());
        }

        [Fact]
        public void IsAdministrator_WhenPrincipalHasAdministratorRole_ShouldReturnTrue()
        {
            // Arrange
            var claims = new List<Claim> { new Claim(ClaimTypes.Role, "Administrator") };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = principal.IsAdministrator();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsAdministrator_WhenPrincipalDoesNotHaveAdministratorRole_ShouldReturnFalse()
        {
            // Arrange
            var claims = new List<Claim> { new Claim(ClaimTypes.Role, "SomeOtherRole") };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

            // Act
            var result = principal.IsAdministrator();

            // Assert
            Assert.False(result);
        }
    }
}
