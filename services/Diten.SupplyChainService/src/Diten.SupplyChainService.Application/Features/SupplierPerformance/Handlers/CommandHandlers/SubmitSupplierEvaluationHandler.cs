using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Commands;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Handlers.CommandHandlers;

/// <summary>
/// MOD-0147 §0.5 stages 5, 6 and 8 for submitSupplierEvaluation (stages 1–3b ran in the context middleware, 4 in the validator).
/// 5 receipt + fingerprint → 6 target, If-Match, lifecycle → 8 one transaction. No stage 7: the evaluation records a past period,
/// so eligibility is not read again. A clash or a lost version race re-runs stage 5 first (review F-2).
/// </summary>
public sealed class SubmitSupplierEvaluationHandler(IEvaluationRepository repository, SupplierPerformanceRequestContext context, TimeProvider clock)
    : IRequestHandler<SubmitSupplierEvaluationCommand, Response<EvaluationSubmissionResult>>
{
    private static readonly JsonSerializerOptions Snapshot = new(JsonSerializerDefaults.Web);

    public async Task<Response<EvaluationSubmissionResult>> Handle(SubmitSupplierEvaluationCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = context.Scope;
        var input = SupplierPerformanceWire.ParseSubmit(request.Body);
        var fingerprint = SupplierPerformanceRequestFingerprint.Submit(input);
        try
        {
            // Stage 5.
            if (await repository.FindSubmitReceiptAsync(scope, request.EvaluationId, context.IdempotencyKey, ct) is { } receipt) return Replay(receipt, fingerprint);

            // Stage 6.
            if (await repository.GetAsync(scope, request.EvaluationId, ct) is not { } evaluation)
                return Response<EvaluationSubmissionResult>.Fail(SupplierPerformanceErrorCodes.UnknownEvaluation, 404);
            if (evaluation.Version != context.ExpectedVersion || evaluation.Status != EvaluationStatus.DRAFT)
            {
                // An identical request may have committed since stage 5 ran: it is a replay, not a refusal.
                if (await repository.FindSubmitReceiptAsync(scope, request.EvaluationId, context.IdempotencyKey, ct, afterClash: true) is { } late) return Replay(late, fingerprint);
                return evaluation.Version != context.ExpectedVersion
                    ? VersionConflict(evaluation.Version)
                    : Response<EvaluationSubmissionResult>.Fail(SupplierPerformanceErrorCodes.BusinessRuleViolation, 422);
            }

            // Stage 8.
            var scorecardId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var publishedAt = clock.GetUtcNow();
            var version = evaluation.Version + 1;
            var result = new EvaluationSubmissionResult(evaluation.Id.ToString("D"), scorecardId.ToString("D"), nameof(EvaluationStatus.PUBLISHED), version, context.CorrelationId);
            var submission = new EvaluationSubmission(evaluation.Id, evaluation.Version, scorecardId, publishedAt, input.SubmittedAt, input.Comment, context.CorrelationId, eventId,
                SupplierEvaluationPublishedEvent.Envelope(eventId, evaluation, scorecardId, publishedAt, context.CorrelationId));
            switch (await repository.SubmitAsync(scope, context.IdempotencyKey, fingerprint, submission, JsonSerializer.Serialize(result, Snapshot), ct))
            {
                case EvaluationCommitOutcome.Committed:
                    return Response<EvaluationSubmissionResult>.Success(result, 200);
                case EvaluationCommitOutcome.Clash:
                case EvaluationCommitOutcome.Conflict:
                    return await Lost(request, scope, fingerprint, ct);
                default:
                    // The repository could not prove the commit: never write again here; the client retries with the same key.
                    return Response<EvaluationSubmissionResult>.Fail(SupplierPerformanceErrorCodes.PersistenceUnavailable, 503);
            }
        }
        catch (SupplierPerformanceFailure failure)
        {
            return Response<EvaluationSubmissionResult>.Fail(failure.Code, failure.Status);
        }
    }

    // §0.5 concurrency: equal receipt → replay, other receipt → 409 reused; no receipt for this key: another key won the version.
    private async Task<Response<EvaluationSubmissionResult>> Lost(SubmitSupplierEvaluationCommand request, SupplierPerformanceScope scope, string fingerprint, CancellationToken ct)
    {
        if (await repository.FindSubmitReceiptAsync(scope, request.EvaluationId, context.IdempotencyKey, ct, afterClash: true) is { } winner) return Replay(winner, fingerprint);
        return await repository.GetAsync(scope, request.EvaluationId, ct) is { } current
            ? VersionConflict(current.Version)
            : Response<EvaluationSubmissionResult>.Fail(SupplierPerformanceErrorCodes.UnknownEvaluation, 404);
    }

    private static Response<EvaluationSubmissionResult> VersionConflict(int currentVersion) =>
        Response<EvaluationSubmissionResult>.FailWith(SupplierPerformanceErrorCodes.VersionConflict, 409, new JsonObject { ["currentVersion"] = currentVersion });

    // Equality on the stored representation, never on a hash. A replay answers 200 with the original body (§0.5).
    private static Response<EvaluationSubmissionResult> Replay(EvaluationReceipt receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? Response<EvaluationSubmissionResult>.Success(JsonSerializer.Deserialize<EvaluationSubmissionResult>(receipt.Body, Snapshot)!, 200)
            : Response<EvaluationSubmissionResult>.Fail(SupplierPerformanceErrorCodes.IdempotencyKeyReused, 409);
}
