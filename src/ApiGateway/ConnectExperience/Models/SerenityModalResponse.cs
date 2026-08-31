namespace ApiGateway.ConnectExperience.Models;

/// <summary>
/// Indique au front-end s'il doit afficher la modal Sérénité.
/// Objet plutôt que booléen nu : extensible sans casser le contrat.
/// </summary>
public sealed record SerenityModalResponse(bool ShouldDisplay);
