using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed class AcceptDemandHistoryRowsHandler :
    IRequestHandler<AcceptDemandHistoryRowsCommand, Response<AcceptedDemandHistoryRow>>
{
    private readonly IHistoryImportBatchStore _batches;
    private readonly IHistoryImportScopeAuthority _authority;
    private readonly IHistoryRowReferenceChecker _references;
    private readonly IAcceptedDemandHistoryStore _accepted;

    public AcceptDemandHistoryRowsHandler(IHistoryImportBatchStore batches,
        IHistoryImportScopeAuthority authority, IHistoryRowReferenceChecker references,
        IAcceptedDemandHistoryStore accepted)
    { _batches = batches; _authority = authority; _references = references; _accepted = accepted; }

    public async Task<Response<AcceptedDemandHistoryRow>> Handle(
        AcceptDemandHistoryRowsCommand command, CancellationToken cancellationToken)
    {
        if (command.TenantId == Guid.Empty || command.ActorId == Guid.Empty || !command.HasAcceptPermission)
            return Response<AcceptedDemandHistoryRow>.Fail("Acceptance permission is required.", 403);
        var reason = command.Reason?.Trim() ?? string.Empty;
        var key = command.IdempotencyKey?.Trim() ?? string.Empty;
        if (command.BatchId == Guid.Empty || command.RowNumber < 1 ||
            command.SelectedLegalEntityHint == Guid.Empty || reason.Length is < 1 or > 2_000 ||
            key.Length is < 1 or > 128)
            return Response<AcceptedDemandHistoryRow>.Fail("Acceptance request is invalid.", 400);

        DemandHistoryImportBatch? batch;
        try { batch = await _batches.GetForReviewAsync(command.TenantId, command.BatchId, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        { return Response<AcceptedDemandHistoryRow>.Fail("Import batch store is unavailable.", 503); }
        if (batch is null) return Response<AcceptedDemandHistoryRow>.Fail("Batch was not found.", 404);

        try
        {
            var selected = await _authority.ResolveSelectedAsync(command.TenantId, command.ActorId,
                command.SelectedLegalEntityHint, cancellationToken);
            if (selected is not null && selected != command.SelectedLegalEntityHint)
                return Response<AcceptedDemandHistoryRow>.Fail("LegalEntity assignment is inconsistent.", 503);
            if (selected != batch.LegalEntityId)
                return Response<AcceptedDemandHistoryRow>.Fail("Batch was not found.", 404);
            var authorized = await _authority.IsAuthorizedAsync(command.TenantId, command.ActorId,
                batch.LegalEntityId, batch.ScopeFrom, batch.ScopeThrough,
                batch.WarehouseScope, cancellationToken);
            if (authorized is null)
                return Response<AcceptedDemandHistoryRow>.Fail("Acceptance scope is unavailable.", 503);
            if (!authorized.Value)
                return Response<AcceptedDemandHistoryRow>.Fail("Batch was not found.", 404);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        { return Response<AcceptedDemandHistoryRow>.Fail("Acceptance scope is unavailable.", 503); }

        if (batch.ReviewState != ImportBatchReviewState.Approved ||
            batch.ReviewedByActorId is null || !batch.IsEligibleForAcceptedHistory())
            return Response<AcceptedDemandHistoryRow>.Fail("Batch is not eligible for history acceptance.", 409);
        var candidates = batch.Rows.Where(x => x.RowNumber == command.RowNumber).ToArray();
        if (candidates.Length != 1)
            return Response<AcceptedDemandHistoryRow>.Fail("Row was not found.", 404);
        var source = candidates[0];
        if (source.RecordKind != "Shipment" || source.IsQuarantined ||
            batch.QuarantineCases.Any(x => x.RowNumber == source.RowNumber && x.State == "Open") ||
            source.SkuId is null || source.OccurredOn is null ||
            source.OriginalQuantity is not > 0 ||
            string.IsNullOrWhiteSpace(source.SourceRecordKey) ||
            string.IsNullOrWhiteSpace(source.WarehouseId) ||
            string.IsNullOrWhiteSpace(source.OriginalUomId) ||
            (!source.UnitVerificationPending &&
             (source.BaseQuantity is not > 0 || source.ConversionFactor is not > 0 ||
              string.IsNullOrWhiteSpace(source.BaseUomId) ||
              string.IsNullOrWhiteSpace(source.ConversionContractId) ||
              string.IsNullOrWhiteSpace(source.ConversionContractVersion))))
            return Response<AcceptedDemandHistoryRow>.Fail("Row is not eligible for history acceptance.", 409);

        HistoryReferenceResult reference;
        try
        {
            reference = await _references.CheckAsync(command.TenantId, batch.LegalEntityId,
                source.SkuId.Value, source.SkuLevel, source.WarehouseId,
                source.OccurredOn.Value, source.OriginalUomId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        { return Response<AcceptedDemandHistoryRow>.Fail("Reference authority is unavailable.", 503); }
        if (reference.State == HistoryReferenceState.Unavailable)
            return Response<AcceptedDemandHistoryRow>.Fail("Reference authority is unavailable.", 503);
        if (reference.State != HistoryReferenceState.Verified ||
            reference.UnitVerificationPending != source.UnitVerificationPending ||
            (!source.UnitVerificationPending &&
             (reference.BaseUomId != source.BaseUomId ||
              reference.ConversionFactor != source.ConversionFactor ||
              reference.ConversionNumerator != source.ConversionNumerator ||
              reference.ConversionDenominator != source.ConversionDenominator ||
              reference.ConversionContractId != source.ConversionContractId ||
              reference.ConversionContractVersion != source.ConversionContractVersion)))
            return Response<AcceptedDemandHistoryRow>.Fail("Row reference or conversion changed.", 409);
        if (source.ConversionNumerator is > 0 && source.ConversionDenominator is > 0)
        {
            try
            {
                var recomputed = source.OriginalUomId == source.BaseUomId ?
                    source.OriginalQuantity!.Value :
                    HistoryUnitConversion.ToBaseQuantity(source.OriginalQuantity!.Value,
                        source.ConversionNumerator.Value,
                        source.ConversionDenominator.Value);
                if (recomputed != source.BaseQuantity)
                    return Response<AcceptedDemandHistoryRow>.Fail(
                        "Converted quantity no longer matches its source.", 409);
            }
            catch (OverflowException)
            {
                return Response<AcceptedDemandHistoryRow>.Fail(
                    "Converted quantity cannot be represented.", 409);
            }
        }

        var acceptedAt = DateTimeOffset.UtcNow;
        var row = new AcceptedDemandHistoryRow
        {
            TenantId = batch.TenantId, LegalEntityId = batch.LegalEntityId,
            BatchId = batch.Id, BatchRowNumber = source.RowNumber,
            SourceSystem = batch.SourceSystem, SourceRecordKey = source.SourceRecordKey,
            SourceFileSha256 = batch.SourceFileSha256,
            SkuId = source.SkuId.Value, SkuLevel = source.SkuLevel,
            WarehouseId = source.WarehouseId, OccurredOn = source.OccurredOn.Value,
            OriginalQuantity = source.OriginalQuantity.Value, OriginalUomId = source.OriginalUomId,
            BaseQuantity = source.BaseQuantity ?? 0, BaseUomId = source.BaseUomId ?? string.Empty,
            ConversionFactor = source.ConversionFactor ?? 0,
            ConversionNumerator = source.ConversionNumerator,
            ConversionDenominator = source.ConversionDenominator,
            ConversionContractId = source.ConversionContractId ?? string.Empty,
            ConversionContractVersion = source.ConversionContractVersion ?? string.Empty,
            AcceptedQuantity = source.UnitVerificationPending ? source.OriginalQuantity.Value :
                source.BaseQuantity!.Value,
            AcceptedUomId = source.UnitVerificationPending ? source.OriginalUomId :
                source.BaseUomId!,
            UnitVerificationPending = source.UnitVerificationPending,
            AcceptedByActorId = command.ActorId,
            AcceptedAt = acceptedAt, AcceptanceReason = reason,
            RequestKey = key
        };
        row.ContentSha256 = AcceptedHistoryFingerprint.Compute(row);
        var request = JsonSerializer.Serialize(new
        { command.ActorId, command.BatchId, command.RowNumber, Key = key, Reason = reason, row.ContentSha256 });
        row.RequestFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request)));
        var audit = new DemandHistoryAcceptanceAudit
        {
            TenantId = row.TenantId, LegalEntityId = row.LegalEntityId,
            AcceptedRowId = row.Id, BatchId = row.BatchId,
            BatchRowNumber = row.BatchRowNumber, SourceRecordKey = row.SourceRecordKey,
            ActorId = command.ActorId, OccurredAt = acceptedAt,
            Reason = reason, ContentSha256 = row.ContentSha256
        };
        HistoryAcceptanceResult result;
        try { result = await _accepted.AcceptAsync(batch, row, audit, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        { return Response<AcceptedDemandHistoryRow>.Fail("History acceptance is unavailable.", 503); }
        return result.Outcome switch
        {
            HistoryAcceptanceOutcome.Created => Response<AcceptedDemandHistoryRow>.Success(result.Row!, 201),
            HistoryAcceptanceOutcome.Existing => Response<AcceptedDemandHistoryRow>.Success(result.Row!),
            HistoryAcceptanceOutcome.Ineligible => Response<AcceptedDemandHistoryRow>.Fail("Batch changed during acceptance.", 409),
            HistoryAcceptanceOutcome.Conflict => Response<AcceptedDemandHistoryRow>.Fail("History record conflicts with an earlier acceptance.", 409),
            _ => Response<AcceptedDemandHistoryRow>.Fail("History acceptance is unavailable.", 503)
        };
    }
}
