using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Recall.Trace;

namespace Diten.InventoryService.Application.Features.Recall.Drills;

/// <summary>WP-0177-R5 — the drill names: audit actions (pack §8.12), subject types, the pending action, the single-running key.</summary>
public static class RecallDrillContract
{
    public const string SubjectPlan = "recall.drill-plan";
    public const string SubjectRun = RecallSignableActions.DrillRunSubjectType;
    public const string ResultAction = RecallSignableActions.DrillResult;

    public const string AuditPlanSaved = "recall.drill-plan.saved";
    public const string AuditStarted = "recall.drill.started";
    public const string AuditStep = "recall.drill.step";
    public const string AuditResultPrepared = "recall.drill.result-prepared";
    public const string AuditCompleted = "recall.drill.completed";
    public const string AuditCancelled = "recall.drill.cancelled";

    /// <summary>The PendingActionResponse <c>action</c> of prepareDrillResult (R5-1: the R4-1 reuse, <c>caseId</c> carries the run id).</summary>
    public const string PendingActionName = "SignDrillResult";

    /// <summary><see cref="RecallDrillRun.RunningKey"/> while Running (the partial unique index of one running run per company).</summary>
    public const string RunningKey = "running";
}

/// <summary>The drill wire mapping, the event factory and the refusals of the drill handlers.</summary>
public static class RecallDrillMapping
{
    public static RecallDrillPlanDto Plan(RecallDrillPlan p) => new(
        p.Id.ToString("D"), p.Code, p.Name, p.Frequency.ToString(), p.CustomDays, p.ScopeKind.ToString(),
        p.NextDueOn.ToString(RecallSupport.DateFormat, CultureInfo.InvariantCulture), new RecallUserRefDto(p.OwnerUserId.ToString("D")), p.Active,
        RecallSupport.Wire(p.CreatedAt), p.Version);

    public static RecallDrillRunDto Run(RecallDrillRun r)
    {
        var traced = r.Steps.Any(s => s.Step == RecallDrillStep.Trace) && r.Trace is not null;
        var listed = r.Steps.Any(s => s.Step == RecallDrillStep.CustomerList);
        return new RecallDrillRunDto(
            r.Id.ToString("D"), r.Number, r.PlanId.ToString("D"), r.PlanCode, r.Status.ToString(), r.SelectionMode.ToString(),
            new RecallLotRefDto(r.LotId, r.LotNumber), r.CandidateCount, RecallSupport.Wire(r.StartedAtUtc), new RecallUserRefDto(r.StartedBy.ToString("D")),
            r.Steps.Select(s => new RecallDrillStepDto(s.Step.ToString(), RecallSupport.Wire(s.CompletedAtUtc), new RecallUserRefDto(s.CompletedBy.ToString("D")))).ToList(),
            r.Status == RecallDrillRunStatus.Running ? RecallDrillRules.NextStep(r)?.ToString() : null,
            RecallSupport.Wire(r.EndedAtUtc), r.DurationMinutes, r.TargetHours, r.Passed, r.GapCount,
            traced && r.SnapshotId is { } snapshotId
                ? new RecallDrillSnapshotDto(snapshotId.ToString("D"), RecallSnapshotPurpose.Drill.ToString(), r.SnapshotSha256 ?? string.Empty, TraceRules.CanonicalVersion,
                    r.Trace!.NodeCount, r.GapCount, r.Trace.Truncated)
                : null,
            traced ? new RecallDrillScopeSummaryDto(r.Trace!.NodeCount, r.Trace.LotCount, r.Trace.StockCount, r.Trace.ShipmentCount, r.GapCount, r.Trace.Truncated) : null,
            listed && r.Trace is not null ? r.Trace.Customers : null,
            r.PendingResult is { } p ? new RecallDrillPendingResultDto(new RecallUserRefDto(p.SetBy.ToString("D")), RecallSupport.Wire(p.SetAtUtc), RecallSupport.Wire(p.ExpiresAtUtc)) : null,
            r.ResultNote, r.ResultSignatureId,
            r.CompletedBy is { } by && r.CompletedAtUtc is { } at ? new RecallUserAtDto(by.ToString("D"), RecallSupport.Wire(at)) : null,
            r.CancelReason,
            r.CancelledBy is { } cancelledBy && r.CancelledAtUtc is { } cancelledAt ? new RecallUserAtDto(cancelledBy.ToString("D"), RecallSupport.Wire(cancelledAt)) : null,
            r.Version);
    }

    public static RecallDrillEvent Event(RecallSupport support, Guid subjectId, RecallDrillEventKind kind, DateTimeOffset at, string? idempotencyKey, string? fingerprint,
        IReadOnlyDictionary<string, string?>? refs = null, string? text = null, string? signatureId = null) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = at,
        SubjectId = subjectId,
        Kind = kind,
        ActorUserId = support.UserId,
        AtUtc = at,
        Text = text,
        Refs = refs is null ? [] : new Dictionary<string, string?>(refs, StringComparer.Ordinal),
        SignatureId = signatureId,
        IdempotencyKey = idempotencyKey,
        RequestFingerprint = fingerprint,
        CorrelationId = support.CorrelationId
    };

    public static Response<T> PlanNotFound<T>() => Response<T>.Fail(RecallErrorCodes.DrillNotFound, "Drill plan not found.", 404);

    public static Response<T> RunNotFound<T>() => Response<T>.Fail(RecallErrorCodes.DrillNotFound, "Drill run not found.", 404);

    public static Response<T> Closed<T>(RecallDrillRun run)
        => RecallSupport.InvalidTransition<T>($"Drill run {run.Number} is {run.Status}; a closed run never changes (CT-R17).");

    public static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string? I(int? value) => value is { } v ? I(v) : null;
}
