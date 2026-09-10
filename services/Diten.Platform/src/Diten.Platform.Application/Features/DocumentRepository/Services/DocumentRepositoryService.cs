using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.DocumentRepository.Services;

/// <summary>
/// MOD-0262-FU01 — the repository's authorisation and orchestration boundary.
/// <para>
/// ⛔ <b>AD-5 — this is the point the old contract got wrong.</b> The seam previously documented that
/// "the caller is responsible for all permission checks BEFORE invoking this". That is safe in-process and a
/// privilege-escalation hole across a service boundary, because a remote caller can simply assert it already
/// checked. This service therefore re-establishes tenant ownership itself on every read and write, in addition
/// to the controller's <c>[HasPermission]</c> layer, and never accepts an object key from a caller.
/// </para>
/// <para>
/// <b>Non-leakage:</b> another tenant's object is invisible, not forbidden — the repository's execution filter
/// excludes it, so the result is <c>404</c>, never <c>403</c>. A 403 would confirm that the id exists.
/// </para>
/// <para>
/// <b>Storage-first with compensation:</b> bytes are written before the metadata row, and if the metadata
/// commit fails the stored object is best-effort deleted so no orphan binary is left. If that compensation
/// itself fails it is logged as an orphan-cleanup follow-up and never silently swallowed.
/// </para>
/// <para>⛔ <b>AD-6:</b> there is no purge, destroy, scheduler or cascade path in this service.</para>
/// </summary>
public sealed class DocumentRepositoryService
{
    private readonly IContentStorageGateway _storage;
    private readonly IRepositoryObjectRepository _objects;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<DocumentRepositoryService> _logger;

