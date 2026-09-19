using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Enzo.Diagrams.Api;

internal sealed class ApiKeyEndpointFilter(IOptions<EnzoOptions> options, TimeProvider timeProvider) : IEndpointFilter
{
    public const string HeaderName = "X-API-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var requiredScope = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<ApiKeyScopeRequirement>()?.Scope;
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var values)
            || values.Count != 1
            || string.IsNullOrWhiteSpace(values[0])
            || !IsAuthorized(values[0]!, requiredScope))
        {
            return Results.Problem(
                title: "Unauthorized.",
                detail: "A valid API key is required.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context);
    }

    private bool IsAuthorized(string providedApiKey, string? requiredScope)
    {
        foreach (var configuredApiKey in options.Value.ApiKeys)
        {
            if (configuredApiKey.ExpiresAt <= timeProvider.GetUtcNow()
                || !AllowsScope(configuredApiKey, requiredScope)
                || !KeysMatch(providedApiKey, configuredApiKey.Sha256))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool AllowsScope(ApiKeyOptions configuredApiKey, string? requiredScope)
    {
        return requiredScope is null
            || configuredApiKey.Scopes.Any(scope =>
                string.Equals(scope, ApiKeyScopes.All, StringComparison.OrdinalIgnoreCase)
                || string.Equals(scope, requiredScope, StringComparison.OrdinalIgnoreCase));
    }

    private static bool KeysMatch(string providedApiKey, string configuredSha256)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedApiKey));

        byte[] configuredHash;
        try
        {
            configuredHash = Convert.FromHexString(configuredSha256);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (configuredHash.Length != providedHash.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }
}

internal static class ApiKeyScopes
{
    public const string All = "*";
    public const string Validate = "validate";
    public const string Render = "render";
}

internal sealed record ApiKeyScopeRequirement(string Scope);

internal sealed class EnzoOptions
{
    public const string SectionName = "Enzo";

    public List<ApiKeyOptions> ApiKeys { get; init; } = [];

    public RenderResultOptions RenderResults { get; init; } = new();

    public RequestLimitOptions Limits { get; init; } = new();
}

internal sealed class ApiKeyOptions
{
    public string Id { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public string[] Scopes { get; init; } = [];

    public DateTimeOffset? ExpiresAt { get; init; }
}

internal sealed class RenderResultOptions
{
    public string Store { get; init; } = "Local";

    public int UrlLifetimeMinutes { get; init; } = 30;

    public string? BlobConnectionString { get; init; }

    public string? BlobContainerName { get; init; }

    public string LocalDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "enzo-diagrams-render-results");
}

internal sealed class RequestLimitOptions
{
    public int MaxRequestBodyBytes { get; init; } = 64 * 1024;

    public int MaxSourceCharacters { get; init; } = 32 * 1024;

    public int MaxSourceLines { get; init; } = 1_000;

    public int MaxDiagramElements { get; init; } = 500;

    public int MaxDiagramConnections { get; init; } = 1_000;

    public int MaxPngWidth { get; init; } = 8_000;

    public int MaxPngHeight { get; init; } = 8_000;

    public int MaxPngPixels { get; init; } = 16_000_000;
}
