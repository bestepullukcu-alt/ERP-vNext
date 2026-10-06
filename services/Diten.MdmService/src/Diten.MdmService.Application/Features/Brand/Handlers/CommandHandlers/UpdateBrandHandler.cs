using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.Vocabulary;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Handlers.CommandHandlers;

public sealed class UpdateBrandHandler : IRequestHandler<Commands.UpdateBrandCommand, Response<NoContent>>
{
    private readonly IBrandRepository _repository;

    public UpdateBrandHandler(IBrandRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(Commands.UpdateBrandCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.BrandId, cancellationToken);
        if (entity is null)
        {
            // Cross-tenant reads are already filtered out by the repository, so "not mine" and "not there"
            // both surface as 404 — existence is never leaked across tenants.
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.BrandNotFound, "Brand not found.", 404);
        }

        if (entity.IsArchived)
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.RecordArchived,
                "Archived brands are read-only. Historical references stay intact.", 409);
        }

        var r = request.Request;

        // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 2) — an edit says which read it was made on; one made on an older read than the
        // stored brand is refused (409) and writes nothing, instead of silently overwriting the newer change.
        if (r.ExpectedVersion is not { } expectedVersion || expectedVersion < 0)
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.ExpectedVersionRequired,
                "ExpectedVersion must be a non-negative integer.", 400);
        }

        if (entity.Version != expectedVersion)
        {
            return Stale();
        }

        if (BrandProductVocabulary.IsArchivedStatus(r.BrandStatus))
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.ArchivedStatusNotAssignable,
                "Brand status 'archived' is set by the archive endpoint, not by a write request.", 400);
        }

        if (!BrandProductVocabulary.IsBrandStatus(r.BrandStatus))
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.InvalidBrandStatus,
                $"Unknown brand status '{r.BrandStatus}'.", 400);
        }

        // BrandCode is stable (FU01 §3): a changed code is rejected rather than silently ignored, so a caller
        // never believes a rename succeeded.
        if (!string.Equals(BrandMappings.NormalizeCode(r.BrandCode), entity.BrandCode, StringComparison.Ordinal))
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.CodeImmutable,
                "BrandCode is immutable. Rename the brand through BrandName instead.", 409);
        }

        if (!BrandProductEffectiveWindow.IsValid(r.EffectiveFrom, r.EffectiveTo))
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.InvalidEffectiveWindow,
                "EffectiveTo cannot be earlier than EffectiveFrom.", 400);
        }

        if (BrandProductExternalReferences.Validate(r.ExternalReferences) is { } externalReferenceFailure)
        {
            return BrandProductFailures.Fail<NoContent>(
                externalReferenceFailure,
                "External references must be unique per (SourceSystem, ExternalId) with at most one primary per source system.",
                409);
        }

        var before = BrandAuditSnapshotSource.Copy(entity);
        BrandMappings.Apply(entity, r);
        entity.UpdatedBy = request.Actor;
        // FIX1 (item 10) — where a reference came from is never re-stamped by an edit.
        BrandProductExternalReferences.PreserveImportedAt(entity.ExternalReferences, before.ExternalReferences);

        if (await BrandProductExternalReferences.FirstInUseAsync(entity.ExternalReferences, async (source, externalId) =>
                (await _repository.FindByExternalReferenceAsync(source, externalId, entity.Id, cancellationToken))?.BrandCode)
            is { } inUse)
        {
            return BrandProductFailures.Fail<NoContent>(BrandProductReasonCodes.ExternalReferenceInUse, inUse.Message, inUse.StatusCode);
        }

        var outcome = await _repository.UpdateEditableFieldsWithHistoryAsync(
            entity,
            expectedVersion,
            BrandHistory.Entry(Domain.Entities.BrandHistoryEntry.EventUpdated, request.Actor, request.CorrelationId, before, entity),
            cancellationToken);
        if (outcome == BrandWriteOutcome.Written)
        {
            return Response<NoContent>.SuccessWithoutData(204);
        }

        // The conditional write lost: a brand that is gone (or another tenant's) stays 404; one still there was changed
        // or archived after this edit's read.
        var current = await _repository.GetByIdAsync(request.BrandId, cancellationToken);
        if (current is null)
        {
            return BrandProductFailures.Fail<NoContent>(BrandProductReasonCodes.BrandNotFound, "Brand not found.", 404);
        }

        return current.IsArchived
            ? BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.RecordArchived,
                "Archived brands are read-only. Historical references stay intact.", 409)
            : Stale();
    }

    private static Response<NoContent> Stale() => BrandProductFailures.Fail<NoContent>(
        BrandProductReasonCodes.VersionConflict,
        "The brand has changed since it was loaded. Reload it and apply the edit again.", 409);
}
