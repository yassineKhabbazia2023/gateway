using ApiGateway.Exceptions;
using ApiGateway.ProspectExperience.Models.Requests;
using FluentValidation;

namespace ApiGateway.ProspectExperience.Validators;

public class CreateProspectRequestValidator : AbstractValidator<CreateProspectRequest>
{
    public CreateProspectRequestValidator()
    {
        RuleFor(x => x.Siret)
            .NotEmpty().WithMessage("Le SIRET est requis.")
            .Matches(@"^\d{14}$").WithMessage("Le SIRET doit contenir exactement 14 chiffres.");

        RuleFor(x => x.LegalForm).NotEmpty().WithMessage("La forme juridique est requise.");
        RuleFor(x => x.LegalStructure).NotEmpty().WithMessage("La structure juridique est requise.");
        RuleFor(x => x.AccountManagerContactId).GreaterThan(0)
            .WithMessage("Le gestionnaire de compte (AccountManagerContactId) doit être renseigné.");

        When(x => x.CaseManagerContactId.HasValue, () =>
        {
            RuleFor(x => x.CaseManagerContactId!.Value).GreaterThan(0)
                .WithName(nameof(CreateProspectRequest.CaseManagerContactId))
                .WithMessage("Le chargé de dossier (CaseManagerContactId) doit être supérieur à 0.");
        });
        RuleFor(x => x.Department).NotEmpty().WithMessage("Le département est requis.");
        RuleFor(x => x.Region).NotEmpty().WithMessage("La région est requise.");
        RuleFor(x => x.Country).NotEmpty().WithMessage("Le pays est requis.");

        RuleFor(x => x.Signatory).NotNull().WithMessage("Les informations du signataire sont requises.")
            .SetValidator(new SignatoryDtoValidator());
    }
}

public class SignatoryDtoValidator : AbstractValidator<SignatoryDto>
{
    public SignatoryDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("La civilité du signataire est requise.");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Le nom du signataire est requis.");
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("Le prénom du signataire est requis.");
        RuleFor(x => x.JobTitle).NotEmpty().WithMessage("La fonction du signataire est requise.");
        RuleFor(x => x.Department).NotEmpty().WithMessage("Le département du signataire est requis.");
        RuleFor(x => x.CompanyRole).NotEmpty().WithMessage("Le rôle du signataire dans l'entreprise est requis.");
        RuleFor(x => x.ContactTypes).NotEmpty().WithMessage("Au moins un type de contact du signataire doit être renseigné.");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("L'email du signataire est requis.")
            .EmailAddress().WithMessage("L'email du signataire est invalide.")
            .Must(email => !email.EndsWith("@rydge.fr", StringComparison.OrdinalIgnoreCase))
            .WithErrorCode(Errors.ProspectSignatoryEmailDomainForbiddenCode)
            .WithMessage(Errors.ProspectSignatoryEmailDomainForbiddenMessage);
        RuleFor(x => x.MobilePhone).NotEmpty().WithMessage("Le numéro de téléphone mobile du signataire est requis.");
    }
}
