using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — the single binary/content storage abstraction. ALL file persistence (store, open/stream,
/// delete) goes through this Application-layer seam. Domain/Application code never touches a filesystem path,
/// a provider SDK, or raw bytes directly. Phase 1 is <c>LocalFileSystemContentStorageGateway</c>; Phase 2 swaps
/// only the provider implementation behind this unchanged interface.
/// </summary>
public interface IContentStorageGateway
{
    /// <summary>Validates (type/size), sanitizes the file name, builds a deterministic per-tenant/company/item/version
    /// object key, computes a SHA-256 checksum, and writes the bytes. Returns a pointer result — never raw bytes.</summary>
    Task<Response<ContentStoreResult>> StoreAsync(ContentStoreRequest request, CancellationToken ct = default);

    /// <summary>Opens a read stream for a previously stored object. Caller is responsible for all permission checks
    /// BEFORE invoking this; the gateway only resolves the object key. Returns 404-style failure if absent.</summary>
    Task<Response<ContentStreamResult>> OpenReadAsync(string storageProvider, string objectKey, CancellationToken ct = default);

    /// <summary>Best-effort delete used by the "no metadata orphan" compensation path. Returns false on any failure
    /// (the caller records an orphan-cleanup follow-up).</summary>
    Task<bool> TryDeleteAsync(string storageProvider, string objectKey, CancellationToken ct = default);
}

public enum ContentStorageScope
{
    Documents = 0,
    Templates = 1
}

public sealed record ContentStoreRequest(
    Guid TenantId,
    Guid CompanyId,
    ContentStorageScope Scope,
    Guid ItemId,
    Guid VersionId,
    string FileName,
    string? DeclaredMediaType,
    byte[] Content,
    string CreatedBy);

public sealed record ContentStoreResult(
    Guid ContentId,
    string StorageProvider,
    string ObjectKey,
    string FileName,
    string MediaType,
    long ByteSize,
    string Checksum);

public sealed record ContentStreamResult(
    Stream Content,
    string MediaType,
    string FileName,
    long ByteSize);
