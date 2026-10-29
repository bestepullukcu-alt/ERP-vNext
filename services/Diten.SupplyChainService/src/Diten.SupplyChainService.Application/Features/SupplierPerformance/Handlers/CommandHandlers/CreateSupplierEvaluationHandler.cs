using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Commands;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance.Handlers.CommandHandlers;

/// <summary>
/// MOD-0147 §0.5 stages 5–8 for createSupplierEvaluation (stages 1–3b ran in the context middleware, 4 in the validator).
/// 5 receipt + fingerprint → 7 canonical code, Procurement eligibility, active period, policy → 8 one transaction. A replay
/// returns before stage 7, so it never calls Procurement; a stage-8 clash re-runs stage 5 and never calls Procurement again.
/// </summary>
public sealed class CreateSupplierEvaluationHandler(IEvaluationRepository repository, SupplierPerformanceRequestContext context,
    ISupplierEligibilityReader eligibility, TimeProvider clock) : IRequestHandler<CreateSupplierEvaluationCommand, Response<EvaluationResponse>>
{
    private static readonly JsonSerializerOptions Snapshot = new(JsonSerializerDefaults.Web);

    public async Task<Response<EvaluationResponse>> Handle(CreateSupplierEvaluationCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = context.Scope;
        var input = SupplierPerformanceWire.Parse(request.Body);
        var fingerprint = SupplierPerformanceRequestFingerprint.Create(input);
        try
        {
            // Stage 5.
            if (await repository.FindCreateReceiptAsync(scope, context.IdempotencyKey, ct) is { } receipt) return Replay(receipt, fingerprint);

            // Stage 7.
            if (!SupplierPerformanceWire.IsAcceptedSupplierIdentity(input.SupplierId))
                return Response<EvaluationResponse>.FailWith(SupplierPerformanceErrorCodes.BusinessRuleViolation, 422,
                    new JsonObject { ["code"] = SupplierPerformanceErrorCodes.SupplierCodeNotCanonical });
            var supplierId = await eligibility.CanonicalSupplierIdAsync(input.SupplierId, ct);
            // An identical request may have committed since stage 5 ran: re-read the receipt before calling it a duplicate (RP-07).
            if (await repository.ActivePeriodExistsAsync(scope, supplierId, input.PeriodStart, input.PeriodEnd, ct))
                return await Clash(scope, fingerprint, ct);
            SupplierPerformanceWire.Date(input.PeriodStart, out var start);
            SupplierPerformanceWire.Date(input.PeriodEnd, out var end);
            if (!ScorePolicy.IsCalendarMonth(start, end) || ScorePolicy.Score(input.Metrics) is not { } score)
                return Response<EvaluationResponse>.Fail(SupplierPerformanceErrorCodes.BusinessRuleViolation, 422);

            // Stage 8.
            var evaluation = new SupplierEvaluation
            {
                Id = Guid.NewGuid(), TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId, CreatedBy = scope.ActorId,
                CreatedAt = clock.GetUtcNow(), Version = 1, SupplierId = supplierId, PeriodStart = input.PeriodStart, PeriodEnd = input.PeriodEnd,
                Metrics = score.Metrics.ToList(), OverallScore = score.OverallScore, RiskLevel = score.RiskLevel, Status = EvaluationStatus.DRAFT,
                IsActivePeriod = true, PolicyRevision = ScorePolicy.Revision, Notes = input.Notes, CorrelationId = context.CorrelationId
            };
            var response = EvaluationResponse.From(evaluation);
            switch (await repository.CreateAsync(scope, context.IdempotencyKey, fingerprint, evaluation, JsonSerializer.Serialize(response, Snapshot), ct))
            {
                case EvaluationCommitOutcome.Committed:
                    return Response<EvaluationResponse>.Success(response, 201);
                case EvaluationCommitOutcome.Clash:
                    return await Clash(scope, fingerprint, ct);
                default:
                    // Commit outcome unknown: never write again here; the client retries with the same key.
                    return Response<EvaluationResponse>.Fail(SupplierPerformanceErrorCodes.PersistenceUnavailable, 503);
            }
        }
        catch (SupplierPerformanceFailure failure)
        {
            return failure.DetailCode is null
                ? Response<EvaluationResponse>.Fail(failure.Code, failure.Status)
                : Response<EvaluationResponse>.FailWith(failure.Code, failure.Status, new JsonObject { ["code"] = failure.DetailCode });
        }
    }

    // §0.5 concurrency: on a clash run stage 5 again first — equal receipt → replay, other receipt → 409 reused, none → 409 duplicate.
    private async Task<Response<EvaluationResponse>> Clash(SupplierPerformanceScope scope, string fingerprint, CancellationToken ct) =>
        await repository.FindCreateReceiptAsync(scope, context.IdempotencyKey, ct) is { } winner
            ? Replay(winner, fingerprint)
            : Response<EvaluationResponse>.Fail(SupplierPerformanceErrorCodes.DuplicateEvaluation, 409);

    // Equality on the stored representation, never on a hash. A replay answers 200 with the original body (§0.5).
    private static Response<EvaluationResponse> Replay(EvaluationReceipt receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? Response<EvaluationResponse>.Success(JsonSerializer.Deserialize<EvaluationResponse>(receipt.Body, Snapshot)!, 200)
            : Response<EvaluationResponse>.Fail(SupplierPerformanceErrorCodes.IdempotencyKeyReused, 409);
}
