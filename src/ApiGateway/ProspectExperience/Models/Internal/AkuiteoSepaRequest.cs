namespace ApiGateway.ProspectExperience.Models.Internal;

/// <summary>
/// Represents SEPA banking information sent through Registry to Akuiteo.
/// </summary>
public sealed class AkuiteoSepaRequest
{
    /// <summary>Gets or sets the domestic bank details.</summary>
    public AkuiteoBankDetailsRequest BankDetails { get; set; } = null!;

    /// <summary>Gets or sets the BIC components.</summary>
    public AkuiteoBicRequest Bic { get; set; } = null!;

    /// <summary>Gets or sets the IBAN components.</summary>
    public AkuiteoIbanRequest Iban { get; set; } = null!;
}