    public DocumentRepositoryService(
        IContentStorageGateway storage,
        IRepositoryObjectRepository objects,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        ILogger<DocumentRepositoryService> logger)
    {
        _storage = storage;
        _objects = objects;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Response<RepositoryObjectModel>> StoreAsync(
        StoreRepositoryObjectInput input, string correlationId, CancellationToken ct)
    {
        // AD-5: tenant identity comes from the authenticated context, never from the request body.
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        if (input.Content is null || string.IsNullOrWhiteSpace(input.FileName))
        {
            return Response<RepositoryObjectModel>.Fail(
                "A file is required.", 400, DocumentRepositoryReasonCodes.ValidationFailed, correlationId);
        }

        var actor = _currentUser.ActorName;

        var stored = await _storage.StoreAsync(
            new ContentStoreRequest(
                tenantId,
                input.CompanyId,
                input.Scope,
                input.OwningItemId,
                input.OwningVersionId,
                input.FileName,
                input.MediaType,
                input.Content,
                actor),
            ct);

        if (!stored.IsSuccessful || stored.Data is null)
        {
            return Response<RepositoryObjectModel>.Fail(
                stored.Errors,
                stored.StatusCode == 0 ? 503 : stored.StatusCode,
                stored.ReasonCode ?? DocumentRepositoryReasonCodes.StorageUnavailable,
                correlationId);
        }

        var result = stored.Data;

        try
        {
            var row = await _objects.CreateAsync(new RepositoryObject
            {
                TenantId = tenantId,
                ContentId = result.ContentId,
                StorageProvider = result.StorageProvider,
                ObjectKey = result.ObjectKey,
                Scope = input.Scope.ToString(),
                OwningItemId = input.OwningItemId,
                OwningVersionId = input.OwningVersionId,
                CompanyId = input.CompanyId,
                FileName = result.FileName,
                MediaType = result.MediaType,
                ByteSize = result.ByteSize,
                Checksum = result.Checksum,
                CreatedBy = actor
            }, ct);

            return Response<RepositoryObjectModel>.Success(ToModel(row), correlationId: correlationId);
        }
        catch (Exception ex)
        {
            // No metadata orphan: the bytes landed but the row did not, so remove the object.
            if (!await _storage.TryDeleteAsync(result.StorageProvider, result.ObjectKey, CancellationToken.None))
            {
                _logger.LogError(ex,
                    "Repository metadata commit failed AND compensation delete failed for content {ContentId}; " +
                    "orphan-cleanup follow-up required (MOD-0262-FU03).", result.ContentId);
            }
            else
            {
                _logger.LogWarning(ex,
                    "Repository metadata commit failed for content {ContentId}; stored object was compensated.",
                    result.ContentId);
            }

            return Response<RepositoryObjectModel>.Fail(
                "Content storage is unavailable.", 503, DocumentRepositoryReasonCodes.StorageUnavailable, correlationId);
        }
    }

    public async Task<Response<RepositoryObjectModel>> GetAsync(Guid contentId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var row = await _objects.GetByContentIdAsync(contentId, ct);
        if (row is null)
        {
            // Another tenant's object lands here too — 404, never 403 (non-leakage).
            return Response<RepositoryObjectModel>.Fail(
                "Content not found.", 404, DocumentRepositoryReasonCodes.NotFoundNonLeakage, correlationId);
        }

        return Response<RepositoryObjectModel>.Success(ToModel(row), correlationId: correlationId);
    }

    /// <summary>
    /// Resolves a content id to a readable stream. The object key is looked up from the tenant-scoped row —
    /// it is never taken from the caller, so possessing a raw key grants nothing.
    /// </summary>
    public async Task<Response<RepositoryObjectContentHandle>> OpenReadAsync(
        Guid contentId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var row = await _objects.GetByContentIdAsync(contentId, ct);
        if (row is null)
        {
            return Response<RepositoryObjectContentHandle>.Fail(
                "Content not found.", 404, DocumentRepositoryReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var stream = await _storage.OpenReadAsync(row.StorageProvider, row.ObjectKey, ct);
        if (!stream.IsSuccessful || stream.Data is null)
        {
            // A missing physical object behind a live row is an orphan; FU03 reconciles these.
            if (stream.StatusCode == 404)
            {
                _logger.LogWarning(
                    "Repository row {ContentId} has no physical object at its key; orphan metadata (MOD-0262-FU03).",
                    contentId);
            }

            return Response<RepositoryObjectContentHandle>.Fail(
                stream.Errors,
                stream.StatusCode == 0 ? 503 : stream.StatusCode,
                stream.ReasonCode ?? DocumentRepositoryReasonCodes.StorageUnavailable,
                correlationId);
        }

        return Response<RepositoryObjectContentHandle>.Success(
            new RepositoryObjectContentHandle(stream.Data.Content, row.MediaType, row.FileName, row.ByteSize),
            correlationId: correlationId);
    }

    /// <summary>
    /// ⛔ Compensation only (AD-6). Marks the metadata row and best-effort removes the stored object after a
    /// failed consumer commit. This is <b>not</b> a purge: it performs no retention evaluation, no legal-hold
    /// check and produces no destruction evidence, because it is not the destruction path. Physical destruction
    /// under a disposition decision is MOD-0262-FU05 and must re-verify holds at execution time.
    /// </summary>
    public async Task<Response<NoContent>> CompensateAsync(Guid contentId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var row = await _objects.GetByContentIdAsync(contentId, ct);
        if (row is null)
        {
            return Response<NoContent>.Fail(
                "Content not found.", 404, DocumentRepositoryReasonCodes.NotFoundNonLeakage, correlationId);
        }

        await _objects.MarkCompensatedAsync(contentId, ct);

        if (!await _storage.TryDeleteAsync(row.StorageProvider, row.ObjectKey, ct))
        {
            _logger.LogWarning(
                "Compensation delete failed for content {ContentId}; orphan-cleanup follow-up required (MOD-0262-FU03).",
                contentId);
        }

        return Response<NoContent>.Success(204, correlationId);
    }

    private static RepositoryObjectModel ToModel(RepositoryObject r) => new(
        r.ContentId,
        r.StorageProvider,
        r.Scope,
        r.OwningItemId,
        r.OwningVersionId,
        r.FileName,
        r.MediaType,
        r.ByteSize,
        r.Checksum,
        r.CreatedAt,
        r.CreatedBy);
}
