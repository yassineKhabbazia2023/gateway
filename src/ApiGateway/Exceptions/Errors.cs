namespace ApiGateway.Exceptions
{
    public static class Errors
    {
        public static readonly string NullArgumentCode = "GTW001";
        public static readonly string NullArgumentMessage = "Le paramètre {0} est null ou vide";

        public static readonly string NullConfigurationCode = "GTW002";
        public static readonly string NullConfigurationMessage = "La configuration {0} est null ou vide";

        public static readonly string NotFoundContactCode = "GTW003";
        public static readonly string NotFoundContactMessage = "Le contact avec l'identifiant {0} est introuvable";

        public static readonly string BadRequestDownstreamCode = "GTW004";
        public static readonly string BadRequestDownstreamMessage = "Erreur au niveau du downstream";

        public static readonly string NotValidObjectCode = "GTW005";
        public static readonly string NotValidObjectMessage = "L'objet {0} dans la fonction {1} est invalide";

        public static readonly string NotValidClaimCode = "GTW006";
        public static readonly string NotValidClaimMessage = "L'utilisateur {0} n'a pas les permissions nécessaires sur l'entité {1} - Claims attendus : {2} - Les permissions de l'utilisateur: {3}";

        public static readonly string NoRoleOnAccountCode = "GTW007";
        public static readonly string NoRoleOnAccountMessage = "Le contact avec l'identifiant {0} n'a pas de rôle dans l'entité {1}";

        public static readonly string NoRightOnAccountCode = "GTW008";
        public static readonly string NoRightOnAccountMessage = "Le contact avec l'identifiant {0} n'a pas le droit sur l'entité {1}";

        public static readonly string UnexpectedExceptionCode = "GTW009";
        public static readonly string UnexptectedExceptionMessage = "Erreur inattendue sur la Gateway! {0}";

        public static readonly string NotValidCollaboratorCode = "GTW010";
        public static readonly string NotValidCollaboratorMessage = "l'utilisateur suivant {0} n'est pas un collaborateur valide.";

        public static readonly string NotValidCustomerCode = "GTW011";
        public static readonly string NotValidCustomerMessage = "l'utilisateur suivant {0} n'est pas un client valide.";

        public static readonly string GigyaError = "GTW012";

        public static readonly string PermissionRequiredCode = "GTW013";
        public static readonly string PermissionRequiredMessage = "Les autorisations requises n'existent pas pour l'utilisateur actuel.";

        public static readonly string RoleRequiredCode = "GTW014";
        public static readonly string RoleRequiredMessage = "Le Contact {0} n'a pas de rôle sur l'Account: {1}.";

        public static readonly string NoCommonAccountRoleCode = "GTW015";
        public static readonly string NoCommonAccountRoleMessage = "Les Contacts: [{0}] , [{1}] n'ont pas de rôle commun au niveau des entités.";

        public static readonly string UnauthorizedExposePrivilegedEndpointsCode = "GTW016";
        public static readonly string UnauthorizedExposePrivilegedEndpointsMessage = "Access Forbidden to privileged endpoints.";

        public static readonly string NotFoundAccountCode = "GTW017";
        public static readonly string NotFoundAccountMessage = "L'account avec l'identifiant {0} est introuvable";

        public static readonly string PennylaneHttpRequestFailedCode = "GTW018";
        public static readonly string PennylaneHttpRequestFailedMessage = "L'appelle vers pennylane a échoué {0} {1}";

        public static readonly string UnauthorizedCode = "GTW019";
        public static readonly string UnauthorizedMessage = "Accès non autorisé. Veuillez vous reconnecter.";

        public static readonly string MissingMobilePhoneCode = "GTW020";
        public static readonly string MissingMobilePhoneMessage = "Le contact client avec l'identifiant {0} doit avoir un numéro de téléphone mobile.";

        public static readonly string XpBookingFeatureFlagDisabledCode = "GTW022";
        public static readonly string XpBookingFeatureFlagDisabledMessage = "L'expérience Booking est désactivée.";

        public static readonly string BookingProvisioningFailedCode = "GTW023";
        public static readonly string BookingProvisioningFailedMessage = "Le provisioning du booking a échoué : {0}";

        public static readonly string MissingBearerTokenCode = "GTW024";
        public static readonly string MissingBearerTokenMessage = "Le token d'authentification est manquant ou invalide.";

        public static readonly string NotValidAdministratorCode = "GTW025";
        public static readonly string NotValidAdministratorMessage = "L'utilisateur {0} n'a pas les droits nécessaires pour accéder à cette ressource.";

        public static readonly string ProspectExperienceDisabledCode = "GTW026";
        public static readonly string ProspectExperienceDisabledMessage = "L'expérience Prospect est désactivée.";

        // --- Pennylane ---
        public static readonly string PennylaneFirmAssignmentRequiredCode = "GTW027";
        public static readonly string PennylaneFirmAssignmentRequiredMessage = "Le dossier existe déjà chez Pennylane et est rattaché à un autre cabinet. Une demande de réassignation doit être effectuée.";

        public static readonly string ApprovedPlatformDisabledCode = "GTW028";
        public static readonly string ApprovedPlatformDisabledMessage = "L'abonnement Plateforme agréée est désactivé.";

        public static readonly string ProspectOrchestrationFailedCode = "GTW029";
        public static readonly string ProspectOrchestrationFailedMessage = "L'orchestration de création du prospect a échoué à l'étape {0}.";

        public static readonly string ProspectSiretAlreadyExistsCode = "GTW030";
        public static readonly string ProspectSiretAlreadyExistsMessage = "Un account existe déjà dans Akuiteo pour le SIRET {0}.";

        public static readonly string ProspectBadRequestCode = "GTW031";
        public static readonly string ProspectBadRequestMessage = "La requête de création du prospect est invalide.";

        public static readonly string InvalidRequestCode = "GTW032";
        public static readonly string InvalidRequestMessage = "La requête est invalide.";

        public static readonly string ProspectResumePayloadMismatchCode = "GTW033";
        public static readonly string ProspectResumePayloadMismatchMessage = "Le prospect incomplet pour le SIRET {0} doit être repris avec les mêmes données de création.";

        // --- Feature Flags ---
        public static readonly string FeatureFlagDisabledCode = "GTW034";
        public static readonly string FeatureFlagDisabledMessage = "La fonctionnalité '{0}' est désactivée.";

        public static readonly string ContactResolutionFailedCode = "GTW035";
        public static readonly string ContactResolutionFailedMessage = "La résolution du contact a échoué : le service Contact a répondu {0}.";

        public static readonly string ProspectSignatoryEmailDomainForbiddenCode = "GTW036";
        public static readonly string ProspectSignatoryEmailDomainForbiddenMessage = "La création d'un prospect avec une adresse email '@rydge.fr' est interdite.";

        public static readonly string SerenityChoiceAlreadyExistsCode = "GTW037";
        public static readonly string SerenityChoiceAlreadyExistsMessage = "Un choix Sérénité a déjà été enregistré pour ce contact.";

        public static readonly string SerenityModalDisabledCode = "GTW038";
        public static readonly string SerenityModalDisabledMessage = "La modal Sérénité est désactivée.";
    }
}
