using System.Diagnostics.CodeAnalysis;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ApiGateway.Configuration;
[ExcludeFromCodeCoverage]
public class HideOcelotControllersFilter : IDocumentFilter
{
    private static readonly string[] _ignoredPaths = {
        "/configuration",
        "/outputcache/{region}"
    };
    

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach(var ignorePath in _ignoredPaths)
        {
            swaggerDoc.Paths.Remove(ignorePath);
        }
    }
}