using System.Security.Cryptography;
using System.Text;
using ApiGateway.ProspectExperience.Models.Requests;

namespace ApiGateway.ProspectExperience.Policies;

/// <summary>
/// Builds a stable fingerprint from the user-provided prospect creation payload so Gateway can
/// detect incompatible retries once external side effects already exist.
/// </summary>
public static class ProspectResumeRequestFingerprint
{
    /// <summary>
    /// Builds the fingerprint of the provided prospect creation request.
    /// </summary>
    /// <param name="request">The request to fingerprint.</param>
    /// <returns>The hexadecimal fingerprint.</returns>
    public static string Build(CreateProspectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Signatory);

        var canonical = string.Join(
            '|',
            Normalize(request.LegalForm),
            Normalize(request.LegalStructure),
            Normalize(request.Department),
            Normalize(request.Region),
            Normalize(request.Country),
            request.CaseManagerContactId?.ToString() ?? string.Empty,
            request.AccountManagerContactId.ToString(),
            Normalize(request.Signatory.Title),
            Normalize(request.Signatory.LastName),
            Normalize(request.Signatory.FirstName),
            Normalize(request.Signatory.JobTitle),
            Normalize(request.Signatory.Department),
            Normalize(request.Signatory.CompanyRole),
            Normalize(request.Signatory.Email),
            Normalize(request.Signatory.MobilePhone),
            string.Join(',', (request.Signatory.ContactTypes ?? []).Select(Normalize).OrderBy(value => value, StringComparer.Ordinal)));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }
}
