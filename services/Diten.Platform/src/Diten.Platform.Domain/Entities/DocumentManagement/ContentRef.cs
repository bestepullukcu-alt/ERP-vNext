namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — embedded pointer to a binary stored through the approved content-storage abstraction
/// (<c>IContentStorageGateway</c>). FU01 never persists raw bytes in Mongo; <see cref="ObjectKey"/> is an
/// internal storage detail and is never exposed to the client.
/// </summary>
public sealed class ContentRef
{
    public required Guid ContentId { get; set; }
    public required string StorageProvider { get; set; }
    public required string ObjectKey { get; set; }
    public required string FileName { get; set; }
    public required string MediaType { get; set; }
    public long ByteSize { get; set; }
    public required string Checksum { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public Guid VersionId { get; set; }
}
