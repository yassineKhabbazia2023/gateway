namespace ApiGateway.Offer.Constants;

/// <summary>
/// Codes des offres du micro-service Offer.
/// Doit rester aligné avec Pulse.Offer.Core.Constants.AllowedOfferCodes.
/// Ne jamais utiliser l'OfferId numérique : le script de seed ne le fixe pas.
/// </summary>
public static class OfferCodes
{
    public const string Pennylane = "Pennylane";
}
