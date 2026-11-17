using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;

namespace TennisSlotWatcher.Services;

/// <summary>Persists the set of slot keys that are currently available (and therefore already alerted).</summary>
internal interface ISlotStateStore
{
    Task<HashSet<string>> LoadAsync(CancellationToken ct);
    Task SaveAsync(IReadOnlyCollection<string> keys, CancellationToken ct);
}

internal sealed class BlobSlotStateStore(BlobServiceClient service) : ISlotStateStore
{
    private const string Container = "slotwatcher";
    private const string BlobName = "alerted-slots.json";

    public async Task<HashSet<string>> LoadAsync(CancellationToken ct)
    {
        var blob = service.GetBlobContainerClient(Container).GetBlobClient(BlobName);
        try
        {
            var content = await blob.DownloadContentAsync(ct);
            var keys = content.Value.Content.ToObjectFromJson<string[]>() ?? [];
            return new HashSet<string>(keys, StringComparer.Ordinal);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    public async Task SaveAsync(IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        var container = service.GetBlobContainerClient(Container);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);

        var json = JsonSerializer.SerializeToUtf8Bytes(keys.Order(StringComparer.Ordinal).ToArray());
        await container.GetBlobClient(BlobName).UploadAsync(new BinaryData(json), overwrite: true, ct);
    }
}
