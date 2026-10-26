using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Application.Features.Recall.Trace;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Recall.Trace;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Drills;

/// <summary>
/// startDrill (R5, pack §8.10 "Başlat") — the server opens a <c>Running</c> run: <c>StartedAtUtc</c> = the SERVER clock (no time member is
/// accepted from the body), <c>TargetHours</c> = the company settings AT START. The lot: Manual = the user's lot (404 <c>UNKNOWN_LOT</c>);
/// Auto = a pick by the injected randomness from the plan's candidate set (422 <c>RECALL_DRILL_NO_CANDIDATE</c> when empty; the pick index and
/// the candidate count are audited). One running run per company — checked first, then held by the partial unique index (409
/// <c>RECALL_DRILL_ALREADY_RUNNING</c>). ONE transaction: the number <c>RC-YYYY-DNNN</c>, the run, event <c>Started</c>, audit
/// <c>recall.drill.started</c>. Nothing else is written (KR-G read-only).
/// </summary>
public sealed class StartRecallDrillHandler : IRequestHandler<StartRecallDrillCommand, Response<RecallDrillRunDto>>
{
    private readonly IRecallDrillRepository _drills;
    private readonly IRecallDrillCandidates _candidates;
    private readonly IRecallDrillRandom _random;
    private readonly RecallSupport _support;

    public StartRecallDrillHandler(IRecallDrillRepository drills, IRecallDrillCandidates candidates, IRecallDrillRandom random, RecallSupport support)
    {
        _drills = drills;
        _candidates = candidates;
        _random = random;
        _support = support;
    }

    public async Task<Response<RecallDrillRunDto>> Handle(StartRecallDrillCommand request, CancellationToken ct)
    {
        var fingerprint = RecallSupport.Fingerprint(("planId", request.PlanId.ToString("D")), ("selectionMode", request.SelectionMode.ToString()),
            ("lotId", request.LotId?.ToString("D")));
        if (await ReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var plan = await _drills.FindPlanAsync(request.PlanId, ct);
        if (plan is null) return RecallDrillMapping.PlanNotFound<RecallDrillRunDto>();
        if (!plan.Active) return RecallSupport.Invalid<RecallDrillRunDto>($"Drill plan {plan.Code} is inactive.");
        if (request.SelectionMode == RecallDrillSelectionMode.Manual && request.LotId is null)
            return RecallSupport.Invalid<RecallDrillRunDto>("lotId is required for selectionMode Manual.");
        if (request.SelectionMode == RecallDrillSelectionMode.Auto && request.LotId is not null)
            return RecallSupport.Invalid<RecallDrillRunDto>("lotId is only for selectionMode Manual (Auto: the server picks).");
        if (request.SelectionMode == RecallDrillSelectionMode.Auto && plan.ScopeKind == RecallDrillScopeKind.SelectedLot)
            return RecallSupport.Invalid<RecallDrillRunDto>($"Drill plan {plan.Code} takes a selected lot: use selectionMode Manual.");
        if (await _drills.FindRunningAsync(ct) is { } running) return AlreadyRunning(running.Number);

        var now = _support.Now();
        RecallDrillLot lot;
        int? candidateCount = null;
        int? pick = null;
        if (request.SelectionMode == RecallDrillSelectionMode.Manual)
        {
            if (await _candidates.LotAsync(request.LotId!.Value, ct) is not { } picked)
                return RecallSupport.Fail<RecallDrillRunDto>(RecallErrorCodes.UnknownLot, "Lot not found.", 404);
            lot = picked;
        }
        else
        {
            var candidates = plan.ScopeKind == RecallDrillScopeKind.RawMaterialLot
                ? await _candidates.RawMaterialLotsAsync(ct)
                : await _candidates.FinishedGoodLotsAsync(now.AddDays(-RecallDrillRules.AutoLookbackDays), ct);
            if (candidates.Count == 0)
            {
                return RecallSupport.Fail<RecallDrillRunDto>(RecallErrorCodes.DrillNoCandidate,
                    $"Drill plan {plan.Code} ({plan.ScopeKind}) has no candidate lot.", 422);
            }

            pick = _random.Next(candidates.Count);
            lot = candidates[pick.Value];
            candidateCount = candidates.Count;
        }

        var settings = await _drills.FindSettingsAsync(ct) ?? RecallSettingsRules.Defaults();
        var run = new RecallDrillRun
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Version = 1,
            PlanId = plan.Id,
            PlanCode = plan.Code,
            Status = RecallDrillRunStatus.Running,
            SelectionMode = request.SelectionMode,
            LotId = lot.LotId,
            LotNumber = lot.LotNumber,
            StartedAtUtc = now,
            TargetHours = settings.DrillTargetHours,
            StartedBy = _support.UserId,
            CreateIdempotencyKey = request.IdempotencyKey,
            CreateRequestFingerprint = fingerprint,
            RunningKey = RecallDrillContract.RunningKey,
            CandidateCount = candidateCount
        };
        var selection = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["planCode"] = plan.Code,
            ["lotId"] = lot.LotId,
            ["lotNumber"] = lot.LotNumber,
            ["selectionMode"] = request.SelectionMode.ToString(),
            ["candidateCount"] = RecallDrillMapping.I(candidateCount),
            ["pickIndex"] = RecallDrillMapping.I(pick),
            ["targetHours"] = RecallDrillMapping.I(run.TargetHours),
            ["startedAtUtc"] = RecallSupport.Wire(now)
        };

