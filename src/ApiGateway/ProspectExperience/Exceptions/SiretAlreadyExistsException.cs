using ApiGateway.Exceptions;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.ProspectExperience.Exceptions;

public class SiretAlreadyExistsException : GatewayException
{
    public string Siret { get; }

    public SiretAlreadyExistsException(string siret)
        : base(StatusCodes.Status409Conflict,
               Errors.ProspectSiretAlreadyExistsCode,
               string.Format(Errors.ProspectSiretAlreadyExistsMessage, siret))
    {
        Siret = siret;
    }
}
