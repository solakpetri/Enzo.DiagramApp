using System.Collections.Concurrent;
using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Enzo.Diagrams.Api;

internal interface IRenderResultStore
{
    ValueTask<StoredRenderResult> StoreAsync(
        byte[] bytes,
        string contentType,
        TimeSpan lifetime,
        Uri requestBaseUri,
        CancellationToken cancellationToken);
}

internal interface ILocalRenderResultReader
{
    ValueTask<StoredRenderResultContent?> GetAsync(string id, CancellationToken cancellationToken);
}

internal sealed record StoredRenderResult(string Id, string Url, DateTimeOffset ExpiresAt);

internal sealed record StoredRenderResultContent(byte[] Bytes, string ContentType, DateTimeOffset ExpiresAt);

internal sealed class LocalRenderResultStore(RenderResultOptions options, TimeProvider timeProvider) :
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

internal sealed class AzureBlobRenderResultStore(RenderResultOptions options, TimeProvider timeProvider) : IRenderResultStore
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

        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        await using var stream = new MemoryStream(bytes, writable: false);
        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            }
        }, cancellationToken);

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

internal static class RenderResultIds
{
    public static string Create()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }
}

internal class RenderResultStoreException(string message, Exception? innerException = null) : Exception(message, innerException);

internal sealed class RenderResultStoreConfigurationException(string message) : RenderResultStoreException(message);