        try
        {
            await _drills.InTransactionAsync(async (session, c) =>
            {
                run.Number = await session.NextRunNumberAsync(now.Year, c);
                await session.InsertRunAsync(run, c);
                await session.InsertEventAsync(RecallDrillMapping.Event(_support, run.Id, RecallDrillEventKind.Started, now, request.IdempotencyKey, fingerprint, selection), c);
                await session.InsertAuditAsync(_support.Audit(RecallDrillContract.AuditStarted, RecallDrillContract.SubjectRun, run.Id, run.Number, now,
                    request.IdempotencyKey, new Dictionary<string, string?>(selection, StringComparer.Ordinal) { ["fingerprint"] = fingerprint }), c);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException ex)
        {
            if (await ReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } first) return first;
            return ex.Index == RecallDrillIndexes.Running ? AlreadyRunning(null) : RecallSupport.KeyReused<RecallDrillRunDto>();
        }

        return Response<RecallDrillRunDto>.Success(RecallDrillMapping.Run(run), 201);
    }

    private static Response<RecallDrillRunDto> AlreadyRunning(string? number)
        => RecallSupport.Fail<RecallDrillRunDto>(RecallErrorCodes.DrillAlreadyRunning,
            number is null ? "A drill is already running for this company." : $"Drill {number} is already running for this company.", 409);

    private async Task<Response<RecallDrillRunDto>?> ReplayAsync(string key, string fingerprint, CancellationToken ct)
        => await _drills.FindRunByCreateKeyAsync(_support.UserId, key, ct) is { } earlier
            ? earlier.CreateRequestFingerprint == fingerprint
                ? Response<RecallDrillRunDto>.Success(RecallDrillMapping.Run(earlier))
                : RecallSupport.KeyReused<RecallDrillRunDto>()
            : null;
}

/// <summary>
/// completeDrillStep (R5, pack §8.10) — strictly Trace → Scope → CustomerList (409 <c>RECALL_DRILL_STEP_ORDER</c>), If-Match, Running only.
/// <list type="bullet">
/// <item>Trace — the R2 engine (read-only), both directions, the company's full depth / node limits; a snapshot of purpose <c>Drill</c>
/// (insert-only, bound to the run, no case) seals it; <c>GapCount</c> and the summary / customer list come from the SAME trace;</item>
/// <item>Scope — the user reviewed the summary: a step record, NOTHING is written as a case scope;</item>
/// <item>CustomerList — the customers of the snapshot's trace; sets <c>EndedAtUtc</c> = the SERVER clock (timer stop), the duration
/// (<c>ceil</c> minutes) and pass / fail against the target the run STARTED with.</item>
/// </list>
/// ONE transaction: the run (+ the Drill snapshot), event <c>StepCompleted</c>, audit <c>recall.drill.step</c> (+ <c>recall.trace.run</c>).
/// </summary>
public sealed class CompleteRecallDrillStepHandler : IRequestHandler<CompleteRecallDrillStepCommand, Response<RecallDrillRunDto>>
{
    private readonly IRecallDrillRepository _drills;
    private readonly RecallTraceEngine _engine;
    private readonly RecallSupport _support;

