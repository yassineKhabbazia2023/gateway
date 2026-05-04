using System.Security.Claims;
using System.Text;
using ApiGateway.Helpers;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace ApiGateway.UnitTests.Helpers;

public class JwtHelperTests
{
    private const string SigningKey = "a_secure_key_that_is_at_least_32_bytes_long!";

    public static TheoryData<Claim[], string> TokenTestCases => new()
    {
        // AAD : upn doit primer sur email (alias ext ≠ UPN réel)
        {
            [new("upn", "toto@rydge.fr"), new("unique_name", "toto@rydge.Fr"), new("email", "toto-ext@rydge.fr")],
            "toto@rydge.fr"
        },
        // AAD sans claim email
        {
            [new("upn", "toto@rydge.fr"), new("unique_name", "toto@rydge.Fr")],
            "toto@rydge.fr"
        },
        // Gigya : pas de upn, fallback sur email
        {
            [new("email", "toto@gmail.com")],
            "toto@gmail.com"
        },
        // Aucun claim email-like
        {
            [new("sub", "some-subject-id")],
            ""
        },
    };

    [Theory]
    [MemberData(nameof(TokenTestCases))]
    public void ExtractUserEmailFromToken_ReturnsExpectedEmail(Claim[] claims, string expectedEmail)
    {
        var token = BuildToken(claims);

        JwtHelper.ExtractUserEmailFromToken(token).Should().Be(expectedEmail);
    }

    private static string BuildToken(Claim[] claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
