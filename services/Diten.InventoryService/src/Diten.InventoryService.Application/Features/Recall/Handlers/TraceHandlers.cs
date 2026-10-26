using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Commands;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Application.Features.Recall.Trace;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Recall.Trace;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Handlers;

/// <summary>The audit row of one trace run (pack §8.12 <c>recall.trace.run</c>: every trace — ad-hoc, case, export).</summary>
public static class RecallTraceAudit
{
    public static RecallAuditRecord Of(RecallSupport support, TraceResult r, string context, Guid? caseId, string? number, DateTimeOffset at, string? key)
        => support.Audit(RecallContract.AuditTraceRun, caseId is null ? RecallContract.SubjectTrace : RecallContract.SubjectCase,
            caseId ?? Guid.Parse(r.RootLotId), number, at, key, new Dictionary<string, string?>
            {
                ["context"] = context,
                ["rootLotId"] = r.RootLotId,
                ["direction"] = r.Direction,
                ["nodes"] = r.Nodes.Count.ToString(CultureInfo.InvariantCulture),
                ["gaps"] = r.Summary.GapCount.ToString(CultureInfo.InvariantCulture),
                ["truncated"] = r.Truncated ? "true" : "false",
                ["durationMs"] = r.DurationMs.ToString(CultureInfo.InvariantCulture),
                ["sha256"] = r.Sha256
            });