    public CompleteRecallDrillStepHandler(IRecallDrillRepository drills, RecallTraceEngine engine, RecallSupport support)
    {
        _drills = drills;
        _engine = engine;
        _support = support;
    }

    public async Task<Response<RecallDrillRunDto>> Handle(CompleteRecallDrillStepCommand request, CancellationToken ct)
    {
        var fingerprint = RecallSupport.Fingerprint(("step", request.Step.ToString()), ("version", RecallDrillMapping.I(request.ExpectedVersion)));
        var run = await _drills.FindRunAsync(request.RunId, ct);
        if (run is null) return RecallDrillMapping.RunNotFound<RecallDrillRunDto>();
        if (await RecallDrillReplay.RunAsync(_drills, _support, run.Id, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (RecallDrillRules.IsClosed(run.Status)) return RecallDrillMapping.Closed<RecallDrillRunDto>(run);
        if (RecallDrillRules.StepOrderRefusal(run, request.Step) is { } order) return RecallSupport.Fail<RecallDrillRunDto>(order);
        if (run.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallDrillRunDto>();

        var now = _support.Now();
        RecallTraceSnapshot? snapshot = null;
        TraceResult? trace = null;
        var detail = new Dictionary<string, string?>(StringComparer.Ordinal) { ["step"] = request.Step.ToString(), ["fingerprint"] = fingerprint };
        switch (request.Step)
        {
            case RecallDrillStep.Trace:
                var settings = await _drills.FindSettingsAsync(ct) ?? RecallSettingsRules.Defaults();
                trace = Guid.TryParseExact(run.LotId, "D", out var root)
                    ? await _engine.RunAsync(new TraceRequest(root, RecallTraceDirection.Both, now, settings.TraceMaxDepth, settings.TraceMaxNodes), now, ct)
                    : null;
                if (trace is null) return RecallSupport.Fail<RecallDrillRunDto>(RecallErrorCodes.UnknownLot, "The drill lot no longer exists for the tenant.", 404);
                snapshot = Snapshot(trace, run, now);
                run.SnapshotId = snapshot.Id;
                run.SnapshotSha256 = trace.Sha256;
                run.GapCount = trace.Summary.GapCount;
                run.Trace = Summary(trace);
                detail["snapshotSha256"] = trace.Sha256;
                detail["nodeCount"] = RecallDrillMapping.I(trace.Nodes.Count);
                detail["gapCount"] = RecallDrillMapping.I(trace.Summary.GapCount);
                break;
            case RecallDrillStep.Scope:
                break; // the review of the summary — a step record only (no case scope, pack §8.10)
            case RecallDrillStep.CustomerList:
                run.EndedAtUtc = now;
                run.DurationMinutes = RecallDrillRules.DurationMinutes(run.StartedAtUtc, now);
                run.Passed = RecallDrillRules.Passed(run.DurationMinutes.Value, run.TargetHours);
                detail["endedAtUtc"] = RecallSupport.Wire(now);
                detail["durationMinutes"] = RecallDrillMapping.I(run.DurationMinutes);
                detail["targetHours"] = RecallDrillMapping.I(run.TargetHours);
                detail["passed"] = run.Passed.Value ? "true" : "false";
                detail["customerCount"] = RecallDrillMapping.I(run.Trace?.Customers.Count ?? 0);
                break;
        }

        run.Steps.Add(new RecallDrillStepRecord { Step = request.Step, CompletedAtUtc = now, CompletedBy = _support.UserId });
        run.Version = request.ExpectedVersion + 1;
        run.UpdatedAt = now;

        bool saved;
        try
        {
            saved = await _drills.InTransactionAsync(async (session, c) =>
            {
                if (!await session.ReplaceRunAsync(run, request.ExpectedVersion, c)) return false;
                if (snapshot is not null) await session.InsertDrillSnapshotAsync(snapshot, c);
                await session.InsertEventAsync(RecallDrillMapping.Event(_support, run.Id, RecallDrillEventKind.StepCompleted, now, request.IdempotencyKey, fingerprint,
                    new Dictionary<string, string?> { ["step"] = request.Step.ToString() }), c);
                await session.InsertAuditAsync(_support.Audit(RecallDrillContract.AuditStep, RecallDrillContract.SubjectRun, run.Id, run.Number, now,
                    request.IdempotencyKey, detail), c);
                if (trace is not null)
                {
                    await session.InsertAuditAsync(_support.Audit(RecallContract.AuditTraceRun, RecallContract.SubjectTrace, run.Id, run.Number, now, null,
                        new Dictionary<string, string?>
                        {
                            ["rootLotId"] = trace.RootLotId,
                            ["direction"] = trace.Direction,
                            ["nodeCount"] = RecallDrillMapping.I(trace.Nodes.Count),
                            ["gapCount"] = RecallDrillMapping.I(trace.Summary.GapCount),
                            ["durationMs"] = trace.DurationMs.ToString(CultureInfo.InvariantCulture),
                            ["truncated"] = trace.Truncated ? "true" : "false",
                            ["purpose"] = RecallSnapshotPurpose.Drill.ToString()
                        }), c);
                }

                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return await RecallDrillReplay.RunAsync(_drills, _support, run.Id, request.IdempotencyKey, fingerprint, ct) ?? RecallSupport.KeyReused<RecallDrillRunDto>();
        }

        return saved ? Response<RecallDrillRunDto>.Success(RecallDrillMapping.Run(run)) : RecallSupport.Conflict<RecallDrillRunDto>();
    }

    private RecallTraceSnapshot Snapshot(TraceResult r, RecallDrillRun run, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        CaseId = Guid.Empty,
        DrillRunId = run.Id,
        Revision = 0,
        Purpose = RecallSnapshotPurpose.Drill,
        RootLotId = r.RootLotId,
        Direction = r.Direction,
        CanonicalVersion = r.CanonicalVersion,
        Sha256 = r.Sha256,
        Canonical = TraceRules.Canonical(r.RootLotId, r.Direction, r.Truncated, r.Nodes, r.Edges),
        NodeKeys = r.Nodes.Select(n => n.NodeKey).ToList(),
        NodeCount = r.Nodes.Count,
        GapCount = r.Summary.GapCount,
        Truncated = r.Truncated,
        ComputedAtUtc = r.ComputedAtUtc,
        DurationMs = r.DurationMs,
        CreatedBy = _support.UserId
    };

    private static RecallDrillTraceSummary Summary(TraceResult r) => new()
    {
        NodeCount = r.Nodes.Count,
        LotCount = r.Nodes.Count(n => TraceRules.IsLotKind(n.Kind)),
        StockCount = r.Nodes.Count(n => n.Kind == TraceNodeKind.Stock),
        ShipmentCount = r.Nodes.Count(n => n.Kind == TraceNodeKind.Shipment),
        Customers = r.Nodes.Where(n => n.Kind == TraceNodeKind.Shipment && !string.IsNullOrEmpty(n.CustomerRef)).Select(n => n.CustomerRef!)
            .Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToList(),
        Truncated = r.Truncated
    };
}

/// <summary>The If-Match replay of a run command: the caller's earlier event with this key — same body → the run as it is now, else 400.</summary>
internal static class RecallDrillReplay
{
    public static async Task<Response<RecallDrillRunDto>?> RunAsync(IRecallDrillRepository drills, RecallSupport support, Guid runId, string key, string fingerprint,
        CancellationToken ct)
    {
        if (await drills.FindEventByKeyAsync(runId, support.UserId, key, ct) is not { } earlier) return null;
        return earlier.RequestFingerprint == fingerprint && await drills.FindRunAsync(runId, ct) is { } current
            ? Response<RecallDrillRunDto>.Success(RecallDrillMapping.Run(current))
            : RecallSupport.KeyReused<RecallDrillRunDto>();
    }
}

/// <summary>
/// prepareDrillResult (R5, pack §8.8 / §8.10) — after the three steps, the result note (10–2000, 400 <c>REASON_REQUIRED</c>) is kept 15
/// minutes as the run's pending result of the caller; the run (subject <c>recall.drill-run</c>, <c>dr-v1</c>) is what MOD-0022 signs. No
/// four-eyes rule (pack §8.8: drill results are signed by the runner). ONE transaction: the run, event <c>ResultPrepared</c>, audit
/// <c>recall.drill.result-prepared</c>. The answer is the RECALL <c>PendingActionResponse</c> (R5-1: <c>caseId</c> carries the run id).
/// </summary>
public sealed class PrepareRecallDrillResultHandler : IRequestHandler<PrepareRecallDrillResultCommand, Response<RecallPendingActionResponseDto>>
{
    private readonly IRecallDrillRepository _drills;
    private readonly IRecallSignature _signature;
    private readonly RecallSupport _support;

    public PrepareRecallDrillResultHandler(IRecallDrillRepository drills, IRecallSignature signature, RecallSupport support)
    {
        _drills = drills;
        _signature = signature;
        _support = support;
    }

    public async Task<Response<RecallPendingActionResponseDto>> Handle(PrepareRecallDrillResultCommand request, CancellationToken ct)
    {
        var note = request.ResultNote?.Trim();
        var fingerprint = RecallSupport.Fingerprint(("resultNote", note), ("version", RecallDrillMapping.I(request.ExpectedVersion)));
        var run = await _drills.FindRunAsync(request.RunId, ct);
        if (run is null) return RecallDrillMapping.RunNotFound<RecallPendingActionResponseDto>();
        if (await ReplayAsync(run.Id, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (RecallDrillRules.Note(note) is { } refusal) return RecallSupport.Fail<RecallPendingActionResponseDto>(refusal);
        if (RecallDrillRules.IsClosed(run.Status)) return RecallDrillMapping.Closed<RecallPendingActionResponseDto>(run);
        if (RecallDrillRules.NextStep(run) is { } next)
            return RecallSupport.InvalidTransition<RecallPendingActionResponseDto>($"Complete the drill steps first (next: {next}).");
        if (run.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallPendingActionResponseDto>();

        var now = _support.Now();
        run.PendingResult = new RecallDrillPendingResult
        {
            Note = note!,
            SetBy = _support.UserId,
            SetAtUtc = now,
            ExpiresAtUtc = now + RecallCaseRules.PendingActionLifetime,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint
        };
        run.Version = request.ExpectedVersion + 1;
        run.UpdatedAt = now;

        bool saved;
        try
        {
            saved = await _drills.InTransactionAsync(async (session, c) =>
            {
                if (!await session.ReplaceRunAsync(run, request.ExpectedVersion, c)) return false;
                await session.InsertEventAsync(RecallDrillMapping.Event(_support, run.Id, RecallDrillEventKind.ResultPrepared, now, request.IdempotencyKey, fingerprint), c);
                await session.InsertAuditAsync(_support.Audit(RecallDrillContract.AuditResultPrepared, RecallDrillContract.SubjectRun, run.Id, run.Number, now,
                    request.IdempotencyKey, new Dictionary<string, string?>
                    {
                        ["expiresAtUtc"] = RecallSupport.Wire(run.PendingResult.ExpiresAtUtc),
                        ["version"] = RecallDrillMapping.I(run.Version),
                        ["fingerprint"] = fingerprint
                    }), c);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return await ReplayAsync(run.Id, request.IdempotencyKey, fingerprint, ct) ?? RecallSupport.KeyReused<RecallPendingActionResponseDto>();
        }

        return saved ? await PreparedAsync(run, ct) : RecallSupport.Conflict<RecallPendingActionResponseDto>();
    }

    private async Task<Response<RecallPendingActionResponseDto>?> ReplayAsync(Guid runId, string key, string fingerprint, CancellationToken ct)
    {
        if (await _drills.FindEventByKeyAsync(runId, _support.UserId, key, ct) is not { } earlier) return null;
        return earlier.RequestFingerprint == fingerprint && await _drills.FindRunAsync(runId, ct) is { PendingResult: not null } current
            ? await PreparedAsync(current, ct)
            : RecallSupport.KeyReused<RecallPendingActionResponseDto>();
    }

    private async Task<Response<RecallPendingActionResponseDto>> PreparedAsync(RecallDrillRun run, CancellationToken ct)
    {
        var requirement = await _signature.RequirementAsync(RecallDrillContract.ResultAction, run.Id, ct);
        return Response<RecallPendingActionResponseDto>.Success(new RecallPendingActionResponseDto(
            run.Id.ToString("D"), RecallDrillContract.PendingActionName, requirement.Required, RecallDrillContract.ResultAction, requirement.MeaningCode,
            requirement.VisualMode, new RecallSubjectRefDto(RecallContract.ESignModule, RecallDrillContract.SubjectRun, run.Id.ToString("D")),
            RecallSupport.Wire(run.PendingResult!.ExpiresAtUtc), run.Version));
    }
}

/// <summary>
/// signDrillResult (R5) — <c>Completed</c>. The caller's prepared result (same note, unexpired; else 409 <c>RECALL_PENDING_ACTION_MISMATCH</c>);
/// policy required → <c>signatureId</c> is mandatory (400 <c>RECALL_SIGNATURE_REQUIRED</c>); a sent signature is verified with F3-A over the
/// R4 port (current dr-v1 hash recomputed from the stored run, signer = sender, meaning = policy, signed after the preparation, never used
/// before, this run's subject — else 409 <c>RECALL_SIGNATURE_MISMATCH</c>). Not required and none sent → completes unsigned. ONE transaction:
/// the run, event <c>Completed</c> (with the signature id), audit <c>recall.drill.completed</c>.
/// </summary>
public sealed class SignRecallDrillResultHandler : IRequestHandler<SignRecallDrillResultCommand, Response<RecallDrillRunDto>>
{
    private readonly IRecallDrillRepository _drills;
    private readonly IRecallSignature _signature;
    private readonly RecallSupport _support;

    public SignRecallDrillResultHandler(IRecallDrillRepository drills, IRecallSignature signature, RecallSupport support)
    {
        _drills = drills;
        _signature = signature;
        _support = support;
    }

    public async Task<Response<RecallDrillRunDto>> Handle(SignRecallDrillResultCommand request, CancellationToken ct)
    {
        var note = request.ResultNote?.Trim();
        var signatureId = RecallSupport.Trimmed(request.SignatureId);
        var fingerprint = RecallSupport.Fingerprint(("resultNote", note), ("signatureId", signatureId), ("version", RecallDrillMapping.I(request.ExpectedVersion)));
        var run = await _drills.FindRunAsync(request.RunId, ct);
        if (run is null) return RecallDrillMapping.RunNotFound<RecallDrillRunDto>();
        if (await RecallDrillReplay.RunAsync(_drills, _support, run.Id, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (RecallDrillRules.Note(note) is { } refusal) return RecallSupport.Fail<RecallDrillRunDto>(refusal);
        if (RecallDrillRules.IsClosed(run.Status)) return RecallDrillMapping.Closed<RecallDrillRunDto>(run);
        if (RecallDrillRules.NextStep(run) is { } next) return RecallSupport.InvalidTransition<RecallDrillRunDto>($"Complete the drill steps first (next: {next}).");
        if (run.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallDrillRunDto>();

        var now = _support.Now();
        var requirement = await _signature.RequirementAsync(RecallDrillContract.ResultAction, run.Id, ct);
        if (requirement.Required && signatureId is null) return RecallSupport.SignatureRequired<RecallDrillRunDto>();
        var pending = run.PendingResult;
        if (pending is null || pending.SetBy != _support.UserId || pending.ExpiresAtUtc <= now || !string.Equals(pending.Note, note, StringComparison.Ordinal))
        {
            return RecallSupport.PendingMismatch<RecallDrillRunDto>();
        }

        if (signatureId is not null && !await _signature.VerifyAsync(new RecallSignatureVerification(signatureId, RecallDrillContract.ResultAction,
                requirement.MeaningCode, _support.UserId, RecallDrillContract.SubjectRun, run.Id, pending.SetAtUtc), ct))
        {
            return RecallSupport.SignatureMismatch<RecallDrillRunDto>();
        }

        run.Status = RecallDrillRunStatus.Completed;
        run.RunningKey = null;
        run.ResultNote = note;
        run.ResultSignatureId = signatureId;
        run.CompletedBy = _support.UserId;
        run.CompletedAtUtc = now;
        run.PendingResult = null;
        run.Version = request.ExpectedVersion + 1;
        run.UpdatedAt = now;

        bool saved;
        try
        {
            saved = await _drills.InTransactionAsync(async (session, c) =>
            {
                if (!await session.ReplaceRunAsync(run, request.ExpectedVersion, c)) return false;
                await session.InsertEventAsync(RecallDrillMapping.Event(_support, run.Id, RecallDrillEventKind.Completed, now, request.IdempotencyKey, fingerprint,
                    signatureId: signatureId), c);
                await session.InsertAuditAsync(_support.Audit(RecallDrillContract.AuditCompleted, RecallDrillContract.SubjectRun, run.Id, run.Number, now,
                    request.IdempotencyKey, new Dictionary<string, string?>
                    {
                        ["lotId"] = run.LotId,
                        ["selectionMode"] = run.SelectionMode.ToString(),
                        ["durationMinutes"] = RecallDrillMapping.I(run.DurationMinutes),
                        ["targetHours"] = RecallDrillMapping.I(run.TargetHours),
                        ["passed"] = run.Passed is { } p ? (p ? "true" : "false") : null,
                        ["gapCount"] = RecallDrillMapping.I(run.GapCount),
                        ["signatureId"] = signatureId,
                        ["fingerprint"] = fingerprint
                    }), c);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return await RecallDrillReplay.RunAsync(_drills, _support, run.Id, request.IdempotencyKey, fingerprint, ct) ?? RecallSupport.KeyReused<RecallDrillRunDto>();
        }

        return saved ? Response<RecallDrillRunDto>.Success(RecallDrillMapping.Run(run)) : RecallSupport.Conflict<RecallDrillRunDto>();
    }
}

/// <summary>
/// cancelDrill (R5, CT-R17) — "İptal": the run is KEPT as <c>Cancelled</c> with its reason (10–1000, 400 <c>REASON_REQUIRED</c> — R5-3);
/// a Completed or Cancelled run never changes (409 <c>RECALL_INVALID_TRANSITION</c>). ONE transaction: the run, event <c>Cancelled</c>,
/// audit <c>recall.drill.cancelled</c>.
/// </summary>
public sealed class CancelRecallDrillHandler : IRequestHandler<CancelRecallDrillCommand, Response<RecallDrillRunDto>>
{
    private const int MaxReasonLength = 1000;

    private readonly IRecallDrillRepository _drills;
    private readonly RecallSupport _support;

    public CancelRecallDrillHandler(IRecallDrillRepository drills, RecallSupport support)
    {
        _drills = drills;
        _support = support;
    }

    public async Task<Response<RecallDrillRunDto>> Handle(CancelRecallDrillCommand request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        var fingerprint = RecallSupport.Fingerprint(("reason", reason), ("version", RecallDrillMapping.I(request.ExpectedVersion)));
        var run = await _drills.FindRunAsync(request.RunId, ct);
        if (run is null) return RecallDrillMapping.RunNotFound<RecallDrillRunDto>();
        if (await RecallDrillReplay.RunAsync(_drills, _support, run.Id, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (RecallCaseRules.Reason(reason, max: MaxReasonLength) is { } refusal) return RecallSupport.Fail<RecallDrillRunDto>(refusal);
        if (RecallDrillRules.IsClosed(run.Status)) return RecallDrillMapping.Closed<RecallDrillRunDto>(run);
        if (run.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallDrillRunDto>();

        var now = _support.Now();
        run.Status = RecallDrillRunStatus.Cancelled;
        run.RunningKey = null;
        run.CancelReason = reason;
        run.CancelledBy = _support.UserId;
        run.CancelledAtUtc = now;
        run.PendingResult = null;
        run.Version = request.ExpectedVersion + 1;
        run.UpdatedAt = now;

        bool saved;
        try
        {
            saved = await _drills.InTransactionAsync(async (session, c) =>
            {
                if (!await session.ReplaceRunAsync(run, request.ExpectedVersion, c)) return false;
                await session.InsertEventAsync(RecallDrillMapping.Event(_support, run.Id, RecallDrillEventKind.Cancelled, now, request.IdempotencyKey, fingerprint, text: reason), c);
                await session.InsertAuditAsync(_support.Audit(RecallDrillContract.AuditCancelled, RecallDrillContract.SubjectRun, run.Id, run.Number, now,
                    request.IdempotencyKey, new Dictionary<string, string?>
                    {
                        ["lotId"] = run.LotId,
                        ["reason"] = reason,
                        ["stepsDone"] = RecallDrillMapping.I(run.Steps.Count),
                        ["fingerprint"] = fingerprint
                    }), c);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return await RecallDrillReplay.RunAsync(_drills, _support, run.Id, request.IdempotencyKey, fingerprint, ct) ?? RecallSupport.KeyReused<RecallDrillRunDto>();
        }

        return saved ? Response<RecallDrillRunDto>.Success(RecallDrillMapping.Run(run)) : RecallSupport.Conflict<RecallDrillRunDto>();
    }
}

/// <summary>getDrillRun (R5).</summary>
public sealed class GetRecallDrillRunHandler : IRequestHandler<GetRecallDrillRunQuery, Response<RecallDrillRunDto>>
{
    private readonly IRecallDrillRepository _drills;

    public GetRecallDrillRunHandler(IRecallDrillRepository drills) => _drills = drills;

    public async Task<Response<RecallDrillRunDto>> Handle(GetRecallDrillRunQuery request, CancellationToken ct)
        => await _drills.FindRunAsync(request.RunId, ct) is { } run
            ? Response<RecallDrillRunDto>.Success(RecallDrillMapping.Run(run))
            : RecallDrillMapping.RunNotFound<RecallDrillRunDto>();
}

/// <summary>
/// listDrillRuns (R5) — status filter, started-on range, paging (newest first) + the frequency warning (R5-2, pack §8.9): days since the last
/// COMPLETED run &gt; the settings' <c>drillFrequencyWarningDays</c>, or no completed run at all.
/// </summary>
public sealed class ListRecallDrillRunsHandler : IRequestHandler<ListRecallDrillRunsQuery, Response<RecallDrillRunPageDto>>
{
    private readonly IRecallDrillRepository _drills;
    private readonly RecallSupport _support;

    public ListRecallDrillRunsHandler(IRecallDrillRepository drills, RecallSupport support)
    {
        _drills = drills;
        _support = support;
    }

    public async Task<Response<RecallDrillRunPageDto>> Handle(ListRecallDrillRunsQuery request, CancellationToken ct)
    {
        RecallDrillRunStatus? status = null;
        if (request.Status is not null)
        {
            if (!Enum.TryParse<RecallDrillRunStatus>(request.Status, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed)
                || !string.Equals(parsed.ToString(), request.Status, StringComparison.Ordinal))
            {
                return RecallSupport.Invalid<RecallDrillRunPageDto>("status must be Running, Completed or Cancelled.");
            }

            status = parsed;
        }

        if (!TryDate(request.From, out var from) || !TryDate(request.To, out var to))
            return RecallSupport.Invalid<RecallDrillRunPageDto>("from / to must be dates yyyy-MM-dd.");
        if (RecallPaging.Parse(request.Page, request.PageSize) is not { } paging)
            return RecallSupport.Invalid<RecallDrillRunPageDto>($"page must be ≥ 1 and pageSize 1-{RecallContract.MaxPageSize}.");

        var (page, pageSize) = paging;
        var result = await _drills.ListRunsAsync(new RecallDrillRunQuery(status, from, to, page, pageSize), ct);
        var settings = await _drills.FindSettingsAsync(ct) ?? RecallSettingsRules.Defaults();
        var last = await _drills.LastCompletedAsync(ct);
        var (days, warning) = RecallDrillRules.Frequency(last?.CompletedAtUtc, _support.Now(), settings.DrillFrequencyWarningDays);
        return Response<RecallDrillRunPageDto>.Success(new RecallDrillRunPageDto(result.Items.Select(RecallDrillMapping.Run).ToList(), result.Total, page, pageSize,
            new RecallDrillFrequencyWarningDto(warning, days, RecallSupport.Wire(last?.CompletedAtUtc), settings.DrillFrequencyWarningDays)));
    }

    private static bool TryDate(string? raw, out DateOnly? day)
    {
        day = null;
        if (raw is null) return true;
        if (!DateOnly.TryParseExact(raw, RecallSupport.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        day = parsed;
        return true;
    }
}
