using ApiGateway.Account.Constants;
using ApiGateway.Pennylane.Constants;

namespace ApiGateway.Pennylane.Mappers
{
    public static class AccountingTypeMapper
    {
        public static string AccountingTypeToPennylaneAccountingType(string accountingType)
        {
            return accountingType switch
            {
                AccountConstants.Engagemment => PennylaneConstants.Accrual,
                AccountConstants.Treasury => PennylaneConstants.CashBased,
                _ => string.Empty,
            };
        }
    }
}
