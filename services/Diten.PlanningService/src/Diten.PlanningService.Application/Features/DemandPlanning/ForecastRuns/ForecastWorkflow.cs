using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning.Cycles;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ForecastRuns;

public sealed record ForecastEvaluationView(Guid PlanningCycleId, Guid SkuId,
    string WarehouseId, string PolicyVersion,
    IReadOnlyList<ForecastCandidateView> Candidates);
public sealed record ForecastCreateView(Guid ForecastRunId, string Method,
    string PolicyVersion, ManualDraftView Draft, bool Replayed);

public sealed class ForecastWorkflow(IManualDraftAuthority authority,
    IPlanningCycleStore cycles, AcceptedHistoryForecastEvaluation evaluation,
    IForecastDraftStore store, TimeProvider clock)
{
    public async Task<Response<ForecastEvaluationView>> EvaluateAsync(Guid tenantId,
        Guid actorId, Guid companyHint, bool canCreateRun, Guid cycleId,
        Guid skuId, string warehouseId, CancellationToken cancellationToken)
    {
        var scoped = await ScopeAsync(tenantId, actorId, companyHint,
            canCreateRun, cycleId, skuId, warehouseId, cancellationToken);
        if (scoped.Error is not null)
            return Response<ForecastEvaluationView>.Fail(scoped.Error, scoped.Status);
        try
        {
            var candidates = await evaluation.EvaluateAsync(scoped.Cycle!,
                skuId, scoped.Reference!.SkuLevel!, warehouseId, cancellationToken);
            return Response<ForecastEvaluationView>.Success(new(cycleId,
                skuId, warehouseId, ApprovedForecastPolicy.Version,
                candidates.Select(x => x.View).ToArray()));
        }
        catch (Exception ex) when (ServiceFailure(ex, cancellationToken))
        {
            return Response<ForecastEvaluationView>.Fail(
                "Accepted history or policy could not be verified.", 503);
        }
    }

    public async Task<Response<ForecastCreateView>> CreateAsync(Guid tenantId,
        Guid actorId, Guid companyHint, bool canCreateRun, bool canCreateDraft,
        Guid cycleId, Guid skuId, string warehouseId, string methodName,
        string reason, int expectedCycleVersion, string requestKey,
        CancellationToken cancellationToken)
    {
        if (!canCreateDraft)
            return Response<ForecastCreateView>.Fail("Draft creation permission is required.", 403);
        if (!Enum.TryParse<ForecastMethodEligibility.Method>(methodName, false, out var method) ||
            !Enum.IsDefined(method) || string.IsNullOrWhiteSpace(reason) ||
            reason.Length > 2_000 || string.IsNullOrWhiteSpace(requestKey) ||
            requestKey.Length > 200 || expectedCycleVersion < 0)
            return Response<ForecastCreateView>.Fail("Method, reason, version or request key is invalid.", 422);
        var scoped = await ScopeAsync(tenantId, actorId, companyHint,
            canCreateRun, cycleId, skuId, warehouseId, cancellationToken);
        if (scoped.Error is not null)
            return Response<ForecastCreateView>.Fail(scoped.Error, scoped.Status);
        var cycle = scoped.Cycle!;
        var normalizedReason = reason.Trim();
        var normalizedKey = requestKey.Trim();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { tenantId, legalEntityId = cycle.LegalEntityId,
                cycleId, actorId, skuId, warehouseId, methodName,
                reason = normalizedReason, expectedCycleVersion }))));
        try
        {
            // Reauthorize first, then replay before re-reading mutable accepted history.
            var previous = await store.FindByRequestAsync(tenantId, cycle.LegalEntityId,
                cycleId, actorId, normalizedKey, cancellationToken);
            if (previous is not null)
            {
                if (previous.Run.RequestFingerprint != fingerprint)
                    return Response<ForecastCreateView>.Fail("Request key has different content.", 409);
                return Response<ForecastCreateView>.Success(new(previous.Run.Id,
                    previous.Run.Method, previous.Run.PolicyVersion,
                    ManualDraftViewMapper.Map(previous.Draft, true), true), 200);
            }
            if (cycle.Version != expectedCycleVersion)
                return Response<ForecastCreateView>.Fail("Planning cycle version changed.", 409);
            var candidates = await evaluation.EvaluateAsync(cycle, skuId,
                scoped.Reference!.SkuLevel!, warehouseId, cancellationToken);
            var chosen = candidates.Single(x => x.View.Method == methodName);
            if (!chosen.View.Eligible || chosen.Result is null || chosen.History is null)
                return Response<ForecastCreateView>.Fail(
                    "Selected method is ineligible: " + chosen.View.Reason, 422);
            var readiness = AcceptedHistoryForecastReadiness.Assess(chosen.History);
            if (readiness.Route != AcceptedHistoryRoute.ForecastCandidate ||
                string.IsNullOrWhiteSpace(readiness.UomId) ||
                !readiness.UnitVerificationPending &&
                    readiness.UomId != scoped.Reference.BaseUomId)
                return Response<ForecastCreateView>.Fail(
                    "Accepted history unit or reference cannot be verified.", 503);
            var now = clock.GetUtcNow();
            var runId = Guid.NewGuid();
            var spec = ApprovedForecastPolicy.Specification(method);
            var origin = new MethodWeekProvenance(runId, methodName,
                spec?.Variant ?? (method == ForecastMethodEligibility.Method.SeasonalNaive ?
                    "annual-lag-52" : "last-value"), ApprovedForecastPolicy.Version,
                chosen.History.Weeks[^1].WeekStart,
                AcceptedHistoryForecastDraftBuilder.Fingerprint(chosen.History),
                chosen.Result.Parameters, readiness.UnitVerificationPending);
            var input = new VerifiedDraftSeries(skuId, warehouseId, readiness.UomId!,
                cycle.Weeks.Select((week, index) => new ManualDraftWeekInput(
                    week.Number, week.WeekStart, week.WeekEnd,
                    DraftWeekValueKind.Known, chosen.Result.Forecast52[index])).ToArray(),
                origin);
            var created = ManualDraftFactory.Create(cycle, tenantId,
                cycle.LegalEntityId, actorId, normalizedReason, now, [input]);
            if (!created.IsSuccessful || created.Data is null)
                return Response<ForecastCreateView>.Fail("Forecast Draft is invalid.", 422);
            var run = new ForecastRun
            {
                Id = runId, TenantId = tenantId, LegalEntityId = cycle.LegalEntityId,
                PlanningCycleId = cycleId, RevisionId = created.Data.Id,
                SkuId = skuId, WarehouseId = warehouseId, BaseUomId = readiness.UomId!,
                Method = methodName, Variant = origin.Variant,
                PolicyVersion = ApprovedForecastPolicy.Version,
                HistoryAsOfWeekStart = origin.HistoryAsOfWeekStart,
                HistoryContentSha256 = origin.HistoryContentSha256,
                UnitVerificationPending = origin.UnitVerificationPending,
                Parameters = chosen.Result.Parameters.ToDictionary(x => x.Key, x => x.Value),
                WapeRatio = chosen.View.WapeRatio, Mae = chosen.View.Mae,
                Bias = chosen.View.Bias, Mase = chosen.View.Mase,
                ByWeeksAhead = chosen.View.ByWeeksAhead.Select(x =>
                    new ForecastHorizonSummary(x.WeeksAhead, x.Count, x.Mae,
                        x.WapeRatio, x.Bias, x.Mase)).ToList(),
                SelectedBy = actorId, SelectedAt = now, CreatedAt = now,
                Reason = normalizedReason, RequestKey = normalizedKey,
                RequestFingerprint = fingerprint
            };
            var audit = new ForecastSelectionAudit
            {
                TenantId = tenantId, LegalEntityId = cycle.LegalEntityId,
                PlanningCycleId = cycleId, RevisionId = created.Data.Id,
                ForecastRunId = runId, SkuId = skuId, WarehouseId = warehouseId,
                PreviousMethod = null, NewMethod = methodName,
                PolicyVersion = ApprovedForecastPolicy.Version,
                Reason = normalizedReason, ActorId = actorId,
                OccurredAt = now, CreatedAt = now,
                RequestKey = normalizedKey, RequestFingerprint = fingerprint
            };
            var result = await store.CreateForecastAsync(created.Data, normalizedKey,
                run, audit, cancellationToken);
            if (result.Outcome is ManualDraftStoreOutcome.Created && result.Draft is not null)
                return Response<ForecastCreateView>.Success(new(runId, methodName,
                    ApprovedForecastPolicy.Version,
                    ManualDraftViewMapper.Map(result.Draft), false), 201);
            if (result.Outcome is ManualDraftStoreOutcome.Replayed)
            {
                var replay = await store.FindByRequestAsync(tenantId,
                    cycle.LegalEntityId, cycleId, actorId, normalizedKey, cancellationToken);
                if (replay is not null && replay.Run.RequestFingerprint == fingerprint)
                    return Response<ForecastCreateView>.Success(new(replay.Run.Id,
                        replay.Run.Method, replay.Run.PolicyVersion,
                        ManualDraftViewMapper.Map(replay.Draft, true), true), 200);
            }
            return Response<ForecastCreateView>.Fail("Forecast request conflicts.", 409);
        }
        catch (Exception ex) when (ServiceFailure(ex, cancellationToken))
        {
            return Response<ForecastCreateView>.Fail(
                "Forecast history, Draft or mandatory audit is unavailable.", 503);
        }
    }

    private async Task<(PlanningCycle? Cycle, VerifiedDraftSeriesReference? Reference,
        string? Error, int Status)> ScopeAsync(Guid tenantId, Guid actorId, Guid hint,
        bool canCreateRun, Guid cycleId, Guid skuId, string warehouseId,
        CancellationToken cancellationToken)
    {
        if (!canCreateRun || tenantId == Guid.Empty || actorId == Guid.Empty)
            return (null, null, "Forecast run permission and server scope are required.", 403);
        if (hint == Guid.Empty || cycleId == Guid.Empty || skuId == Guid.Empty ||
            string.IsNullOrWhiteSpace(warehouseId))
            return (null, null, "Cycle and series are required.", 422);
        try
        {
            var company = await authority.ResolveSelectedAsync(tenantId,
                actorId, hint, cancellationToken);
            if (company != hint) return (null, null, "Company authority is unavailable.", 503);
            var cycle = await cycles.ReadAsync(tenantId, company.Value,
                cycleId, cancellationToken);
            if (cycle is null || cycle.IsDeleted)
                return (null, null, "Planning cycle was not found.", 404);
            var keys = new[] { new DraftSeriesKey(skuId, warehouseId) };
            var refs = await authority.VerifyCreateSeriesAsync(tenantId,
                actorId, company.Value, keys, cancellationToken);
            var access = await authority.CanAccessAsync(tenantId, actorId,
                company.Value, keys, cancellationToken);
            if (access is false) return (null, null, "Series was not found.", 404);
            if (access is not true || refs is null || refs.Count != 1 ||
                refs[0].SkuId != skuId || refs[0].WarehouseId != warehouseId ||
                string.IsNullOrWhiteSpace(refs[0].BaseUomId) ||
                refs[0].SkuLevel is not ("Gsku" or "Lsku" or "FinishedGood"))
                return (null, null, "Series reference is unavailable.", 503);
            return (cycle, refs[0], null, 200);
        }
        catch (Exception ex) when (ServiceFailure(ex, cancellationToken))
        {
            return (null, null, "Company or series authority is unavailable.", 503);
        }
    }

    private static bool ServiceFailure(Exception ex, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && ex is not OperationCanceledException;
}
