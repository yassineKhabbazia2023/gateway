using ApiGateway.Account.Constants;
using ApiGateway.Pennylane.Constants;
using ApiGateway.Pennylane.Mappers;

namespace ApiGateway.UnitTests.Pennylane
{
    public class AccountingTypeMapperTests
    {
        [Fact]
        public void AccountingTypeToPennylaneAccountingType_WhenEngagement_ShouldReturnAccrual()
        {
            // Arrange
            var input = AccountConstants.Engagemment;

            // Act
            var result = AccountingTypeMapper.AccountingTypeToPennylaneAccountingType(input);

            // Assert
            result.Should().Be(PennylaneConstants.Accrual);
        }

        [Fact]
        public void AccountingTypeToPennylaneAccountingType_WhenTreasury_ShouldReturnCashBased()
        {
            // Arrange
            var input = AccountConstants.Treasury;

            // Act
            var result = AccountingTypeMapper.AccountingTypeToPennylaneAccountingType(input);

            // Assert
            result.Should().Be(PennylaneConstants.CashBased);
        }

        [Fact]
        public void AccountingTypeToPennylaneAccountingType_WhenUnknownValue_ShouldReturnEmptyString()
        {
            // Arrange
            var input = "UNKNOWN_TYPE";

            // Act
            var result = AccountingTypeMapper.AccountingTypeToPennylaneAccountingType(input);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void AccountingTypeToPennylaneAccountingType_WhenNull_ShouldReturnEmptyString()
        {
            // Arrange
            string? input = null;

            // Act
            var result = AccountingTypeMapper.AccountingTypeToPennylaneAccountingType(input!);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void AccountingTypeToPennylaneAccountingType_WhenEmpty_ShouldReturnEmptyString()
        {
            // Arrange
            var input = string.Empty;

            // Act
            var result = AccountingTypeMapper.AccountingTypeToPennylaneAccountingType(input);

            // Assert
            result.Should().BeEmpty();
        }
    }
}
