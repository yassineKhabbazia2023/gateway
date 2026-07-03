using ApiGateway.ProspectExperience.Models.Requests;
using FluentValidation;

namespace ApiGateway.ProspectExperience.Validators;

public class CreateProspectRequestValidator : AbstractValidator<CreateProspectRequest>
{
    public CreateProspectRequestValidator()
    {
        RuleFor(x => x.Siret)
            .NotEmpty()
            .Matches(@"^\d{14}$").WithMessage("Le SIRET doit contenir exactement 14 chiffres.");

        RuleFor(x => x.LegalForm).NotEmpty();
        RuleFor(x => x.LegalStructure).NotEmpty();
        RuleFor(x => x.AccountManagerContactId).GreaterThan(0);

        When(x => x.CaseManagerContactId.HasValue, () =>
        {
            RuleFor(x => x.CaseManagerContactId!.Value).GreaterThan(0)
                .WithName(nameof(CreateProspectRequest.CaseManagerContactId));
        });
        RuleFor(x => x.Department).NotEmpty();
        RuleFor(x => x.Region).NotEmpty();
        RuleFor(x => x.Country).NotEmpty();

        RuleFor(x => x.Signatory).NotNull().SetValidator(new SignatoryDtoValidator());
    }
}

public class SignatoryDtoValidator : AbstractValidator<SignatoryDto>
{
    public SignatoryDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.LastName).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty();
        RuleFor(x => x.JobTitle).NotEmpty();
        RuleFor(x => x.Department).NotEmpty();
        RuleFor(x => x.CompanyRole).NotEmpty();
        RuleFor(x => x.ContactTypes).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.MobilePhone).NotEmpty();
    }
}
