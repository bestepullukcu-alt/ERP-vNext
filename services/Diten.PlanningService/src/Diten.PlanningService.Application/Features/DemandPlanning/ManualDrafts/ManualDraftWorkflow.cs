using Diten.PlanningService.Application.Features.DemandPlanning.Cycles;
using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

// Application boundary shared by thin CQRS handlers. All reference and data-scope
// evidence is obtained server-side on every request; the production adapter fails closed.
public sealed class ManualDraftWorkflow(
    IManualDraftAuthority authority, IPlanningCycleStore cycles,
    IManualDraftStore drafts)
{
    public async Task<Response<ManualDraftView>> CreateAsync(
        Guid tenantId, Guid actorId, Guid selectedLegalEntityHint, bool hasPermission,
        Guid cycleId, string reason, IReadOnlyList<ManualDraftSeriesInput>? series,
        string requestKey, CancellationToken cancellationToken)
    {
        if (!hasPermission || tenantId == Guid.Empty || actorId == Guid.Empty)
            return Fail("Draft creation permission and server scope are required.", 403);
        if (cycleId == Guid.Empty || !ValidKey(requestKey) ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 2_000 ||
            series is null || series.Count == 0 ||
            series.Any(x => x.SkuId == Guid.Empty || string.IsNullOrWhiteSpace(x.WarehouseId) ||
                x.Weeks is null || x.Weeks.Count != 52 ||
                x.Weeks.Select(w => w.Number).Distinct().Count() != 52 ||
                x.Weeks.Any(w => w.Number is < 1 or > 52 ||
                    !DemandRevisionDraft.IsValidValue(w.ValueKind, w.Quantity))) ||
            series.Select(x => (x.SkuId, x.WarehouseId)).Distinct().Count() != series.Count)
            return Fail("Cycle, reason, idempotency key or 52-week series is invalid.", 422);

        var legalEntityId = await ResolveAsync(tenantId, actorId,
            selectedLegalEntityHint, cancellationToken);
        if (legalEntityId is null) return Unavailable();
        try
        {
            var cycle = await cycles.ReadAsync(tenantId, legalEntityId.Value,
                cycleId, cancellationToken);
            if (cycle is null) return Fail("Planning cycle was not found.", 404);
            var keys = series.Select(x => new DraftSeriesKey(x.SkuId, x.WarehouseId)).ToArray();
            var references = await authority.VerifyCreateSeriesAsync(tenantId,
                actorId, legalEntityId.Value, keys, cancellationToken);
            if (references is null || references.Count != keys.Length ||
                references.Select(x => (x.SkuId, x.WarehouseId)).Distinct().Count() != keys.Length ||
                references.Any(x => x.SkuId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(x.WarehouseId) ||
                    string.IsNullOrWhiteSpace(x.BaseUomId)) ||
                keys.Any(key => !references.Any(x => x.SkuId == key.SkuId &&
                    x.WarehouseId == key.WarehouseId)))
                return Unavailable();

            var verified = series.Select(input =>
            {
                var reference = references.Single(x => x.SkuId == input.SkuId &&
                    x.WarehouseId == input.WarehouseId);
                return new VerifiedDraftSeries(input.SkuId, input.WarehouseId,
                    reference.BaseUomId, input.Weeks.Select(value =>
                    {
                        var week = cycle.Weeks[value.Number - 1];
                        return new ManualDraftWeekInput(value.Number, week.WeekStart,
                            week.WeekEnd, value.ValueKind, value.Quantity);
                    }).ToArray());
            }).OrderBy(x => x.SkuId).ThenBy(x => x.WarehouseId).ToArray();
            var created = ManualDraftFactory.Create(cycle, tenantId,
                legalEntityId.Value, actorId, reason, DateTimeOffset.UtcNow, verified);
            if (!created.IsSuccessful || created.Data is null)
                return Fail(created.Errors.FirstOrDefault() ?? "Draft is invalid.",
                    created.StatusCode);
            var persisted = await drafts.CreateAsync(created.Data,
                requestKey.Trim(), cancellationToken);
            if (persisted.Outcome is ManualDraftStoreOutcome.Created or
                ManualDraftStoreOutcome.Replayed && persisted.Draft is not null)
                return Response<ManualDraftView>.Success(
                    ManualDraftViewMapper.Map(persisted.Draft,
                        persisted.Outcome == ManualDraftStoreOutcome.Replayed),
                    persisted.Outcome == ManualDraftStoreOutcome.Created ? 201 : 200);
            return Outcome(persisted.Outcome);
        }
        catch (Exception ex) when (CatchServiceFailure(ex, cancellationToken))
        {
            return Unavailable();
        }
    }

    public async Task<Response<ManualDraftView>> ReadAsync(
        Guid tenantId, Guid actorId, Guid selectedLegalEntityHint, bool hasPermission,
        Guid revisionId, CancellationToken cancellationToken)
    {
        var result = await ScopedReadAsync(tenantId, actorId, selectedLegalEntityHint,
            hasPermission, revisionId, cancellationToken);
        return result.Error ?? Response<ManualDraftView>.Success(
            ManualDraftViewMapper.Map(result.Draft!));
    }

    public async Task<Response<ManualDraftView>> EditWeekAsync(
        Guid tenantId, Guid actorId, Guid selectedLegalEntityHint, bool hasPermission,
        Guid revisionId, Guid skuId, string warehouseId, int weekNumber,
        DraftWeekValueKind kind, decimal? quantity, string reason,
        string requestKey, int expectedContentVersion, CancellationToken cancellationToken)
    {
        if (!ValidKey(requestKey) || string.IsNullOrWhiteSpace(reason) ||
            reason.Length > 2_000 || skuId == Guid.Empty ||
            string.IsNullOrWhiteSpace(warehouseId) || weekNumber is < 1 or > 52 ||
            expectedContentVersion < 0 || !DemandRevisionDraft.IsValidValue(kind, quantity))
            return Fail("Manual edit or expected content version is invalid.", 422);
        var scoped = await ScopedReadAsync(tenantId, actorId, selectedLegalEntityHint,
            hasPermission, revisionId, cancellationToken);
        if (scoped.Error is not null) return scoped.Error;
        try
        {
            var outcome = await drafts.EditWeekAsync(tenantId,
                scoped.Draft!.LegalEntityId, revisionId, actorId, skuId, warehouseId,
                weekNumber, kind, quantity, reason.Trim(), requestKey.Trim(),
                expectedContentVersion, DateTimeOffset.UtcNow, cancellationToken);
            return await AfterWriteAsync(outcome, tenantId, scoped.Draft.LegalEntityId,
                revisionId, cancellationToken);
        }
        catch (Exception ex) when (CatchServiceFailure(ex, cancellationToken))
        {
            return Unavailable();
        }
    }

    public async Task<Response<ManualDraftView>> TransitionAsync(
        Guid tenantId, Guid actorId, Guid selectedLegalEntityHint, bool hasPermission,
        Guid revisionId, DraftReviewAction action, string? reason, string requestKey,
        int expectedContentVersion, int expectedStateVersion,
        CancellationToken cancellationToken)
    {
        if (!ValidKey(requestKey) || expectedContentVersion < 0 ||
            expectedStateVersion < 0 ||
            action is not (DraftReviewAction.Submitted or DraftReviewAction.Approved or
                DraftReviewAction.Rejected or DraftReviewAction.Reopened) ||
            (action != DraftReviewAction.Submitted &&
                (string.IsNullOrWhiteSpace(reason) || reason.Length > 2_000)))
            return Fail("Review action, reason or expected version is invalid.", 422);
        var scoped = await ScopedReadAsync(tenantId, actorId, selectedLegalEntityHint,
            hasPermission, revisionId, cancellationToken);
        if (scoped.Error is not null) return scoped.Error;
        try
        {
            var outcome = await drafts.TransitionReviewAsync(tenantId,
                scoped.Draft!.LegalEntityId, revisionId, actorId, action,
                reason?.Trim(), requestKey.Trim(), expectedContentVersion,
                expectedStateVersion, DateTimeOffset.UtcNow, cancellationToken);
            return await AfterWriteAsync(outcome, tenantId, scoped.Draft.LegalEntityId,
                revisionId, cancellationToken);
        }
        catch (Exception ex) when (CatchServiceFailure(ex, cancellationToken))
        {
            return Unavailable();
        }
    }

    public async Task<Response<ManualDraftView>> ExcludeSeriesAsync(
        Guid tenantId, Guid actorId, Guid selectedLegalEntityHint, bool hasPermission,
        Guid revisionId, Guid skuId, string warehouseId, string reason,
        string requestKey, int expectedContentVersion, int expectedStateVersion,
        CancellationToken cancellationToken)
    {
        if (!ValidKey(requestKey) || string.IsNullOrWhiteSpace(reason) ||
            reason.Length > 2_000 || skuId == Guid.Empty ||
            string.IsNullOrWhiteSpace(warehouseId) ||
            expectedContentVersion < 0 || expectedStateVersion < 0)
            return Fail("Series exclusion, reason or expected version is invalid.", 422);
        var scoped = await ScopedReadAsync(tenantId, actorId, selectedLegalEntityHint,
            hasPermission, revisionId, cancellationToken);
        if (scoped.Error is not null) return scoped.Error;
        try
        {
            var outcome = await drafts.ExcludeSeriesAsync(tenantId,
                scoped.Draft!.LegalEntityId, revisionId, actorId, skuId,
                warehouseId, reason.Trim(), requestKey.Trim(),
                expectedContentVersion, expectedStateVersion,
                DateTimeOffset.UtcNow, cancellationToken);
            return await AfterWriteAsync(outcome, tenantId, scoped.Draft.LegalEntityId,
                revisionId, cancellationToken);
        }
        catch (Exception ex) when (CatchServiceFailure(ex, cancellationToken))
        {
            return Unavailable();
        }
    }

    private async Task<(DemandRevisionDraft? Draft, Response<ManualDraftView>? Error)>
        ScopedReadAsync(Guid tenantId, Guid actorId, Guid hint, bool hasPermission,
            Guid revisionId, CancellationToken cancellationToken)
    {
        if (!hasPermission || tenantId == Guid.Empty || actorId == Guid.Empty)
            return (null, Fail("Demand permission and server scope are required.", 403));
        if (revisionId == Guid.Empty) return (null, Fail("Draft was not found.", 404));
        var legalEntityId = await ResolveAsync(tenantId, actorId, hint,
            cancellationToken);
        if (legalEntityId is null) return (null, Unavailable());
        try
        {
            var draft = await drafts.ReadAsync(tenantId, legalEntityId.Value,
                revisionId, cancellationToken);
            if (draft is null) return (null, Fail("Draft was not found.", 404));
            if (draft.State is not (DemandRevisionState.Draft or
                DemandRevisionState.InReview or DemandRevisionState.Approved))
                return (null, Fail("Draft was not found.", 404));
            var keys = draft.Series.Select(x => new DraftSeriesKey(
                x.SkuId, x.WarehouseId)).ToArray();
            var access = await authority.CanAccessAsync(tenantId, actorId,
                legalEntityId.Value, keys, cancellationToken);
            if (access is null) return (null, Unavailable());
            if (!access.Value) return (null, Fail("Draft was not found.", 404));
            return (draft, null);
        }
        catch (Exception ex) when (CatchServiceFailure(ex, cancellationToken))
        {
            return (null, Unavailable());
        }
    }

    private async Task<Guid?> ResolveAsync(Guid tenantId, Guid actorId,
        Guid hint, CancellationToken cancellationToken)
    {
        if (hint == Guid.Empty) return null;
        try
        {
            var resolved = await authority.ResolveSelectedAsync(tenantId,
                actorId, hint, cancellationToken);
            // A nullable legacy result cannot prove whether denial is same-tenant
            // or cross-tenant. No operation proceeds on that ambiguity.
            return resolved == hint ? resolved : null;
        }
        catch (Exception ex) when (CatchServiceFailure(ex, cancellationToken))
        {
            return null;
        }
    }

    private async Task<Response<ManualDraftView>> AfterWriteAsync(
        ManualDraftStoreResult result, Guid tenantId, Guid legalEntityId,
        Guid revisionId, CancellationToken cancellationToken)
    {
        if (result.Outcome is not (ManualDraftStoreOutcome.Changed or
            ManualDraftStoreOutcome.Replayed)) return Outcome(result.Outcome);
        var draft = result.Draft ?? await drafts.ReadAsync(tenantId, legalEntityId,
            revisionId, cancellationToken);
        return draft is null ? Unavailable() :
            Response<ManualDraftView>.Success(ManualDraftViewMapper.Map(draft,
                result.Outcome == ManualDraftStoreOutcome.Replayed));
    }

    private static Response<ManualDraftView> Outcome(ManualDraftStoreOutcome outcome) =>
        outcome switch
        {
            ManualDraftStoreOutcome.ScopeDenied => Fail("Draft was not found.", 404),
            ManualDraftStoreOutcome.SeparationDenied => Fail(
                "A significant contributor cannot review this revision.", 403),
            ManualDraftStoreOutcome.Invalid => Fail(
                "Draft validation failed; no state or audit changed.", 422),
            _ => Fail("Draft version, candidate slot or idempotency key conflicts.", 409)
        };

    private static bool ValidKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && key.Trim().Length <= 128;
    private static bool CatchServiceFailure(Exception ex, CancellationToken token) =>
        ex is not OperationCanceledException || !token.IsCancellationRequested;
    private static Response<ManualDraftView> Unavailable() =>
        Fail("Authoritative scope, references, draft or mandatory audit are unavailable.", 503);
    private static Response<ManualDraftView> Fail(string message, int status) =>
        Response<ManualDraftView>.Fail(message, status);
}
