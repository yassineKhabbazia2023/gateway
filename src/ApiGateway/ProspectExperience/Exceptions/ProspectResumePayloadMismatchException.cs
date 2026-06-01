using ApiGateway.Exceptions;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Exceptions;

/// <summary>
/// Represents the conflict raised when a user retries an incomplete prospect creation with
/// payload changes that would diverge from the persisted Prospect checkpoint.
/// </summary>
public sealed class ProspectResumePayloadMismatchException : GatewayException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProspectResumePayloadMismatchException"/> class.
    /// </summary>
    /// <param name="siret">The SIRET of the incomplete prospect being retried.</param>
    public ProspectResumePayloadMismatchException(string siret)
        : base(
            StatusCodes.Status409Conflict,
            Errors.ProspectResumePayloadMismatchCode,
            string.Format(Errors.ProspectResumePayloadMismatchMessage, siret))
    {
    }
}
