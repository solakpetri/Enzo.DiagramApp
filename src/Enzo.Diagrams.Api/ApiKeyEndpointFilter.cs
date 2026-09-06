using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Enzo.Diagrams.Api;

internal sealed class ApiKeyEndpointFilter(IOptions<EnzoOptions> options) : IEndpointFilter
{
    public const string HeaderName = "X-API-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var configuredApiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(configuredApiKey)
            || !context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var values)
            || values.Count != 1
            || string.IsNullOrWhiteSpace(values[0])
            || !KeysMatch(values[0]!, configuredApiKey))
        {
            return Results.Problem(
                title: "Unauthorized.",
                detail: "A valid API key is required.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context);
    }

    private static bool KeysMatch(string providedApiKey, string configuredApiKey)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedApiKey));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredApiKey));

        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }
}

internal sealed class EnzoOptions
{
    public const string SectionName = "Enzo";

    public string? ApiKey { get; init; }

    public RenderResultOptions RenderResults { get; init; } = new();
}

internal sealed class RenderResultOptions
{
    public string Store { get; init; } = "Local";

    public int UrlLifetimeMinutes { get; init; } = 30;

    public string? BlobConnectionString { get; init; }

    public string? BlobContainerName { get; init; }

    public string LocalDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "enzo-diagrams-render-results");
}
