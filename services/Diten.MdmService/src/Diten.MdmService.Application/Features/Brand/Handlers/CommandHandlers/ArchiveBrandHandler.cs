using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.Vocabulary;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Handlers.CommandHandlers;

public sealed class ArchiveBrandHandler : IRequestHandler<Commands.ArchiveBrandCommand, Response<NoContent>>
{
    private readonly IBrandRepository _repository;

    public ArchiveBrandHandler(IBrandRepository repository)
    {
        _repository = repository;
    }

    /// <summary>How many times the archive re-reads a brand that an edit changed between the read and the write.</summary>
    private const int MaxAttempts = 3;

    public async Task<Response<NoContent>> Handle(Commands.ArchiveBrandCommand request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var entity = await _repository.GetByIdAsync(request.BrandId, cancellationToken);
            if (entity is null)
            {
                return BrandProductFailures.Fail<NoContent>(
                    BrandProductReasonCodes.BrandNotFound, "Brand not found.", 404);
            }

            // Idempotent: re-archiving an archived brand succeeds instead of 409-ing, so a retried request or a
            // double-clicked button is not reported as a failure — and leaves no second history entry.
            if (entity.IsArchived)
            {
                return Response<NoContent>.SuccessWithoutData(204);
            }

            // NO CASCADE (FU01 §11): products under this brand keep their own lifecycle and stay readable. Only
            // NEW product links are refused from here on, and that refusal is visible (409), never silent.
            // There is no un-archive (owner decision pending): the confirmation dialog says so before this runs.
            var before = BrandAuditSnapshotSource.Copy(entity);
            var expectedVersion = entity.Version;
            entity.IsArchived = true;
            entity.BrandStatus = BrandProductVocabulary.StatusArchived;
            entity.ArchivedAt = DateTimeOffset.UtcNow;
            entity.ArchivedBy = request.Actor;
            entity.UpdatedBy = request.Actor;

            // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 1) — only the archive fields are written, with the history entry, and only
            // on the version read here; an edit that landed in between is re-read (a concurrent archive reads as done).
            var outcome = await _repository.ArchiveWithHistoryAsync(
                entity,
                expectedVersion,
                BrandHistory.Entry(Domain.Entities.BrandHistoryEntry.EventArchived, request.Actor, request.CorrelationId, before, entity),
                cancellationToken);
            if (outcome == BrandWriteOutcome.Written)
            {
                return Response<NoContent>.SuccessWithoutData(204);
            }

            if (outcome == BrandWriteOutcome.Ambiguous)
            {
                return BrandProductFailures.OutcomeUnknown<NoContent>();
            }

            if (outcome == BrandWriteOutcome.Unavailable)
            {
                // FIX2 (items 7, 18) — nothing was archived: an archive without its history entry is never written.
                return BrandProductFailures.StoreUnavailable<NoContent>();
            }

            if (attempt == MaxAttempts)
            {
                return BrandProductFailures.Fail<NoContent>(
                    BrandProductReasonCodes.VersionConflict,
                    "The brand kept changing while it was being archived. Reload it and try again.", 409);
            }
        }
    }
}