    /// <summary>Query parameters of runTrace / exportTrace (canonical UUID, exact direction name, ISO-8601 instant).</summary>
    public static string? Parse(string? rootLotId, string? direction, string? asOf, out Guid root, out RecallTraceDirection dir, out DateTimeOffset? at)
    {
        dir = RecallTraceDirection.Both;
        at = null;
        if (!RecallSupport.TryParseId(rootLotId, out root)) return "rootLotId is required and must be a canonical UUID.";
        if (direction is not null && !RecallWire.TryParseName(direction, out dir)) return "direction must be Backward, Forward or Both.";
        if (asOf is null) return null;
        if (!DateTimeOffset.TryParse(asOf, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return "asOfUtc must be an ISO-8601 instant.";
        at = parsed;
        return null;
    }
}

/// <summary>§4.2 runTrace (pack §8.2) — writes the trace audit row; 404 <c>UNKNOWN_LOT</c> when the root is not the tenant's lot.</summary>
public sealed class RunRecallTraceHandler : IRequestHandler<RunRecallTraceCommand, Response<RecallTraceDto>>
{
    private readonly RecallTraceService _trace;
    private readonly IRecallRepository _repository;
    private readonly RecallSupport _support;

    public RunRecallTraceHandler(RecallTraceService trace, IRecallRepository repository, RecallSupport support)
    {
        _trace = trace;
        _repository = repository;
        _support = support;
    }

    public async Task<Response<RecallTraceDto>> Handle(RunRecallTraceCommand request, CancellationToken ct)
    {
        if (RecallTraceAudit.Parse(request.RootLotId, request.Direction, request.AsOfUtc, out var root, out var direction, out var asOf) is { } error)
        {
            return RecallSupport.Invalid<RecallTraceDto>(error);
        }

        var result = await _trace.RunAsync(root, direction, asOf, ct);
        if (result is null) return RecallSupport.Fail<RecallTraceDto>(RecallErrorCodes.UnknownLot, "Lot not found.", 404);
        await _repository.InTransactionAsync<bool>(async (session, tx) =>
        {
            await session.InsertAuditAsync(RecallTraceAudit.Of(_support, result, "runTrace", null, null, _support.Now(), null), tx);
            return true;
        }, ct);
        return Response<RecallTraceDto>.Success(RecallTraceService.Dto(result));
    }
}

/// <summary>§4.3 exportTrace — the CSV of the trace (rule-10 order); audited as a trace run, not logged as evidence.</summary>
public sealed class ExportRecallTraceHandler : IRequestHandler<ExportRecallTraceCommand, Response<RecallTraceCsvDto>>
{
    private readonly RecallTraceService _trace;
    private readonly IRecallRepository _repository;
    private readonly RecallSupport _support;

    public ExportRecallTraceHandler(RecallTraceService trace, IRecallRepository repository, RecallSupport support)
    {
        _trace = trace;
        _repository = repository;
        _support = support;
    }

    public async Task<Response<RecallTraceCsvDto>> Handle(ExportRecallTraceCommand request, CancellationToken ct)
    {
        if (request.Format is not (null or "csv")) return RecallSupport.Invalid<RecallTraceCsvDto>("format must be csv.");
        if (RecallTraceAudit.Parse(request.RootLotId, request.Direction, null, out var root, out var direction, out _) is { } error)
        {
            return RecallSupport.Invalid<RecallTraceCsvDto>(error);
        }

        var result = await _trace.RunAsync(root, direction, null, ct);
        if (result is null) return RecallSupport.Fail<RecallTraceCsvDto>(RecallErrorCodes.UnknownLot, "Lot not found.", 404);
        await _repository.InTransactionAsync<bool>(async (session, tx) =>
        {
            await session.InsertAuditAsync(RecallTraceAudit.Of(_support, result, "exportTrace", null, null, _support.Now(), null), tx);
            return true;
        }, ct);
        return Response<RecallTraceCsvDto>.Success(new RecallTraceCsvDto($"izleme_{result.RootLotNumber}.csv", RecallTraceService.Csv(result)));
    }
}

/// <summary>
/// §4.7 getCaseScopeDiff (pack §8.3 step 5) — "Yeniden izle": the live trace against the APPROVED snapshot. Nodes found now but not in the
/// snapshot are "Kapsam dışı bulgu" — each recorded once as an <c>OutOfScopeFinding</c> event (+ audit); they change nothing by themselves
/// (adding them is a scope revision, CT-R5). Before approval → 409 <c>RECALL_INVALID_TRANSITION</c>.
/// </summary>
public sealed class DiffRecallScopeHandler : IRequestHandler<DiffRecallScopeCommand, Response<RecallScopeDiffDto>>
{
    private readonly RecallTraceService _trace;
    private readonly IRecallRepository _repository;
    private readonly RecallSupport _support;

    public DiffRecallScopeHandler(RecallTraceService trace, IRecallRepository repository, RecallSupport support)
    {
        _trace = trace;
        _repository = repository;
        _support = support;
    }

    public async Task<Response<RecallScopeDiffDto>> Handle(DiffRecallScopeCommand request, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(request.CaseId, out var caseId)) return RecallSupport.CaseNotFound<RecallScopeDiffDto>();
        var c = await _repository.FindCaseAsync(caseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallScopeDiffDto>();
        if (c.ApprovedSnapshotId is not { } snapshotId || await _repository.FindSnapshotAsync(snapshotId, ct) is not { } approved)
        {
            return RecallSupport.InvalidTransition<RecallScopeDiffDto>("The scope diff compares with the approved snapshot; this case has none yet.");
        }

        var live = await _trace.RunAsync(Guid.Parse(c.RootLotId), c.Direction, null, ct);
        if (live is null) return RecallSupport.Fail<RecallScopeDiffDto>(RecallErrorCodes.UnknownLot, "Lot not found.", 404);
        var approvedKeys = approved.NodeKeys.ToHashSet(StringComparer.Ordinal);
        var liveKeys = live.Nodes.Select(n => n.NodeKey).ToHashSet(StringComparer.Ordinal);
        var added = live.Nodes.Where(n => !approvedKeys.Contains(n.NodeKey)).ToList();
        var removed = approved.NodeKeys.Where(k => !liveKeys.Contains(k)).ToList();

        var reported = (await _repository.AllEventsAsync(c.Id, ct)).Where(e => e.Kind == RecallCaseEventKind.OutOfScopeFinding)
            .Select(e => e.Refs.GetValueOrDefault("nodeKey")).ToHashSet(StringComparer.Ordinal);
        var now = _support.Now();
        await _repository.InTransactionAsync<bool>(async (session, tx) =>
        {
            foreach (var node in added.Where(n => !reported.Contains(n.NodeKey)))
            {
                var refs = new Dictionary<string, string?> { ["nodeKey"] = node.NodeKey, ["kind"] = node.Kind.ToString(), ["approvedSha256"] = approved.Sha256 };
                await session.InsertEventAsync(_support.Event(c.Id, RecallCaseEventKind.OutOfScopeFinding, now, node.Text ?? node.NodeKey, refs), tx);
                await session.InsertAuditAsync(_support.Audit(RecallContract.AuditOutOfScopeFinding, RecallContract.SubjectCase, c.Id, c.Number, now, null, refs), tx);
            }

            await session.InsertAuditAsync(RecallTraceAudit.Of(_support, live, "scopeDiff", c.Id, c.Number, now, null), tx);
            await session.InsertAuditAsync(_support.Audit(RecallContract.AuditScopeDiffed, RecallContract.SubjectCase, c.Id, c.Number, now, null,
                new Dictionary<string, string?>
                {
                    ["approvedSha256"] = approved.Sha256,
                    ["liveSha256"] = live.Sha256,
                    ["added"] = added.Count.ToString(CultureInfo.InvariantCulture),
                    ["removed"] = removed.Count.ToString(CultureInfo.InvariantCulture)
                }), tx);
            return true;
        }, ct);
        return Response<RecallScopeDiffDto>.Success(new RecallScopeDiffDto(approved.Sha256, live.Sha256, added.Select(RecallTraceService.Node).ToList(), removed,
            RecallSupport.Wire(live.ComputedAtUtc)));
    }
}

/// <summary>
/// Pack §8.3 step 4 — the live re-trace against the SUBMITTED snapshot. A different seal is 409 <c>RECALL_SCOPE_CHANGED</c> with the diff
/// (node keys added / removed), so the approver never prepares or signs a stale trace. A case submitted before R2 has no snapshot: no check.
/// </summary>
public static class RecallScopeDrift
{
    public static async Task<(TraceResult? Live, Dictionary<string, object?>? Drift)> CheckAsync(
        RecallTraceService trace, IRecallRepository repository, RecallCase c, CancellationToken ct)
    {
        if (c.SubmittedSnapshotId is not { } id || await repository.FindSnapshotAsync(id, ct) is not { } submitted) return (null, null);
        var live = await trace.RunAsync(Guid.Parse(c.RootLotId), c.Direction, null, ct);
        if (live is null) return (null, new Dictionary<string, object?> { ["submittedSha256"] = submitted.Sha256, ["currentSha256"] = null });
        if (string.Equals(live.Sha256, submitted.Sha256, StringComparison.Ordinal)) return (live, null);
        var before = submitted.NodeKeys.ToHashSet(StringComparer.Ordinal);
        var now = live.Nodes.Select(n => n.NodeKey).ToHashSet(StringComparer.Ordinal);
        return (live, new Dictionary<string, object?>
        {
            ["submittedSha256"] = submitted.Sha256,
            ["currentSha256"] = live.Sha256,
            ["added"] = now.Where(k => !before.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList(),
            ["removed"] = before.Where(k => !now.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList()
        });
    }

    public static Response<T> Refusal<T>(Dictionary<string, object?> drift)
        => Response<T>.Fail(RecallErrorCodes.ScopeChanged, "The live trace differs from the submitted snapshot; return the scope to Draft and submit it again.", 409, drift);
}
