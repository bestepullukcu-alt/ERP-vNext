using System.Security.Cryptography;
using System.Text;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed class ReviewHistoryImportBatchHandler :
    IRequestHandler<ReviewHistoryImportBatchCommand, Response<HistoryImportBatchResult>>
{
    private readonly IHistoryImportBatchStore _store;
    private readonly IHistoryImportReviewAuditStore _audit;
    private readonly IHistoryImportScopeAuthority _authority;

    public ReviewHistoryImportBatchHandler(IHistoryImportBatchStore store,
        IHistoryImportReviewAuditStore audit, IHistoryImportScopeAuthority authority)
    { _store = store; _audit = audit; _authority = authority; }

    public async Task<Response<HistoryImportBatchResult>> Handle(
        ReviewHistoryImportBatchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TenantId == Guid.Empty || command.ActorId == Guid.Empty)
            return Response<HistoryImportBatchResult>.Fail("Authenticated tenant and actor are required.", 403);

        var key = command.IdempotencyKey?.Trim() ?? string.Empty;
        var reason = command.Reason?.Trim() ?? string.Empty;
        var fingerprint = Fingerprint(command.ActorId, command.Decision, reason);

        async Task<Response<HistoryImportBatchResult>> FailAsync(string outcome,
            string message, int status, Guid legalEntityId = default)
        {
            try
            {
                await _audit.AppendOnceAsync(new HistoryImportReviewFailure(
                    command.TenantId, legalEntityId, command.BatchId, command.ActorId,
                    key, fingerprint, outcome, reason, DateTimeOffset.UtcNow), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException ||
                                       !cancellationToken.IsCancellationRequested)
            {
                return Response<HistoryImportBatchResult>.Fail("Review audit is unavailable.", 503);
            }
            return Response<HistoryImportBatchResult>.Fail(message, status);
        }

        if (!command.HasReviewPermission)
            return await FailAsync("PermissionDenied", "Review permission is required.", 403);
        if (command.SelectedLegalEntityHint == Guid.Empty)
            return await FailAsync("MissingLegalEntitySelection", "LegalEntity selection is required.", 400);
        if (command.BatchId == Guid.Empty || key.Length is < 1 or > 128 ||
            reason.Length is < 1 or > 2_000 ||
            command.Decision == ImportBatchReviewState.Pending ||
            !Enum.IsDefined(command.Decision))
            return await FailAsync("InvalidReviewRequest", "Review decision, reason or key is invalid.", 400);

        DemandHistoryImportBatch? batch;
        try { batch = await _store.GetForReviewAsync(command.TenantId, command.BatchId, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException ||
                                   !cancellationToken.IsCancellationRequested)
        { return Response<HistoryImportBatchResult>.Fail("Import batch store is unavailable.", 503); }
        if (batch is null)
            return await FailAsync("NotFound", "Batch was not found.", 404);

        Guid? selectedLegalEntityId;
        bool? authorized;
        try
        {
            selectedLegalEntityId = await _authority.ResolveSelectedAsync(command.TenantId,
                command.ActorId, command.SelectedLegalEntityHint, cancellationToken);
            if (selectedLegalEntityId is not null &&
                selectedLegalEntityId != command.SelectedLegalEntityHint)
                return await FailAsync("AssignmentMismatch", "LegalEntity assignment is inconsistent.",
                    503, batch.LegalEntityId);
            if (selectedLegalEntityId != batch.LegalEntityId)
                return await FailAsync("ScopeDenied", "Batch was not found.", 404, batch.LegalEntityId);
            authorized = await _authority.IsAuthorizedAsync(command.TenantId, command.ActorId,
                batch.LegalEntityId, batch.ScopeFrom, batch.ScopeThrough,
                batch.WarehouseScope, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException ||
                                   !cancellationToken.IsCancellationRequested)
        { authorized = null; }
        if (authorized is null)
            return await FailAsync("ScopeUnavailable", "Review scope authority is unavailable.",
                503, batch.LegalEntityId);
        if (authorized is false)
            return await FailAsync("ScopeDenied", "Batch was not found.", 404, batch.LegalEntityId);
        if (command.ActorId == batch.UploadedByActorId)
            return await FailAsync("SelfReviewDenied", "Uploader cannot review own batch.",
                403, batch.LegalEntityId);

        if (batch.ReviewState != ImportBatchReviewState.Pending)
        {
            if (batch.ReviewedByActorId == command.ActorId &&
                batch.ReviewRequestKey == key && batch.ReviewRequestFingerprint == fingerprint)
                return Response<HistoryImportBatchResult>.Success(HistoryImportResultMapper.Map(batch));
            return await FailAsync("DecisionConflict", "Batch already has a review decision.",
                409, batch.LegalEntityId);
        }
        if (command.Decision == ImportBatchReviewState.Approved &&
            !batch.IsEligibleForIndependentReview())
            return await FailAsync("BlockingValidation", "Blocking issue or open quarantine prevents approval.",
                409, batch.LegalEntityId);

        DemandHistoryImportBatch? decided;
        try
        {
            decided = await _store.TryDecideAsync(batch, command.ActorId, command.Decision,
                reason, key, fingerprint, DateTimeOffset.UtcNow, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException ||
                                   !cancellationToken.IsCancellationRequested)
        { return Response<HistoryImportBatchResult>.Fail("Import batch store is unavailable.", 503); }
        if (decided is not null)
            return Response<HistoryImportBatchResult>.Success(HistoryImportResultMapper.Map(decided));

        DemandHistoryImportBatch? current;
        try { current = await _store.GetForReviewAsync(command.TenantId, command.BatchId, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException ||
                                   !cancellationToken.IsCancellationRequested)
        { return Response<HistoryImportBatchResult>.Fail("Import batch store is unavailable.", 503); }
        if (current?.ReviewedByActorId == command.ActorId &&
            current.ReviewRequestKey == key && current.ReviewRequestFingerprint == fingerprint)
            return Response<HistoryImportBatchResult>.Success(HistoryImportResultMapper.Map(current));
        return await FailAsync("DecisionConflict", "Batch changed during review.",
            409, batch.LegalEntityId);
    }

    private static string Fingerprint(Guid actorId, ImportBatchReviewState decision,
        string reason) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{actorId:D}|{decision}|{reason}")));
}
