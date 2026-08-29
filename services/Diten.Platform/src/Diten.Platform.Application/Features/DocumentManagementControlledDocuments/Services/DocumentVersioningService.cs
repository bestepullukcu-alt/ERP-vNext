using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;

/// <summary>
/// MOD-0029-FU01 — immutable version creation helper: decodes the upload, writes content storage-first through
/// <see cref="IContentStorageGateway"/>, and maps the storage result into a <see cref="ContentRef"/> pointer.
/// Metadata is committed by the calling aggregate service only AFTER storage succeeds; on a metadata failure the
/// caller best-effort deletes the stored object (no metadata orphan).
/// </summary>
public sealed class DocumentVersioningService
{
    private readonly IContentStorageGateway _storage;
    private readonly ITenantContext _tenantContext;

    public DocumentVersioningService(IContentStorageGateway storage, ITenantContext tenantContext)
    {
        _storage = storage;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ContentStoreResult>> StoreAsync(
        ContentStorageScope scope,
        Guid companyId,
        Guid itemId,
        Guid versionId,
        FileUploadInput file,
        string createdBy,
        string correlationId,
        CancellationToken ct,
        string? storagePartition = null)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        if (file is null || string.IsNullOrWhiteSpace(file.ContentBase64) || string.IsNullOrWhiteSpace(file.FileName))
        {
            return Response<ContentStoreResult>.Fail(
                "A file is required.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(file.ContentBase64);
        }
        catch (FormatException)
        {
            return Response<ContentStoreResult>.Fail(
                "Uploaded content is not valid base64.", 400, ControlledDocumentReasonCodes.ValidationFailed, correlationId);
        }

        var request = new ContentStoreRequest(
            tenantId, companyId, scope, itemId, versionId, file.FileName, file.MediaType, bytes, createdBy,
            storagePartition);

        var stored = await _storage.StoreAsync(request, ct);
        if (!stored.IsSuccessful)
        {
            return Response<ContentStoreResult>.Fail(
                stored.Errors,
                stored.StatusCode == 0 ? 503 : stored.StatusCode,
                stored.ReasonCode ?? ControlledDocumentReasonCodes.StorageUnavailable,
                correlationId);
        }

        return Response<ContentStoreResult>.Success(stored.Data!, correlationId: correlationId);
    }

    public Task<bool> TryDeleteAsync(ContentStoreResult stored, CancellationToken ct) =>
        _storage.TryDeleteAsync(stored.StorageProvider, stored.ObjectKey, ct);

    /// <summary>
    /// Computes the SHA-256 content fingerprint (lowercase hex) of a base64 upload, matching the format produced
    /// by the storage gateway. Used to detect a byte-identical re-upload before any storage write — so a "new
    /// version" that did not actually change the content can be rejected without leaving an orphan object.
    /// Returns <c>null</c> when the payload is empty or not valid base64 (the caller surfaces the real error).
    /// </summary>
    public static string? ComputeChecksum(string? contentBase64)
    {
        if (string.IsNullOrWhiteSpace(contentBase64))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(contentBase64);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static ContentRef ToContentRef(ContentStoreResult r, Guid versionId, string createdBy) => new()
    {
        ContentId = r.ContentId,
        StorageProvider = r.StorageProvider,
        ObjectKey = r.ObjectKey,
        FileName = r.FileName,
        MediaType = r.MediaType,
        ByteSize = r.ByteSize,
        Checksum = r.Checksum,
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = createdBy,
        VersionId = versionId
    };
}
