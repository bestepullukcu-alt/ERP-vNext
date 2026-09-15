using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Commands;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Handlers.CommandHandlers;

/// <summary>
/// WP-DM-DCP005-REGISTER-IMPORT-UI-01 — Step 1: preview. Writes nothing — the whole answer comes from
/// <see cref="DocumentRegisterImportPreviewService"/>.
/// </summary>
public sealed class DryRunDocumentRegisterImportHandler(
    DocumentRegisterImportPreviewService previewService, ITenantContext tenantContext)
    : IRequestHandler<DryRunDocumentRegisterImportCommand, Response<DocumentRegisterImportPreview>>
{
    public async Task<Response<DocumentRegisterImportPreview>> Handle(
        DryRunDocumentRegisterImportCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(tenantContext);
        var preview = await previewService.BuildAsync(request.FileName, request.CsvContent, tenantId, ct);
        return Response<DocumentRegisterImportPreview>.Success(preview, 200, request.CorrelationId);
    }
}

/// <summary>
/// WP-DM-DCP005-REGISTER-IMPORT-UI-01 — Step 2: commit. Content-hash idempotency FIRST (both checks read-only,
/// neither writes), then the actual write is delegated to <see cref="IngestDocumentMasterRegisterCommand"/> —
/// its upsert logic is not duplicated here, only wrapped with the audit trail and the batch record.
///
/// <para>⚠ ORDER MATTERS. The hash-changed check runs before the already-applied check: a caller whose file
/// changed since their dry-run gets told THAT first, because "your file changed" and "this file was already
/// loaded" are different corrections and the first one is what actually happened to them.</para>
/// </summary>
public sealed class CommitDocumentRegisterImportHandler(
    IMediator mediator,
    IDocumentRegisterImportBatchRepository batches,
    ITenantContext tenantContext,
    ICurrentUserContext currentUser)
    : IRequestHandler<CommitDocumentRegisterImportCommand, Response<DocumentRegisterImportCommitResult>>
{
    public async Task<Response<DocumentRegisterImportCommitResult>> Handle(
        CommitDocumentRegisterImportCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(tenantContext);
        // The actor is the signed-in caller, resolved server-side — never accepted from the request body.
        var actor = currentUser.ActorName;

        var actualHash = DocumentReferenceListParser.HashContent(request.CsvContent);
        if (!string.Equals(actualHash, request.ExpectedContentHash, StringComparison.Ordinal))
        {
            return Response<DocumentRegisterImportCommitResult>.Fail(
                "The file changed since it was previewed. Preview it again before committing.",
                409, MasterRegisterReasonCodes.ImportContentChanged, request.CorrelationId);
        }

        var already = await batches.FindByContentHashAsync(actualHash, ct);
        if (already is not null)
        {
            return Response<DocumentRegisterImportCommitResult>.Fail(
                $"This exact file was already imported on {already.AppliedAt:O} by {already.Actor}.",
                409, MasterRegisterReasonCodes.ImportAlreadyApplied, request.CorrelationId);
        }

        // The actual write — the SAME command/handler a future direct caller would use, so the two paths cannot
        // disagree about what "imported" means (DM-0 mapping, CitableByQualityDecision, idempotent upsert).
        var ingest = await mediator.Send(
            new IngestDocumentMasterRegisterCommand(request.CsvContent, actor, request.CorrelationId), ct);
        if (!ingest.IsSuccessful)
        {
            return Response<DocumentRegisterImportCommitResult>.Fail(
                ingest.Errors, ingest.StatusCode, ingest.ReasonCode, request.CorrelationId);
        }

        var batch = new DocumentRegisterImportBatch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContentHash = actualHash,
            FileName = request.FileName,
            Actor = actor,
            AppliedAt = DateTimeOffset.UtcNow,
            TotalRows = ingest.Data!.TotalRows,
            Created = ingest.Data.Created,
            Updated = ingest.Data.Updated,
            Unchanged = ingest.Data.Unchanged,
            Blocked = ingest.Data.Blocked,
            CorrelationId = request.CorrelationId,
            CreatedBy = actor
        };
        await batches.CreateAsync(batch, ct);

        return Response<DocumentRegisterImportCommitResult>.Success(
            new DocumentRegisterImportCommitResult(
                batch.Id, batch.ContentHash, batch.AppliedAt,
                batch.TotalRows, batch.Created, batch.Updated, batch.Unchanged, batch.Blocked, ingest.Data.Errors),
            201, request.CorrelationId);
    }
}

/// <summary>WP-DM-DCP005-REGISTER-IMPORT-UI-01 — "Import history": every committed batch, newest first.</summary>
public sealed class GetDocumentRegisterImportHistoryHandler(IDocumentRegisterImportBatchRepository batches)
    : IRequestHandler<GetDocumentRegisterImportHistoryQuery, Response<IReadOnlyList<DocumentRegisterImportBatchDto>>>
{
    public async Task<Response<IReadOnlyList<DocumentRegisterImportBatchDto>>> Handle(
        GetDocumentRegisterImportHistoryQuery request, CancellationToken ct)
    {
        var rows = await batches.ListAsync(ct);
        return Response<IReadOnlyList<DocumentRegisterImportBatchDto>>.Success(
            rows.Select(b => new DocumentRegisterImportBatchDto(
                b.Id, b.FileName, b.ContentHash, b.Actor, b.AppliedAt, b.TotalRows, b.Created, b.Updated,
                b.Unchanged, b.Blocked))
                .ToList(),
            200, request.CorrelationId);
    }
}
