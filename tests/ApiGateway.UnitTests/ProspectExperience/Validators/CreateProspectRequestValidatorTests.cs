using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Validators;
using FluentAssertions;
using Xunit;

namespace ApiGateway.UnitTests.ProspectExperience.Validators;

public class SignatoryDtoValidatorTests
{
    private readonly SignatoryDtoValidator _validator = new();

    private static SignatoryDto CreateValidSignatory(string email = "signatory@client.fr") => new()
    {
        Title = "M.",
        LastName = "Dupont",
        FirstName = "Jean",
        JobTitle = "Directeur",
        Department = "Finance",
        CompanyRole = "Signataire",
        ContactTypes = ["Legal"],
        Email = email,
        MobilePhone = "0612345678"
    };

    [Theory]
    [InlineData("signatory@rydge.fr")]
    [InlineData("SIGNATORY@RYDGE.FR")]
    [InlineData("signatory@Rydge.Fr")]
    public void Should_Fail_When_Email_Uses_Rydge_Domain(string email)
    {
        var signatory = CreateValidSignatory(email);

        var result = _validator.Validate(signatory);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(SignatoryDto.Email) &&
            e.ErrorMessage == "La création d'un prospect avec une adresse email '@rydge.fr' est interdite.");
    }

    [Fact]
    public void Should_Succeed_When_Email_Uses_Another_Domain()
    {
        var signatory = CreateValidSignatory("signatory@client.fr");

        var result = _validator.Validate(signatory);

        result.IsValid.Should().BeTrue();
    }
}
