using System.Collections.Concurrent;
using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Enzo.Diagrams.Application;

namespace Enzo.Diagrams.Infrastructure;

public sealed class LocalRenderResultStore(RenderResultStorageOptions options, TimeProvider timeProvider) :
    IRenderResultStore,
    ILocalRenderResultReader
{
    private readonly ConcurrentDictionary<string, StoredLocalRenderResult> results = new();

    public async ValueTask<StoredRenderResult> StoreAsync(
        byte[] bytes,
        string contentType,
        TimeSpan lifetime,
        Uri requestBaseUri,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.LocalDirectory);
        CleanupExpired();

        var id = RenderResultIds.Create();
        var expiresAt = timeProvider.GetUtcNow().Add(lifetime);
        var filePath = Path.Combine(options.LocalDirectory, $"{id}.png");
        await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);

        results[id] = new StoredLocalRenderResult(filePath, contentType, expiresAt);
        return new StoredRenderResult(id, new Uri(requestBaseUri, $"v1/render-results/{id}").ToString(), expiresAt);
    }

    public async ValueTask<StoredRenderResultContent?> GetAsync(string id, CancellationToken cancellationToken)
    {
        if (!results.TryGetValue(id, out var result))
        {
            return null;
        }

        if (result.ExpiresAt <= timeProvider.GetUtcNow())
        {
            Remove(id, result);
            return null;
        }

        if (!File.Exists(result.FilePath))
        {
            results.TryRemove(id, out _);
            return null;
        }

        return new StoredRenderResultContent(
            await File.ReadAllBytesAsync(result.FilePath, cancellationToken),
            result.ContentType,
            result.ExpiresAt);
    }

    private void CleanupExpired()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var (id, result) in results)
        {
            if (result.ExpiresAt <= now)
            {
                Remove(id, result);
            }
        }
    }

    private void Remove(string id, StoredLocalRenderResult result)
    {
        results.TryRemove(id, out _);
        try
        {
            File.Delete(result.FilePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record StoredLocalRenderResult(string FilePath, string ContentType, DateTimeOffset ExpiresAt);
}

public sealed class AzureBlobRenderResultStore(RenderResultStorageOptions options, TimeProvider timeProvider) : IRenderResultStore
{
    private const string BlobPrefix = "render-results";
    private readonly BlobContainerClient containerClient = new(options.BlobConnectionString, options.BlobContainerName);

    public async ValueTask<StoredRenderResult> StoreAsync(
        byte[] bytes,
        string contentType,
        TimeSpan lifetime,
        Uri requestBaseUri,
        CancellationToken cancellationToken)
    {
        var id = RenderResultIds.Create();
        var blobName = $"{BlobPrefix}/{id}.png";
        var blobClient = containerClient.GetBlobClient(blobName);
        if (!blobClient.CanGenerateSasUri)
        {
            throw new RenderResultStoreConfigurationException("Hosted render storage is not configured for read-only URL generation.");
        }

        try
        {
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            await using var stream = new MemoryStream(bytes, writable: false);
            await blobClient.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            }, cancellationToken);
        }
        catch (RequestFailedException exception)
        {
            throw new RenderResultStoreException("The rendered diagram could not be stored.", exception);
        }

        var expiresAt = timeProvider.GetUtcNow().Add(lifetime);
        var sasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, expiresAt)
        {
            BlobContainerName = options.BlobContainerName,
            BlobName = blobName,
            Resource = "b"
        };

        return new StoredRenderResult(id, blobClient.GenerateSasUri(sasBuilder).ToString(), expiresAt);
    }

}

public static class RenderResultIds
{
    public static string Create()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }
}
