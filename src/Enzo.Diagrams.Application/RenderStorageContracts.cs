namespace Enzo.Diagrams.Application;

public interface IRenderResultStore
{
    ValueTask<StoredRenderResult> StoreAsync(
        byte[] bytes,
        string contentType,
        TimeSpan lifetime,
        Uri requestBaseUri,
        CancellationToken cancellationToken);
}

public interface ILocalRenderResultReader
{
    ValueTask<StoredRenderResultContent?> GetAsync(string id, CancellationToken cancellationToken);
}

public sealed record StoredRenderResult(string Id, string Url, DateTimeOffset ExpiresAt);

public sealed record StoredRenderResultContent(byte[] Bytes, string ContentType, DateTimeOffset ExpiresAt);

public class RenderResultStoreException(string message, Exception? innerException = null) : Exception(message, innerException);

public sealed class RenderResultStoreConfigurationException(string message) : RenderResultStoreException(message);
