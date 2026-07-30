namespace Diten.BuildingBlocks.Eventing;

/// <summary>
/// Infrastructure-only seam for derived transport authentication metadata.
/// Implementations must never return signing keys or other secret material.
/// </summary>
public interface ITrustedTransportMetadataProvider
{
    ValueTask<TrustedTransportMetadata> CreateAsync(
        EventMetadata metadata,
        ReadOnlyMemory<byte> canonicalPayloadUtf8,
        CancellationToken cancellationToken = default);
}

public sealed class EmptyTrustedTransportMetadataProvider : ITrustedTransportMetadataProvider
{
    public ValueTask<TrustedTransportMetadata> CreateAsync(
        EventMetadata metadata,
        ReadOnlyMemory<byte> canonicalPayloadUtf8,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(TrustedTransportMetadata.Empty);
    }
}
