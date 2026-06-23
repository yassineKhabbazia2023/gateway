namespace ApiGateway.Pennylane.Mappers
{
    public static class CountryCodeMapper
    {
        private const string defaultCountryCode = "FR";
        private static readonly Dictionary<string, string> countryCodes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Autriche", "AT" },
                { "Belgique", "BE" },
                { "Bulgarie", "BG" },
                { "Chypre", "CY" },
                { "République tchèque", "CZ" },
                { "Allemagne", "DE" },
                { "Danemark", "DK" },
                { "Estonie", "EE" },
                { "Espagne", "ES" },
                { "Finlande", "FI" },
                { "France", "FR" },
                { "Grèce", "GR" },
                { "Croatie", "HR" },
                { "Hongrie", "HU" },
                { "Irlande", "IE" },
                { "Italie", "IT" },
                { "Lituanie", "LT" },
                { "Luxembourg", "LU" },
                { "Lettonie", "LV" },
                { "Malte", "MT" },
                { "Pays-Bas", "NL" },
                { "Pologne", "PL" },
                { "Portugal", "PT" },
                { "Roumanie", "RO" },
                { "Suède", "SE" },
                { "Slovénie", "SI" },
                { "Slovaquie", "SK" },
                { "Royaume-Uni", "GB" },
                { "Monaco", "MC" },
                { "Suisse", "CH" },
                { "Andorre", "AD" },
                { "Maurice", "MU" },
                { "Norvège", "NO" }
            };

        public static string CountryToPennylaneCountryCode(string country)
        {
            if (country == null)
            {
                return defaultCountryCode;
            }

            return countryCodes.TryGetValue(country, out var code)
                ? code
                : defaultCountryCode;
        }
    }
}
