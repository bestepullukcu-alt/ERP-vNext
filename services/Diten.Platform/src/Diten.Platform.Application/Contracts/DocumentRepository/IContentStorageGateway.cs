using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Contracts.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 — the single binary/content storage abstraction, owned by the Internal Document Repository
/// Service. ALL file persistence (store, open/stream, best-effort delete) goes through this seam. Domain and
/// Application code never touches a filesystem path, a provider SDK, or raw bytes directly.
/// <para>
/// <b>Relocated by MOD-0262-FU01 (DCP-008 AD-8 / OD-1 / OD-A).</b> This contract previously lived inside a
/// consumer's feature folder (<c>Features/DocumentManagementControlledDocuments/Services/</c>), which made the
/// repository depend on MOD-0029. It now lives in this shared contract location inside
/// <c>Diten.Platform.Application</c> — deliberately <b>not</b> <c>Diten.Platform.Common</c>: cross-service
/// contract extraction is deferred to MOD-0262-FU07 (service extraction), per OD-A.
/// </para>
/// <para>
/// <b>AD-2 — the seam is preserved, not rewritten.</b> Existing MOD-0029 consumers keep calling exactly these
/// operations with identical semantics and identical reason codes.
/// </para>
/// <para>
/// ⛔ <b>AD-5 — the previous "caller is responsible for all permission checks" contract is REVOKED.</b> That
/// assumption was valid only in-process. Callers must reach the repository through an application service that
/// authorises independently; the gateway resolves object keys and moves bytes, and is never the authorisation
/// boundary. A remote caller's claim of prior authorisation is never trusted.
/// </para>
/// <para>
/// ⛔ <b>AD-6 — there is no purge path here.</b> <see cref="TryDeleteAsync"/> is best-effort orphan
/// compensation only (it returns <c>false</c> on failure by design). Physical destruction of records under a
/// retention/disposition decision is MOD-0262-FU05 and must re-verify legal hold at execution time; it must
/// never be built on top of this method.
/// </para>
/// </summary>
public interface IContentStorageGateway
{
    /// <summary>
    /// Validates (extension/media type/size), sanitizes the file name, builds a deterministic per-tenant object
    /// key, computes a SHA-256 checksum while streaming, and writes the bytes. Returns a pointer result — never
    /// raw bytes.
    /// <para>
    /// <b>AD-4:</b> the payload is a <see cref="Stream"/>. The former <c>byte[] Content</c> member was removed
    /// so that no binary payload is ever buffered whole or carried in a JSON body. The stream is read once,
    /// forward-only; the caller owns its lifetime.
    /// </para>
    /// </summary>
    Task<Response<ContentStoreResult>> StoreAsync(ContentStoreRequest request, CancellationToken ct = default);

    /// <summary>
    /// Opens a read stream for a previously stored object. The gateway only resolves the object key —
    /// authorisation and tenant ownership are enforced by the calling application service (AD-5). Returns a
    /// 404-style failure when the object is absent.
    /// </summary>
    Task<Response<ContentStreamResult>> OpenReadAsync(string storageProvider, string objectKey, CancellationToken ct = default);

    /// <summary>
    /// Best-effort delete used by the "no metadata orphan" compensation path. Returns <c>false</c> on any
    /// failure (the caller records an orphan-cleanup follow-up). ⛔ Not a purge path — see AD-6.
    /// </summary>
    Task<bool> TryDeleteAsync(string storageProvider, string objectKey, CancellationToken ct = default);
}

/// <summary>
/// Logical partition of the repository. Additive only: extending this enum stays backward compatible because
/// the value is projected into the object key by name, never by ordinal.
/// </summary>
public enum ContentStorageScope
{
    Documents = 0,
    Templates = 1,

    /// <summary>MOD-0024 Slice ATT-1 — task attachments/evidence/deliverables. Additive only, per this enum's own
    /// doc comment; the value is projected into the object key by name, never by ordinal.</summary>
    TaskAttachments = 2,

    /// <summary>CAND-CAP-0011 / SCMM-16 (WP-SCMM-16A) — rendered structured-content &amp; messaging artifacts
    /// (e.g. a composed ContentSetRevision PDF). Additive only, per this enum's own doc comment; the value is
    /// projected into the object key by name (its own <c>content-messaging-artifacts</c> partition), never by
    /// ordinal, so existing persisted keys are unaffected. The render service (SCMM-16B, CrmService) stores and
    /// reads these through the existing MOD-0262-FU01 <c>/api/v1/document-repository</c> surface.</summary>
    ContentMessagingArtifacts = 3
}

/// <summary>
/// A store request. <see cref="Content"/> is a forward-only stream (AD-4) — never a materialised byte array.
/// <see cref="TenantId"/> is a structural part of the object key, not merely a query filter.
/// </summary>
public sealed record ContentStoreRequest(
    Guid TenantId,
    Guid CompanyId,
    ContentStorageScope Scope,
    Guid ItemId,
    Guid VersionId,
    string FileName,
    string? DeclaredMediaType,
    Stream Content,
    string CreatedBy,
    string? StoragePartition = null);

/// <summary>Pointer result of a successful store. <c>ObjectKey</c> is an internal detail and is never returned to a client.</summary>
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
