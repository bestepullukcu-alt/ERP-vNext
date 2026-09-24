namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.Rendering;

/// <summary>
/// SCMM-16B (CAND-CAP-0011, SCMM-16) — stores and reads a rendered content artifact through the MOD-0262-FU01 document
/// repository (Platform), which CrmService consumes over the Gateway. The seam lives in Application so the render handler
/// depends only on it; the HTTP transport (multipart upload, streamed download, user-token forward) is an Infrastructure
/// detail.
/// <para>
/// ⛔ <b>Fail-closed.</b> A store that does not return a 2xx MUST throw <see cref="ContentArtifactStoreException"/> — never
/// a soft/empty result. The render command then fails and no artifact pointer is bound, so a storage outage can never
/// leave a revision marked rendered with nothing behind it. (Audit, separately, stays fail-soft.)
/// </para>
/// </summary>
public interface IContentArtifactStore
{
    /// <summary>Uploads the rendered bytes to FU01 (scope = content-messaging-artifacts) and returns the stored pointer.
    /// TenantId/CompanyId are resolved server-side from the forwarded caller context, never from the request.</summary>
    Task<ContentArtifactStoreResult> StoreAsync(ContentArtifactStoreRequest request, CancellationToken cancellationToken);

    /// <summary>Opens the stored artifact's byte stream by content id, proxying FU01's content download with the caller's
    /// token forwarded. Returns <c>null</c> when FU01 answers 404 (absent, or another tenant's id — non-leakage). The
    /// caller owns the returned stream's lifetime.</summary>
    Task<ContentArtifactReadResult?> OpenReadAsync(Guid contentId, CancellationToken cancellationToken);
}

/// <summary>What the handler knows at store time. Scope, tenant and company are supplied by the store from the forwarded
/// context — not here — so this carries only the owning identity, the file name/media type, and the bytes.</summary>
public sealed record ContentArtifactStoreRequest(
    Guid OwningItemId,
    Guid OwningVersionId,
    string FileName,
    string MediaType,
    byte[] Content);

/// <summary>The FU01 store pointer. The internal object key is never carried (non-leakage).</summary>
public sealed record ContentArtifactStoreResult(
    Guid ContentId,
    string Checksum,
    long ByteSize,
    string MediaType);

/// <summary>A read stream plus its content metadata. The caller disposes <see cref="Content"/>.</summary>
public sealed record ContentArtifactReadResult(
    Stream Content,
    string MediaType,
    string FileName,
    long ByteSize);

/// <summary>Thrown when the artifact store cannot complete a store (non-2xx from FU01, an auth rejection, or a transport
/// failure). <see cref="StatusCode"/> carries the upstream/derived status so the failure is not swallowed.</summary>
public sealed class ContentArtifactStoreException : Exception
{
    public ContentArtifactStoreException(int statusCode, string message) : base(message) => StatusCode = statusCode;

    public int StatusCode { get; }
}
