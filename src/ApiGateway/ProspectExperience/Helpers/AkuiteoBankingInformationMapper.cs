using ApiGateway.ProspectExperience.Models.Internal;

namespace ApiGateway.ProspectExperience.Helpers;

/// <summary>
/// Maps Mandat bank-details extraction responses to Registry Akuiteo requests.
/// </summary>
public static class AkuiteoBankingInformationMapper
{
    private const string AddAction = "ADD";
    private const string DirectDebitPaymentMethod = "DIRECT_DEBIT";

    /// <summary>
    /// Maps extracted banking details to one Akuiteo banking-information addition.
    /// </summary>
    /// <param name="source">The banking details extracted by Mandat.</param>
    /// <returns>The Registry banking-information request.</returns>
    public static AkuiteoBankingInformationRequest ToBankingInformationRequest(
        MandateBankDetailsExtractionResponse source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AkuiteoBankingInformationRequest
        {
            Sepa = new AkuiteoSepaRequest
            {
                BankDetails = new AkuiteoBankDetailsRequest
                {
                    Entity = source.Rib.BankCode,
                    Counter = source.Rib.BranchCode,
                    AccountNumber = source.Rib.AccountNumber,
                    Key = source.Rib.RibKey,
                    Domiciliation = source.Domiciliation
                },
                Bic = new AkuiteoBicRequest
                {
                    Country = source.Bic.CountryCode,
                    Bank = source.Bic.BankCode,
                    Location = source.Bic.LocationCode,
                    Branch = source.Bic.BranchCode
                },
                Iban = new AkuiteoIbanRequest
                {
                    Country = source.Iban.CountryCode,
                    Key = source.Iban.CheckDigits,
                    AccountNumber = source.Iban.BankAccountPart
                }
            },
            NoneSepa = null,
            Action = AddAction
        };
    }

    /// <summary>
    /// Creates the exact account patch required to activate direct debit.
    /// </summary>
    /// <returns>The Akuiteo account payment-method patch.</returns>
    public static AkuiteoAccountPaymentMethodRequest ToDirectDebitPaymentMethodRequest()
    {
        return new AkuiteoAccountPaymentMethodRequest
        {
            ConditionOfPayment = new AkuiteoConditionOfPaymentRequest
            {
                DeadLine = string.Empty,
                Term = string.Empty,
                Day = 0
            },
            MethodOfPayment = DirectDebitPaymentMethod
        };
    }
}
