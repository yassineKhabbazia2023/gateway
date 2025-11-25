using ApiGateway.Pennylane.Mappers;

namespace ApiGateway.UnitTests.Pennylane
{
    public class CountryCodeMapperTests
    {
        private const string DefaultCode = "FR";

        [Theory]
        [InlineData("France", "FR")]
        [InlineData("Belgique", "BE")]
        [InlineData("Allemagne", "DE")]
        [InlineData("Espagne", "ES")]
        [InlineData("Royaume-Uni", "GB")]
        [InlineData("Suisse", "CH")]
        [InlineData("Norvège", "NO")]
        public void CountryToPennylaneCountryCode_KnownCountry_ReturnsExpectedCode(string country, string expectedCode)
        {
            var result = CountryCodeMapper.CountryToPennylaneCountryCode(country);
            Assert.Equal(expectedCode, result);
        }

        [Theory]
        [InlineData("france", "FR")]
        [InlineData("FRANCE", "FR")]
        [InlineData("FrAnCe", "FR")]
        [InlineData("belgique", "BE")]
        [InlineData("royaume-uni", "GB")]
        public void CountryToPennylaneCountryCode_IsCaseInsensitive(string country, string expectedCode)
        {
            var result = CountryCodeMapper.CountryToPennylaneCountryCode(country);
            Assert.Equal(expectedCode, result);
        }

        [Fact]
        public void CountryToPennylaneCountryCode_UnknownCountry_ReturnsDefault()
        {
            var result = CountryCodeMapper.CountryToPennylaneCountryCode("Mars");
            Assert.Equal(DefaultCode, result);
        }

        [Fact]
        public void CountryToPennylaneCountryCode_NullInput_ReturnsDefault()
        {
            var result = CountryCodeMapper.CountryToPennylaneCountryCode(null);
            Assert.Equal(DefaultCode, result);
        }

        [Fact]
        public void CountryToPennylaneCountryCode_SimilarButIncorrectName_ReturnsDefault()
        {
            var result = CountryCodeMapper.CountryToPennylaneCountryCode("Republique tcheque");
            Assert.Equal(DefaultCode, result);
        }
    }
}
