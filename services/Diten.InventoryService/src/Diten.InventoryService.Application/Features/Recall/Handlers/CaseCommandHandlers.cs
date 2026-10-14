using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Commands;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Handlers;

/// <summary>
/// §4.30 openCase — R1 by hand (opening from a trace is R2). The root lot is the tenant's (MOD-0174 CT-D3); one NON-TERMINAL case per
/// company + root lot (409 <c>RECALL_CASE_ALREADY_OPEN</c>). Class 1–3, level, direction, reason 10–1000. Case + root scope item + Opened
/// event + audit row in ONE transaction; the number is <c>RC-{yyyy}-{nnnn}</c>.
/// </summary>
public sealed class OpenRecallCaseHandler : IRequestHandler<OpenRecallCaseCommand, Response<RecallCaseDetailDto>>
{
    private readonly IRecallRepository _repository;
    private readonly ILotRepository _lots;
    private readonly RecallReads _reads;
    private readonly RecallSupport _support;
    private readonly Trace.RecallTraceService _trace;

    public OpenRecallCaseHandler(IRecallRepository repository, ILotRepository lots, RecallReads reads, RecallSupport support, Trace.RecallTraceService trace)
    {
        _repository = repository;
        _lots = lots;
        _reads = reads;
        _support = support;
        _trace = trace;
    }

    public async Task<Response<RecallCaseDetailDto>> Handle(OpenRecallCaseCommand request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        var reference = RecallSupport.Trimmed(request.ReasonReference);
        var fingerprint = RecallSupport.Fingerprint(("rootLotId", request.RootLotId.ToString("D")), ("direction", request.Direction.ToString()),
            ("class", request.Class.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("level", request.Level.ToString()), ("reason", reason),
            ("reasonReference", reference));
        if (await _repository.FindCaseByCreateKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.CreateRequestFingerprint == fingerprint
                ? Response<RecallCaseDetailDto>.Success(await _reads.DetailAsync(earlier, ct))
                : RecallSupport.KeyReused<RecallCaseDetailDto>();
        }

        if (RecallCaseRules.Reason(reason) is { } refusal) return RecallSupport.Fail<RecallCaseDetailDto>(refusal);
        if (request.Class is < 1 or > 3) return RecallSupport.Invalid<RecallCaseDetailDto>("class must be 1, 2 or 3.");
        if (reference is { Length: > RecallCaseRules.MaxReferenceLength })
        {
            return RecallSupport.Invalid<RecallCaseDetailDto>($"reasonReference must be at most {RecallCaseRules.MaxReferenceLength} characters.");
        }

        var lot = await _lots.FindAsync(request.RootLotId, ct);
        if (lot is null) return RecallSupport.Fail<RecallCaseDetailDto>(RecallErrorCodes.UnknownLot, "Lot not found.", 404);

        var lotId = lot.Id.ToString("D");
        if (await _repository.FindActiveCaseByRootAsync(lotId, ct) is { } active) return AlreadyOpen(active.Id);

        var now = _support.Now();
        var recallCase = new RecallCase
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Status = RecallCaseStatus.Draft,
            Class = request.Class,
            Level = request.Level,
            Direction = request.Direction,
            RootLotId = lotId,
            RootLotNumber = lot.LotNumber,
            ItemId = lot.ItemId,
            SkuId = lot.SkuId,
            SkuLevel = lot.SkuLevel,
            Reason = reason!,
            ReasonReference = reference,
            OwnerUserId = _support.UserId,
            OpenedAtUtc = now,
            ScopeRevision = 1,
            CreateIdempotencyKey = request.IdempotencyKey,
            CreateRequestFingerprint = fingerprint,
            ActiveRootKey = lotId
        };
        var root = new RecallScopeItem
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            CaseId = recallCase.Id,
            Revision = 1,
            NodeKey = RecallScopeRules.NodeKey(RecallScopeItemKind.Lot, lotId),
            Kind = RecallScopeItemKind.Lot,
            IsRoot = true,
            LotId = lotId,
            LotNumber = lot.LotNumber,
            ItemId = lot.ItemId,
            SkuId = lot.SkuId,
            SkuLevel = lot.SkuLevel,
            Included = true,
            AddedBy = _support.UserId,
            AddedAtUtc = now
        };

        // R2 (pack §8.3 step 1): the live trace seeds the scope — root + forward lots, their stock and shipments, all included
        var trace = await _trace.RunAsync(lot.Id, request.Direction, null, ct);
        var items = trace is null ? [root] : Trace.RecallTraceService.ScopeItems(trace, recallCase.Id, 1, _support.UserId, now);
        if (items.Count == 0 || items.All(i => !i.IsRoot)) items.Insert(0, root);

        try
        {
            await _repository.InTransactionAsync<bool>(async (session, c) =>
            {
                recallCase.Number = await session.NextCaseNumberAsync(now.UtcDateTime.Year, c);
                await session.InsertCaseAsync(recallCase, c);
                await session.InsertScopeItemsAsync(items, c);
                await session.InsertEventAsync(_support.Event(recallCase.Id, RecallCaseEventKind.Opened, now, reason,
                    new Dictionary<string, string?> { ["rootLotId"] = lotId, ["rootLotNumber"] = lot.LotNumber, ["reasonReference"] = reference },
                    idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint), c);
                await session.InsertAuditAsync(_support.Audit(RecallContract.AuditCaseOpened, RecallContract.SubjectCase, recallCase.Id, recallCase.Number, now,
                    request.IdempotencyKey, new Dictionary<string, string?>
                    {
                        ["rootLotId"] = lotId,
                        ["class"] = request.Class.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["level"] = request.Level.ToString(),
                        ["reason"] = reason
                    }), c);
                if (trace is not null)
                {
                    await session.InsertEventAsync(_support.Event(recallCase.Id, RecallCaseEventKind.TraceRun, now, null, TraceRefs(trace)), c);
                    await session.InsertAuditAsync(RecallTraceAudit.Of(_support, trace, "openCase", recallCase.Id, recallCase.Number, now, request.IdempotencyKey), c);
                }

                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            if (await _repository.FindCaseByCreateKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } twin)
            {
                return twin.CreateRequestFingerprint == fingerprint
                    ? Response<RecallCaseDetailDto>.Success(await _reads.DetailAsync(twin, ct))
                    : RecallSupport.KeyReused<RecallCaseDetailDto>();
            }

            return await _repository.FindActiveCaseByRootAsync(lotId, ct) is { } other ? AlreadyOpen(other.Id) : RecallSupport.Conflict<RecallCaseDetailDto>();
        }

        return Response<RecallCaseDetailDto>.Success(await _reads.DetailAsync(recallCase, ct), 201);
    }

    internal static Dictionary<string, string?> TraceRefs(Domain.Recall.Trace.TraceResult t) => new()
    {
        ["sha256"] = t.Sha256,
        ["nodes"] = t.Nodes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["gaps"] = t.Summary.GapCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["truncated"] = t.Truncated ? "true" : "false",
        ["durationMs"] = t.DurationMs.ToString(System.Globalization.CultureInfo.InvariantCulture)
    };

    private static Response<RecallCaseDetailDto> AlreadyOpen(Guid existing)
        => Response<RecallCaseDetailDto>.Fail(RecallErrorCodes.CaseAlreadyOpen, "A recall case that is not closed or cancelled already exists for this root lot.", 409,
            new Dictionary<string, object?> { ["caseId"] = existing.ToString("D") });
}

/// <summary>
/// The shared skeleton of the If-Match commands on one case: the caller's earlier event with the same key answers the current state (same
/// body) or 400; then the case (404), then the command's own rule; the change is written with its event + audit row (+ the command's own
/// writes) in ONE transaction, guarded by the version (stale → 409 <c>RECALL_CONFLICT</c>).
/// </summary>
public abstract class RecallCaseCommandHandler
{
    protected RecallCaseCommandHandler(IRecallRepository repository, RecallReads reads, RecallSupport support)
    {
        Repository = repository;
        Reads = reads;
        Support = support;
    }

    protected IRecallRepository Repository { get; }

    protected RecallReads Reads { get; }

    protected RecallSupport Support { get; }

    /// <summary>null = not a replay; otherwise the answer to the replay.</summary>
    protected async Task<Response<T>?> ReplayAsync<T>(Guid caseId, string key, string fingerprint, Func<Guid, CancellationToken, Task<Response<T>>> current, CancellationToken ct)
    {
        var earlier = await Repository.FindEventByKeyAsync(caseId, Support.UserId, key, ct);
        if (earlier is null) return null;
        return earlier.RequestFingerprint == fingerprint ? await current(caseId, ct) : RecallSupport.KeyReused<T>();
    }

    protected Task<Response<RecallCaseDetailDto>?> ReplayCaseAsync(Guid caseId, string key, string fingerprint, CancellationToken ct)
        => ReplayAsync(caseId, key, fingerprint, Reads.CurrentAsync, ct);

    /// <summary>Writes the case (version + 1), its event, its audit row and <paramref name="extra"/> in one transaction; false = stale.</summary>
    protected async Task<bool> SaveAsync(RecallCase c, int expectedVersion, RecallCaseEvent caseEvent, RecallAuditRecord audit, CancellationToken ct,
        Func<IRecallWriteSession, CancellationToken, Task>? extra = null)
    {
        c.Version = expectedVersion + 1;
        c.UpdatedAt = caseEvent.AtUtc;
        try
        {
            return await Repository.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceCaseAsync(c, expectedVersion, tx)) return false;
                if (extra is not null) await extra(session, tx);
                await session.InsertEventAsync(caseEvent, tx);
                await session.InsertAuditAsync(audit, tx);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return false;
        }
    }

    protected async Task<Response<RecallCaseDetailDto>> WriteCaseAsync(RecallCase c, int expectedVersion, RecallCaseEvent caseEvent, RecallAuditRecord audit,
        CancellationToken ct, Func<IRecallWriteSession, CancellationToken, Task>? extra = null)
        => await SaveAsync(c, expectedVersion, caseEvent, audit, ct, extra)
            ? Response<RecallCaseDetailDto>.Success(await Reads.DetailAsync(c, ct))
            : RecallSupport.Conflict<RecallCaseDetailDto>();

    protected static Dictionary<string, string?> Detail(RecallCase c, params (string Key, string? Value)[] extra)
    {
        var detail = new Dictionary<string, string?>(StringComparer.Ordinal) { ["status"] = c.Status.ToString(), ["rootLotId"] = c.RootLotId };
        foreach (var (key, value) in extra) detail[key] = value;
        return detail;
    }

    /// <summary>
    /// The prepared decision of the caller: same action, unexpired, same payload (pack §8.8). null = matches.
    /// </summary>
    protected Response<T>? PendingRefusal<T>(RecallCase c, RecallPendingActionKind action, string payloadSha, DateTimeOffset now)
        => c.PendingAction is { } p && p.SetBy == Support.UserId && p.Action == action && p.ExpiresAtUtc > now
           && string.Equals(p.PayloadSha256, payloadSha, StringComparison.Ordinal)
            ? null
            : RecallSupport.PendingMismatch<T>();

    /// <summary>
    /// The MOD-0022 gate of a signable decision: required and missing → 400, sent and not verified → 409. WP-0177-R4: the verifier (F3-A)
    /// recomputes the <c>rc-v1</c> hash of the case as stored — before this command changes it — and compares it with the signed one.
    /// </summary>
    protected static async Task<Response<T>?> SignatureRefusalAsync<T>(IRecallSignature signature, RecallCase c, RecallPendingActionKind action, string? signatureId,
        Guid signer, CancellationToken ct)
    {
        var code = RecallCaseRules.ActionCode(action);
        var requirement = await signature.RequirementAsync(code, c.Id, ct);
        if (requirement.Required && signatureId is null) return RecallSupport.SignatureRequired<T>();
        if (signatureId is not null && !await signature.VerifyAsync(new RecallSignatureVerification(signatureId, code, requirement.MeaningCode, signer,
                RecallContract.CaseSubjectType, c.Id, c.PendingAction!.SetAtUtc), ct))
        {
            return RecallSupport.SignatureMismatch<T>();
        }

        return null;
    }

    public static string PayloadSha(RecallPendingActionKind action, string? note, string? summary, string? acceptance) => action switch
    {
        RecallPendingActionKind.ApproveScope => RecallSupport.Fingerprint(("action", action.ToString()), ("note", note)),
        RecallPendingActionKind.CompleteEffectiveness => RecallSupport.Fingerprint(("action", action.ToString()), ("note", note)),
        RecallPendingActionKind.Close => RecallSupport.Fingerprint(("action", action.ToString()), ("summary", summary), ("acceptance", acceptance)),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    protected static string? NoteError(string? note)
        => note is { Length: > RecallCaseRules.MaxNoteLength } ? $"note must be at most {RecallCaseRules.MaxNoteLength} characters." : null;
}

/// <summary>
/// §4.31a addScopeItems — R1 hand entry (Draft only). A <c>Lot</c> item names a tenant lot under a Lot parent (default: the root); a
/// <c>Stock</c> item a warehouse of the company + status + quantity + unit under its lot's item; a <c>Shipment</c> item a source document +
/// customer + quantity + unit under its lot's item. Node keys are computed (<see cref="RecallScopeRules.NodeKey"/>); a key already in the
/// scope → 409 <c>RECALL_SCOPE_ITEM_EXISTS</c>.
/// </summary>
public sealed class AddRecallScopeItemsHandler : RecallCaseCommandHandler, IRequestHandler<AddRecallScopeItemsCommand, Response<RecallScopeViewDto>>
{
    private readonly ILotRepository _lots;
    private readonly ILocationRepository _locations;

    public AddRecallScopeItemsHandler(IRecallRepository repository, RecallReads reads, RecallSupport support, ILotRepository lots, ILocationRepository locations)
        : base(repository, reads, support)
    {
        _lots = lots;
        _locations = locations;
    }

    public async Task<Response<RecallScopeViewDto>> Handle(AddRecallScopeItemsCommand request, CancellationToken ct)
    {
        var fingerprint = RecallSupport.Fingerprint(("op", "add-items"), ("id", request.CaseId.ToString("D")), ("items", string.Join("|", request.Items.Select(i =>
            string.Join(";", i.Kind, i.LotId.ToString("D"), i.ParentKey, i.WarehouseId?.ToString("D"), i.LocationId?.ToString("D"), i.StockStatus?.ToString(),
                i.SourceDocumentId, i.CustomerRef, RecallSupport.D(i.Quantity), i.BaseUomId)))));
        if (await ReplayAsync(request.CaseId, request.IdempotencyKey, fingerprint, Reads.CurrentScopeAsync, ct) is { } replay) return replay;

        if (request.Items.Count is < 1 or > RecallScopeRules.MaxItemsPerRequest)
        {
            return RecallSupport.Invalid<RecallScopeViewDto>($"items must hold 1-{RecallScopeRules.MaxItemsPerRequest} entries.");
        }

        var c = await Repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallScopeViewDto>();
        if (!RecallCaseRules.CanEditScope(c.Status))
        {
            return RecallSupport.InvalidTransition<RecallScopeViewDto>($"The scope of a case in status {c.Status} cannot change (Draft only).");
        }

        var existing = await Repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct);
        var keys = existing.ToDictionary(i => i.NodeKey, StringComparer.Ordinal);
        var lots = (await _lots.FindManyAsync(request.Items.Select(i => i.LotId).Distinct().ToList(), ct)).ToDictionary(l => l.Id);
        var now = Support.Now();
        var added = new List<RecallScopeItem>();
        foreach (var input in request.Items)
        {
            if (!lots.TryGetValue(input.LotId, out var lot))
            {
                return RecallSupport.Fail<RecallScopeViewDto>(RecallErrorCodes.UnknownLot, $"Lot {input.LotId:D} not found.", 404);
            }

            var lotId = lot.Id.ToString("D");
            var lotKey = RecallScopeRules.NodeKey(RecallScopeItemKind.Lot, lotId);
            string? parent;
            string key;
            switch (input.Kind)
            {
                case RecallScopeItemKind.Lot:
                    parent = input.ParentKey ?? RecallScopeRules.NodeKey(RecallScopeItemKind.Lot, c.RootLotId);
                    if (!keys.TryGetValue(parent, out var p) || p.Kind != RecallScopeItemKind.Lot)
                    {
                        return RecallSupport.Invalid<RecallScopeViewDto>($"parentKey '{parent}' is not a Lot item of this scope.");
                    }

                    key = lotKey;
                    break;
                case RecallScopeItemKind.Stock:
                    if (input.WarehouseId is not { } wh || input.StockStatus is null || input.Quantity is not > 0 || string.IsNullOrWhiteSpace(input.BaseUomId))
                    {
                        return RecallSupport.Invalid<RecallScopeViewDto>("A Stock item needs warehouseId, stockStatus, quantity > 0 and baseUomId.");
                    }

                    if (await _locations.FindAsync(wh, ct) is not { Type: LocationType.Warehouse })
                    {
                        return RecallSupport.Fail<RecallScopeViewDto>(InventoryErrorCodes.UnknownLocation, "warehouseId is not a warehouse of the company.", 404);
                    }

                    if (input.LocationId is { } loc && await _locations.FindAsync(loc, ct) is null)
                    {
                        return RecallSupport.Fail<RecallScopeViewDto>(InventoryErrorCodes.UnknownLocation, "locationId is not a location of the company.", 404);
                    }

                    parent = lotKey;
                    key = RecallScopeRules.NodeKey(RecallScopeItemKind.Stock, lotId, wh.ToString("D"), input.LocationId?.ToString("D"),
                        WireNames.ToWire(input.StockStatus.Value));
                    break;
                case RecallScopeItemKind.Shipment:
                    var document = RecallSupport.Trimmed(input.SourceDocumentId);
                    var customer = RecallSupport.Trimmed(input.CustomerRef);
                    if (document is null || customer is null || input.Quantity is not > 0 || string.IsNullOrWhiteSpace(input.BaseUomId))
                    {
                        return RecallSupport.Invalid<RecallScopeViewDto>("A Shipment item needs sourceDocumentId, customerRef, quantity > 0 and baseUomId.");
                    }

                    if (document.Length > RecallContract.MaxDocumentIdLength || customer.Length > RecallContract.MaxCustomerRefLength)
                    {
                        return RecallSupport.Invalid<RecallScopeViewDto>(
                            $"sourceDocumentId ≤ {RecallContract.MaxDocumentIdLength} and customerRef ≤ {RecallContract.MaxCustomerRefLength} characters.");
                    }

                    if (input.WarehouseId is { } shipWh && await _locations.FindAsync(shipWh, ct) is not { Type: LocationType.Warehouse })
                    {
                        return RecallSupport.Fail<RecallScopeViewDto>(InventoryErrorCodes.UnknownLocation, "warehouseId is not a warehouse of the company.", 404);
                    }

                    parent = lotKey;
                    key = RecallScopeRules.NodeKey(RecallScopeItemKind.Shipment, lotId, sourceDocumentId: document);
                    break;
                default:
                    return RecallSupport.Invalid<RecallScopeViewDto>("Unknown kind.");
            }

            if (input.Kind != RecallScopeItemKind.Lot && !keys.ContainsKey(lotKey))
            {
                return RecallSupport.Invalid<RecallScopeViewDto>($"Add the Lot item '{lotKey}' before its stock or shipment items.");
            }

            if (input.BaseUomId is { Length: > RecallContract.MaxUomLength })
            {
                return RecallSupport.Invalid<RecallScopeViewDto>($"baseUomId must be at most {RecallContract.MaxUomLength} characters.");
            }

            if (keys.ContainsKey(key))
            {
                return Response<RecallScopeViewDto>.Fail(RecallErrorCodes.ScopeItemExists, $"The scope already holds '{key}'.", 409,
                    new Dictionary<string, object?> { ["nodeKey"] = key });
            }

            var item = new RecallScopeItem
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                CaseId = c.Id,
                Revision = c.ScopeRevision,
                NodeKey = key,
                ParentKey = parent,
                Kind = input.Kind,
                LotId = lotId,
                LotNumber = lot.LotNumber,
                ItemId = lot.ItemId,
                SkuId = lot.SkuId,
                SkuLevel = lot.SkuLevel,
                WarehouseId = input.WarehouseId?.ToString("D"),
                LocationId = input.Kind == RecallScopeItemKind.Stock ? input.LocationId?.ToString("D") : null,
                StockStatus = input.Kind == RecallScopeItemKind.Stock ? input.StockStatus : null,
                SourceDocumentId = input.Kind == RecallScopeItemKind.Shipment ? RecallSupport.Trimmed(input.SourceDocumentId) : null,
                CustomerRef = input.Kind == RecallScopeItemKind.Shipment ? RecallSupport.Trimmed(input.CustomerRef) : null,
                Quantity = input.Kind == RecallScopeItemKind.Lot ? null : input.Quantity,
                BaseUomId = input.Kind == RecallScopeItemKind.Lot ? null : input.BaseUomId!.Trim(),
                Included = true,
                AddedBy = Support.UserId,
                AddedAtUtc = now
            };
            keys[key] = item;
            added.Add(item);
        }

        if (c.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallScopeViewDto>();

        var saved = await SaveAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, RecallCaseEventKind.ScopeItemsAdded, now, null,
                new Dictionary<string, string?> { ["count"] = added.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint),
            Support.Audit(RecallContract.AuditScopeItemsAdded, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey,
                Detail(c, ("nodeKeys", string.Join(",", added.Select(a => a.NodeKey))))),
            ct, (session, tx) => session.InsertScopeItemsAsync(added, tx));
        return saved ? Response<RecallScopeViewDto>.Success(await Reads.ScopeAsync(c, ct)) : RecallSupport.Conflict<RecallScopeViewDto>();
    }
}

/// <summary>
/// §4.31 markScopeItems — include / exclude (Draft; QA). The root cannot be marked; an exclusion needs a reason ≥ 10
/// (<c>RECALL_SCOPE_EXCLUSION_REASON_REQUIRED</c>); excluding an item excludes its subtree (CT-R6, effective inclusion).
/// </summary>
public sealed class MarkRecallScopeItemsHandler : RecallCaseCommandHandler, IRequestHandler<MarkRecallScopeItemsCommand, Response<RecallScopeViewDto>>
{
    public MarkRecallScopeItemsHandler(IRecallRepository repository, RecallReads reads, RecallSupport support) : base(repository, reads, support)
    {
    }

    public async Task<Response<RecallScopeViewDto>> Handle(MarkRecallScopeItemsCommand request, CancellationToken ct)
    {
        var fingerprint = RecallSupport.Fingerprint(("op", "mark"), ("id", request.CaseId.ToString("D")),
            ("marks", string.Join("|", request.Marks.Select(m => $"{m.NodeKey};{m.Included};{m.ExclusionReason?.Trim()}"))));
        if (await ReplayAsync(request.CaseId, request.IdempotencyKey, fingerprint, Reads.CurrentScopeAsync, ct) is { } replay) return replay;

        if (request.Marks.Count is < 1 or > RecallContract.MaxMarksPerRequest)
        {
            return RecallSupport.Invalid<RecallScopeViewDto>($"marks must hold 1-{RecallContract.MaxMarksPerRequest} entries.");
        }

        if (request.Marks.Select(m => m.NodeKey).Distinct(StringComparer.Ordinal).Count() != request.Marks.Count)
        {
            return RecallSupport.Invalid<RecallScopeViewDto>("A node key may be marked once per request.");
        }

        var c = await Repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallScopeViewDto>();
        if (!RecallCaseRules.CanEditScope(c.Status))
        {
            return RecallSupport.InvalidTransition<RecallScopeViewDto>($"The scope of a case in status {c.Status} cannot change (Draft only).");
        }

        var items = (await Repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct)).ToDictionary(i => i.NodeKey, StringComparer.Ordinal);
        var now = Support.Now();
        var changed = new List<RecallScopeItem>();
        foreach (var mark in request.Marks)
        {
            if (!items.TryGetValue(mark.NodeKey, out var item) || item.IsRoot)
            {
                return Response<RecallScopeViewDto>.Fail(RecallErrorCodes.ScopeItemNotMarkable,
                    $"'{mark.NodeKey}' is not a markable item of this scope (the root lot cannot be excluded).", 400,
                    new Dictionary<string, object?> { ["nodeKey"] = mark.NodeKey });
            }

            var reason = mark.ExclusionReason?.Trim();
            if (!mark.Included && RecallCaseRules.Reason(reason) is not null)
            {
                return Response<RecallScopeViewDto>.Fail(RecallErrorCodes.ScopeExclusionReasonRequired,
                    $"Excluding '{mark.NodeKey}' needs a reason of {RecallCaseRules.MinReasonLength}-{RecallCaseRules.MaxReasonLength} characters.", 400,
                    new Dictionary<string, object?> { ["nodeKey"] = mark.NodeKey });
            }

            item.Included = mark.Included;
            item.ExclusionReason = mark.Included ? null : reason;
            item.MarkedBy = Support.UserId;
            item.MarkedAtUtc = now;
            item.UpdatedAt = now;
            changed.Add(item);
        }

        if (c.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallScopeViewDto>();

        var saved = await SaveAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, RecallCaseEventKind.ScopeMarked, now, null,
                new Dictionary<string, string?>
                {
                    ["included"] = string.Join(",", changed.Where(i => i.Included).Select(i => i.NodeKey)),
                    ["excluded"] = string.Join(",", changed.Where(i => !i.Included).Select(i => i.NodeKey))
                }, idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint),
            Support.Audit(RecallContract.AuditScopeMarked, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey,
                Detail(c, ("marks", string.Join(",", changed.Select(i => $"{i.NodeKey}={(i.Included ? "in" : "out")}"))))),
            ct, async (session, tx) =>
            {
                foreach (var item in changed) await session.ReplaceScopeItemAsync(item, tx);
            });
        return saved ? Response<RecallScopeViewDto>.Success(await Reads.ScopeAsync(c, ct)) : RecallSupport.Conflict<RecallScopeViewDto>();
    }
}

/// <summary>
/// §4.32 submitScope — Draft → PendingScopeApproval. The scope is SEALED: the SHA-256 of its canonical form (pack §8.3,
/// <see cref="RecallScopeRules.Canonical"/>) and the submitter (CT-R1: may not approve) are stored.
/// </summary>
public sealed class SubmitRecallScopeHandler : RecallCaseCommandHandler, IRequestHandler<SubmitRecallScopeCommand, Response<RecallCaseDetailDto>>
{
    private readonly Trace.RecallTraceService _trace;

    public SubmitRecallScopeHandler(IRecallRepository repository, RecallReads reads, RecallSupport support, Trace.RecallTraceService trace)
        : base(repository, reads, support) => _trace = trace;

    public async Task<Response<RecallCaseDetailDto>> Handle(SubmitRecallScopeCommand request, CancellationToken ct)
    {
        var note = RecallSupport.Trimmed(request.Note);
        var fingerprint = RecallSupport.Fingerprint(("op", "submit"), ("id", request.CaseId.ToString("D")), ("note", note));
        if (await ReplayCaseAsync(request.CaseId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (NoteError(note) is { } noteError) return RecallSupport.Invalid<RecallCaseDetailDto>(noteError);

        var c = await Repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallCaseDetailDto>();
        if (!RecallCaseRules.CanSubmit(c.Status)) return RecallSupport.InvalidTransition<RecallCaseDetailDto>($"A case in status {c.Status} cannot be submitted.");
        if (c.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallCaseDetailDto>();

        // R2 (pack §8.3 step 3): re-trace, reconcile the trace items (manual items stay), seal the trace (rt-v1) and the scope (rs-scope-v1)
        var now = Support.Now();
        var trace = await _trace.RunAsync(Guid.Parse(c.RootLotId), c.Direction, null, ct);
        if (trace is null) return RecallSupport.Fail<RecallCaseDetailDto>(RecallErrorCodes.UnknownLot, "The root lot no longer exists for the tenant.", 404);
        var stored = await Repository.ScopeItemsWithRemovedAsync(c.Id, c.ScopeRevision, ct);
        var (inserts, replaces) = Trace.RecallTraceService.Reconcile(stored, Trace.RecallTraceService.ScopeItems(trace, c.Id, c.ScopeRevision, Support.UserId, now), now);
        var items = stored.Where(i => !i.IsDeleted).Concat(inserts).ToList();
        var snapshot = _trace.Snapshot(trace, c, RecallSnapshotPurpose.Submitted, now);
        c.SubmittedSnapshotId = snapshot.Id;
        c.SubmittedScope = new RecallScopeSeal
        {
            Revision = c.ScopeRevision,
            Sha256 = RecallScopeRules.Sha256(items, c.ScopeRevision),
            CanonicalVersion = RecallScopeRules.CanonicalVersion,
            ItemCount = items.Count,
            ComputedAtUtc = now
        };
        c.ScopeSubmittedBy = Support.UserId;
        c.ScopeSubmittedAtUtc = now;
        c.Status = RecallCaseStatus.PendingScopeApproval;
        c.PendingAction = null;
        return await WriteCaseAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, RecallCaseEventKind.ScopeSubmitted, now, note,
                new Dictionary<string, string?> { ["scopeSha256"] = c.SubmittedScope.Sha256, ["items"] = items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint),
            Support.Audit(RecallContract.AuditScopeSubmitted, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey,
                Detail(c, ("scopeSha256", c.SubmittedScope.Sha256), ("traceSha256", trace.Sha256), ("newItems", inserts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("revision", c.ScopeRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)))), ct,
            async (session, tx) =>
            {
                foreach (var item in replaces) await session.ReplaceScopeItemAsync(item, tx);
                await session.InsertScopeItemsAsync(inserts, tx);
                await session.InsertSnapshotAsync(snapshot, tx);
                await session.InsertEventAsync(Support.Event(c.Id, RecallCaseEventKind.TraceRun, now, null, OpenRecallCaseHandler.TraceRefs(trace)), tx);
                await session.InsertAuditAsync(RecallTraceAudit.Of(Support, trace, "submitScope", c.Id, c.Number, now, request.IdempotencyKey), tx);
            });
    }
}

/// <summary>§4.33 returnScope — PendingScopeApproval → Draft ("Geri gönder"), reason ≥ 10; the seal and any prepared approval are dropped.</summary>
public sealed class ReturnRecallScopeHandler : RecallCaseCommandHandler, IRequestHandler<ReturnRecallScopeCommand, Response<RecallCaseDetailDto>>
{
    public ReturnRecallScopeHandler(IRecallRepository repository, RecallReads reads, RecallSupport support) : base(repository, reads, support)
    {
    }

    public async Task<Response<RecallCaseDetailDto>> Handle(ReturnRecallScopeCommand request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        var fingerprint = RecallSupport.Fingerprint(("op", "return"), ("id", request.CaseId.ToString("D")), ("reason", reason));
        if (await ReplayCaseAsync(request.CaseId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (RecallCaseRules.Reason(reason) is { } refusal) return RecallSupport.Fail<RecallCaseDetailDto>(refusal);

        var c = await Repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallCaseDetailDto>();
        if (!RecallCaseRules.CanReturn(c.Status)) return RecallSupport.InvalidTransition<RecallCaseDetailDto>($"A case in status {c.Status} cannot be returned to Draft.");
        if (c.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallCaseDetailDto>();

        var now = Support.Now();
        c.Status = RecallCaseStatus.Draft;
        c.SubmittedScope = null;
        c.SubmittedSnapshotId = null;
        c.ScopeSubmittedBy = null;
        c.ScopeSubmittedAtUtc = null;
        c.PendingAction = null;
        return await WriteCaseAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, RecallCaseEventKind.ScopeReturned, now, reason, idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint),
            Support.Audit(RecallContract.AuditScopeReturned, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey, Detail(c, ("reason", reason))), ct);
    }
}

/// <summary>
/// §4.34 prepareCaseAction — the decision the caller is about to sign, held 15 minutes on the case (If-Match, version + 1). Refused like
/// the decision itself (status, CT-R1 SoD, scope changed, open quantity without acceptance) so the user is not sent to sign what cannot be
/// applied. Answers what the MOD-0022 dialog needs: required (effective policy), action code, meaning, visual mode, subject.
/// </summary>
public sealed class PrepareRecallCaseActionHandler : IRequestHandler<PrepareRecallCaseActionCommand, Response<RecallPendingActionResponseDto>>
{
    private readonly IRecallRepository _repository;
    private readonly IRecallSignature _signature;
    private readonly RecallSupport _support;
    private readonly Trace.RecallTraceService _trace;

    public PrepareRecallCaseActionHandler(IRecallRepository repository, IRecallSignature signature, RecallSupport support, Trace.RecallTraceService trace)
    {
        _repository = repository;
        _signature = signature;
        _support = support;
        _trace = trace;
    }

    public async Task<Response<RecallPendingActionResponseDto>> Handle(PrepareRecallCaseActionCommand request, CancellationToken ct)
    {
        var note = RecallSupport.Trimmed(request.Note);
        var summary = RecallSupport.Trimmed(request.Summary);
        var acceptance = RecallSupport.Trimmed(request.OpenQuantityAcceptance);
        var fingerprint = RecallSupport.Fingerprint(("op", "prepare"), ("id", request.CaseId.ToString("D")), ("action", request.Action.ToString()),
            ("note", note), ("summary", summary), ("acceptance", acceptance));
        var c = await _repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallPendingActionResponseDto>();

        if (c.PendingAction is { } pending && pending.SetBy == _support.UserId && pending.IdempotencyKey == request.IdempotencyKey)
        {
            return pending.RequestFingerprint == fingerprint ? await PreparedAsync(c, pending, ct) : RecallSupport.KeyReused<RecallPendingActionResponseDto>();
        }

        // the payload of each decision (the same rules the decision itself applies)
        switch (request.Action)
        {
            case RecallPendingActionKind.ApproveScope:
                if (summary is not null || acceptance is not null) return RecallSupport.Invalid<RecallPendingActionResponseDto>("ApproveScope takes a note only.");
                if (note is { Length: > RecallCaseRules.MaxNoteLength }) return RecallSupport.Invalid<RecallPendingActionResponseDto>($"note must be at most {RecallCaseRules.MaxNoteLength} characters.");
                break;
            case RecallPendingActionKind.CompleteEffectiveness:
                if (summary is not null || acceptance is not null) return RecallSupport.Invalid<RecallPendingActionResponseDto>("CompleteEffectiveness takes a note only.");
                if (RecallCaseRules.Reason(note, "note") is { } noteRefusal) return RecallSupport.Fail<RecallPendingActionResponseDto>(noteRefusal);
                break;
            case RecallPendingActionKind.Close:
                if (note is not null) return RecallSupport.Invalid<RecallPendingActionResponseDto>("Close takes summary and openQuantityAcceptance.");
                if (RecallCaseRules.Reason(summary, "summary", RecallCaseRules.MaxSummaryLength) is { } summaryRefusal) return RecallSupport.Fail<RecallPendingActionResponseDto>(summaryRefusal);
                if (acceptance is not null && RecallCaseRules.Reason(acceptance, "openQuantityAcceptance") is { } acceptRefusal)
                    return RecallSupport.Fail<RecallPendingActionResponseDto>(acceptRefusal);
                break;
        }

        if (!RecallCaseRules.CanPrepare(c, request.Action))
        {
            return RecallSupport.InvalidTransition<RecallPendingActionResponseDto>($"{request.Action} is not possible for a case in status {c.Status}.");
        }

        if (RecallCaseRules.SeparationOfDuties(c, request.Action, _support.UserId) is { } sod) return RecallSupport.Fail<RecallPendingActionResponseDto>(sod);

        string? scopeSha = null;
        if (request.Action == RecallPendingActionKind.ApproveScope)
        {
            scopeSha = RecallScopeRules.Sha256(await _repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct), c.ScopeRevision);
            if (!string.Equals(scopeSha, c.SubmittedScope?.Sha256, StringComparison.Ordinal)) return ScopeChanged(c, scopeSha);
            var (_, drift) = await RecallScopeDrift.CheckAsync(_trace, _repository, c, ct);
            if (drift is not null) return RecallScopeDrift.Refusal<RecallPendingActionResponseDto>(drift);
        }

        if (request.Action == RecallPendingActionKind.Close && RecallEffectivenessRule.HasOpenQuantity(c.Effectiveness!) && acceptance is null)
        {
            return RecallSupport.Fail<RecallPendingActionResponseDto>(RecallErrorCodes.OpenQuantityAcceptanceRequired,
                "An open quantity remains; closing needs openQuantityAcceptance (10-1000 characters).", 400);
        }

        if (c.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallPendingActionResponseDto>();

        var now = _support.Now();
        var prepared = new RecallPendingAction
        {
            Action = request.Action,
            SetBy = _support.UserId,
            SetAtUtc = now,
            ExpiresAtUtc = now + RecallCaseRules.PendingActionLifetime,
            PayloadSha256 = RecallCaseCommandHandler.PayloadSha(request.Action, note, summary, acceptance),
            ScopeSha256 = scopeSha,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint
        };
        c.PendingAction = prepared;
        c.Version = request.ExpectedVersion + 1;
        c.UpdatedAt = now;
        var saved = await _repository.InTransactionAsync(async (session, tx) =>
        {
            if (!await session.ReplaceCaseAsync(c, request.ExpectedVersion, tx)) return false;
            await session.InsertAuditAsync(_support.Audit(RecallContract.AuditActionPrepared, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey,
                new Dictionary<string, string?> { ["action"] = request.Action.ToString(), ["expiresAtUtc"] = RecallSupport.Wire(prepared.ExpiresAtUtc) }), tx);
            return true;
        }, ct);
        return saved ? await PreparedAsync(c, prepared, ct) : RecallSupport.Conflict<RecallPendingActionResponseDto>();
    }

    private async Task<Response<RecallPendingActionResponseDto>> PreparedAsync(RecallCase c, RecallPendingAction pending, CancellationToken ct)
    {
        var action = RecallCaseRules.ActionCode(pending.Action);
        var requirement = await _signature.RequirementAsync(action, c.Id, ct);
        return Response<RecallPendingActionResponseDto>.Success(new RecallPendingActionResponseDto(
            c.Id.ToString("D"), pending.Action.ToString(), requirement.Required, action, requirement.MeaningCode, requirement.VisualMode,
            new RecallSubjectRefDto(RecallContract.ESignModule, RecallContract.CaseSubjectType, c.Id.ToString("D")), RecallSupport.Wire(pending.ExpiresAtUtc), c.Version));
    }

    internal static Response<T> ScopeChangedOf<T>(RecallCase c, string current)
        => Response<T>.Fail(RecallErrorCodes.ScopeChanged, "The scope changed since it was submitted; return it to Draft and submit it again.", 409,
            new Dictionary<string, object?> { ["submittedSha256"] = c.SubmittedScope?.Sha256, ["currentSha256"] = current });

    private static Response<RecallPendingActionResponseDto> ScopeChanged(RecallCase c, string current) => ScopeChangedOf<RecallPendingActionResponseDto>(c, current);
}

/// <summary>
/// §4.35 approveScope — PendingScopeApproval → Active. Order: case → status → SoD (CT-R1, 409 <c>RECALL_SOD_VIOLATION</c>) → the caller's own
/// unexpired prepared approval with the same note (409 <c>RECALL_PENDING_ACTION_MISMATCH</c>) → the scope RE-SEALED now equals the submitted
/// seal and the prepared one (409 <c>RECALL_SCOPE_CHANGED</c>) → signature (fail-closed until R4) → If-Match. In ONE transaction: the case
/// (approved seal, approver, signature id), the lot blocks of the effectively included Lot items (pack §8.5 — read by
/// <c>getLotRecallStatus</c>, enforced by the posting gate from R3), the event and the audit row.
/// </summary>
public sealed class ApproveRecallScopeHandler : RecallCaseCommandHandler, IRequestHandler<ApproveRecallScopeCommand, Response<RecallCaseDetailDto>>
{
    private readonly IRecallSignature _signature;

    private readonly Trace.RecallTraceService _trace;

    private readonly ILotRepository _lots;

    private readonly Quarantine.RecallQuarantineDispatcher _dispatcher;

    public ApproveRecallScopeHandler(IRecallRepository repository, RecallReads reads, RecallSupport support, IRecallSignature signature, Trace.RecallTraceService trace,
        ILotRepository lots, Quarantine.RecallQuarantineDispatcher dispatcher)
        : base(repository, reads, support)
    {
        _signature = signature;
        _trace = trace;
        _lots = lots;
        _dispatcher = dispatcher;
    }

    public async Task<Response<RecallCaseDetailDto>> Handle(ApproveRecallScopeCommand request, CancellationToken ct)
    {
        var note = RecallSupport.Trimmed(request.Note);
        var signatureId = RecallSupport.Trimmed(request.SignatureId);
        var fingerprint = RecallSupport.Fingerprint(("op", "approve"), ("id", request.CaseId.ToString("D")), ("note", note), ("signatureId", signatureId));
        if (await ReplayCaseAsync(request.CaseId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (NoteError(note) is { } noteError) return RecallSupport.Invalid<RecallCaseDetailDto>(noteError);

        var c = await Repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallCaseDetailDto>();
        if (!RecallCaseRules.CanApproveScope(c)) return RecallSupport.InvalidTransition<RecallCaseDetailDto>($"A case in status {c.Status} cannot be approved.");
        var revision = RecallCaseRules.IsRevisionPending(c); // CT-R5: the approval of a revision's delta
        if (RecallCaseRules.SeparationOfDuties(c, RecallPendingActionKind.ApproveScope, Support.UserId) is { } sod) return RecallSupport.Fail<RecallCaseDetailDto>(sod);

        var now = Support.Now();
        if (PendingRefusal<RecallCaseDetailDto>(c, RecallPendingActionKind.ApproveScope, PayloadSha(RecallPendingActionKind.ApproveScope, note, null, null), now) is { } pendingRefusal)
        {
            return pendingRefusal;
        }

        // pack §8.3 step 4: the approver never signs a stale scope — re-seal now and compare with the submitted and the prepared seal
        var items = await Repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct);
        var sealNow = RecallScopeRules.Sha256(items, c.ScopeRevision);
        if (!string.Equals(sealNow, c.SubmittedScope?.Sha256, StringComparison.Ordinal) || !string.Equals(sealNow, c.PendingAction!.ScopeSha256, StringComparison.Ordinal))
        {
            return PrepareRecallCaseActionHandler.ScopeChangedOf<RecallCaseDetailDto>(c, sealNow);
        }

        // R2 (pack §8.3 step 4): the LIVE re-trace must equal the submitted snapshot — the approver never signs a stale trace
        var (live, drift) = await RecallScopeDrift.CheckAsync(_trace, Repository, c, ct);
        if (drift is not null) return RecallScopeDrift.Refusal<RecallCaseDetailDto>(drift);

        if (await SignatureRefusalAsync<RecallCaseDetailDto>(_signature, c, RecallPendingActionKind.ApproveScope, signatureId, Support.UserId, ct) is { } signatureRefusal)
        {
            return signatureRefusal;
        }

        if (c.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallCaseDetailDto>();

        var byKey = items.ToDictionary(i => i.NodeKey, StringComparer.Ordinal);

        // pack §8.5: the blocks — the effectively included Lot items (root + forward lots). A revision ADDS the delta's lots and never removes a
        // block of the approved scope (CT-R5); a lot already blocked by this case is not written twice.
        var alreadyBlocked = revision ? (await Repository.LotBlocksOfCaseAsync(c.Id, ct)).Select(b => b.LotId).ToHashSet(StringComparer.Ordinal) : [];
        var blocks = items.Where(i => i.Kind == RecallScopeItemKind.Lot && RecallScopeRules.EffectivelyIncluded(i, byKey))
            .GroupBy(i => i.LotId, StringComparer.Ordinal).Select(g => g.First())
            .Where(i => !alreadyBlocked.Contains(i.LotId))
            .Select(i => new RecallLotBlock
            {
                Id = Guid.NewGuid(), CreatedAt = now, LotId = i.LotId, LotNumber = i.LotNumber, SkuId = i.SkuId, SkuLevel = i.SkuLevel, Revision = c.ScopeRevision,
                CaseId = c.Id, CaseNumber = c.Number, Active = true, ActivatedAtUtc = now
            }).ToList();

        // pack §8.4 KR-B: one request per Stock item of this revision (a revision: only the delta's — the approved ones have theirs), in THIS
        // transaction as Pending / NotNeeded; the Pending ones are sent after the commit
        var requested = revision ? (await Repository.QuarantineRequestsAsync(c.Id, ct)).Select(r => r.NodeKey).ToHashSet(StringComparer.Ordinal) : [];
        var stock = items.Where(i => i.Kind == RecallScopeItemKind.Stock && !requested.Contains(i.NodeKey)).ToList();
        var lotsById = (await _lots.FindManyAsync(stock.Select(i => Guid.TryParse(i.LotId, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList(), ct))
            .ToDictionary(l => l.Id.ToString("D"), StringComparer.Ordinal);
        var requests = stock.Select(i => Quarantine.RecallQuarantinePlanner.Plan(c, c.ScopeRevision, i, RecallScopeRules.EffectivelyIncluded(i, byKey),
            lotsById.GetValueOrDefault(i.LotId), Support.UserId, now)).ToList();

        var approvedSnapshot = live is null ? null : _trace.Snapshot(live, c, RecallSnapshotPurpose.Approved, now);
        c.ApprovedSnapshotId = approvedSnapshot?.Id ?? c.ApprovedSnapshotId;
        c.Status = RecallCaseStatus.Active;
        c.PendingRevision = null;
        c.ApprovedScope = new RecallScopeSeal
        {
            Revision = c.ScopeRevision, Sha256 = sealNow, CanonicalVersion = RecallScopeRules.CanonicalVersion, ItemCount = items.Count, ComputedAtUtc = now
        };
        c.ScopeApprovedBy = Support.UserId;
        c.ScopeApprovedAtUtc = now;
        c.ScopeApprovalSignatureId = signatureId;
        c.PendingAction = null;
        var counts = new Dictionary<string, string?>
        {
            ["scopeSha256"] = sealNow, ["revision"] = c.ScopeRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["blockedLots"] = string.Join(",", blocks.Select(b => b.LotId)),
            ["quarantineRequests"] = requests.Count(r => r.Status == RecallQuarantineRequestStatus.Pending).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["notNeeded"] = requests.Count(r => r.Status == RecallQuarantineRequestStatus.NotNeeded).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var saved = await SaveAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, RecallCaseEventKind.ScopeApproved, now, note, counts, signatureId, request.IdempotencyKey, fingerprint),
            Support.Audit(RecallContract.AuditScopeApproved, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey,
                Detail(c, ("scopeSha256", sealNow), ("signatureId", signatureId), ("revision", counts["revision"]), ("blockedLots", counts["blockedLots"]),
                    ("quarantineRequests", counts["quarantineRequests"]))),
            ct, async (session, tx) =>
            {
                await session.InsertLotBlocksAsync(blocks, tx);
                await session.InsertQuarantineRequestsAsync(requests, tx);
                if (approvedSnapshot is not null) await session.InsertSnapshotAsync(approvedSnapshot, tx);
                if (live is not null) await session.InsertAuditAsync(RecallTraceAudit.Of(Support, live, "approveScope", c.Id, c.Number, now, request.IdempotencyKey), tx);
            });
        if (!saved) return RecallSupport.Conflict<RecallCaseDetailDto>();

        // pack §8.4 "Execution": after the commit, one 0175 call per Pending request (partial success); a crash here is the sweeper's
        await _dispatcher.DispatchAsync(requests.Where(r => r.Status == RecallQuarantineRequestStatus.Pending).Select(r => r.Id).ToList(), ct);
        return await Reads.CurrentAsync(c.Id, ct);
    }
}

/// <summary>§4.37 cancelCase — Draft | PendingScopeApproval → Cancelled, reason ≥ 10; frees the root lot for a new case.</summary>
public sealed class CancelRecallCaseHandler : RecallCaseCommandHandler, IRequestHandler<CancelRecallCaseCommand, Response<RecallCaseDetailDto>>
{
    public CancelRecallCaseHandler(IRecallRepository repository, RecallReads reads, RecallSupport support) : base(repository, reads, support)
    {
    }

    public async Task<Response<RecallCaseDetailDto>> Handle(CancelRecallCaseCommand request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        var fingerprint = RecallSupport.Fingerprint(("op", "cancel"), ("id", request.CaseId.ToString("D")), ("reason", reason));
        if (await ReplayCaseAsync(request.CaseId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (RecallCaseRules.Reason(reason) is { } refusal) return RecallSupport.Fail<RecallCaseDetailDto>(refusal);

        var c = await Repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallCaseDetailDto>();
        if (!RecallCaseRules.CanCancel(c.Status))
        {
            return RecallSupport.InvalidTransition<RecallCaseDetailDto>($"A case in status {c.Status} cannot be cancelled (only Draft or PendingScopeApproval).");
        }

        if (c.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallCaseDetailDto>();

        var now = Support.Now();
        c.Status = RecallCaseStatus.Cancelled;
        c.CancelReason = reason;
        c.CancelledBy = Support.UserId;
        c.CancelledAtUtc = now;
        c.PendingAction = null;
        c.ActiveRootKey = null;
        return await WriteCaseAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, RecallCaseEventKind.Cancelled, now, reason, idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint),
            Support.Audit(RecallContract.AuditCaseCancelled, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey, Detail(c, ("reason", reason))), ct);
    }
}

/// <summary>§4.44 startEffectivenessCheck — Active → EffectivenessCheck ("Etkinlik kontrolüne geç").</summary>
public sealed class StartRecallEffectivenessHandler : RecallCaseCommandHandler, IRequestHandler<StartRecallEffectivenessCommand, Response<RecallCaseDetailDto>>
{
    public StartRecallEffectivenessHandler(IRecallRepository repository, RecallReads reads, RecallSupport support) : base(repository, reads, support)
    {
    }

    public async Task<Response<RecallCaseDetailDto>> Handle(StartRecallEffectivenessCommand request, CancellationToken ct)
    {
        var fingerprint = RecallSupport.Fingerprint(("op", "start-effectiveness"), ("id", request.CaseId.ToString("D")));
        if (await ReplayCaseAsync(request.CaseId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var c = await Repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallCaseDetailDto>();
        if (!RecallCaseRules.CanStartEffectiveness(c.Status))
        {
            return RecallSupport.InvalidTransition<RecallCaseDetailDto>($"A case in status {c.Status} cannot start the effectiveness check (Active only).");
        }

        if (c.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallCaseDetailDto>();

        var now = Support.Now();
        c.Status = RecallCaseStatus.EffectivenessCheck;
        c.EffectivenessStartedAtUtc = now;
        return await WriteCaseAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, RecallCaseEventKind.EffectivenessStarted, now, idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint),
            Support.Audit(RecallContract.AuditEffectivenessStarted, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey, Detail(c)), ct);
    }
}

/// <summary>
/// §4.45 completeEffectivenessCheck — computes and stores the effectiveness result (pack §8.6; target = the company's class target, KR-G)
/// under the caller's prepared decision + signature (fail-closed until R4). The completer is recorded (CT-R1: may not close).
/// </summary>
public sealed class CompleteRecallEffectivenessHandler : RecallCaseCommandHandler, IRequestHandler<CompleteRecallEffectivenessCommand, Response<RecallCaseDetailDto>>
{
    private readonly IRecallSignature _signature;
    private readonly RecallSubjectFacts _facts;

    public CompleteRecallEffectivenessHandler(IRecallRepository repository, RecallReads reads, RecallSupport support, IRecallSignature signature,
        RecallSubjectFacts facts)
        : base(repository, reads, support)
    {
        _signature = signature;
        _facts = facts;
    }

    public async Task<Response<RecallCaseDetailDto>> Handle(CompleteRecallEffectivenessCommand request, CancellationToken ct)
    {
        var note = RecallSupport.Trimmed(request.Note);
        var signatureId = RecallSupport.Trimmed(request.SignatureId);
        var fingerprint = RecallSupport.Fingerprint(("op", "complete-effectiveness"), ("id", request.CaseId.ToString("D")), ("note", note), ("signatureId", signatureId));
        if (await ReplayCaseAsync(request.CaseId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (RecallCaseRules.Reason(note, "note") is { } refusal) return RecallSupport.Fail<RecallCaseDetailDto>(refusal);

        var c = await Repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallCaseDetailDto>();
        if (!RecallCaseRules.CanCompleteEffectiveness(c))
        {
            return RecallSupport.InvalidTransition<RecallCaseDetailDto>($"The effectiveness check of a case in status {c.Status} cannot be completed.");
        }

        var now = Support.Now();
        if (PendingRefusal<RecallCaseDetailDto>(c, RecallPendingActionKind.CompleteEffectiveness, PayloadSha(RecallPendingActionKind.CompleteEffectiveness, note, null, null), now)
            is { } pendingRefusal)
        {
            return pendingRefusal;
        }

        if (await SignatureRefusalAsync<RecallCaseDetailDto>(_signature, c, RecallPendingActionKind.CompleteEffectiveness, signatureId, Support.UserId, ct)
            is { } signatureRefusal)
        {
            return signatureRefusal;
        }

        if (c.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallCaseDetailDto>();

        // R4: the SAME computation rc-v1 signed for this prepared completion (RecallSubjectFacts) — what the signer saw is what is stored
        var result = await _facts.LiveEffectivenessAsync(c, null, now, ct);
        c.Effectiveness = result;
        c.EffectivenessCompletedBy = Support.UserId;
        c.EffectivenessCompletedAtUtc = now;
        c.EffectivenessNote = note;
        c.EffectivenessSignatureId = signatureId;
        c.PendingAction = null;
        var totals = string.Join(";", result.Totals.Select(t => $"{t.SkuId:D}/{t.BaseUomId}: {RecallSupport.D(t.Percent)}% open {RecallSupport.D(t.Open)}"));
        return await WriteCaseAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, RecallCaseEventKind.EffectivenessCompleted, now, note, new Dictionary<string, string?> { ["totals"] = totals },
                signatureId, request.IdempotencyKey, fingerprint),
            Support.Audit(RecallContract.AuditEffectivenessCompleted, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey,
                Detail(c, ("totals", totals), ("targetPercent", RecallSupport.D(result.TargetPercent)), ("signatureId", signatureId))), ct);
    }
}

/// <summary>
/// §4.46 closeCase — EffectivenessCheck (completed) → Closed. CT-R1: the effectiveness completer may not close (409 <c>RECALL_SOD_VIOLATION</c>).
/// An open quantity needs the acceptance reason (400 <c>RECALL_OPEN_QUANTITY_ACCEPTANCE_REQUIRED</c>). Prepared decision + signature
/// (fail-closed until R4). The lot blocks of the case are released and the root lot is free for a new case.
/// </summary>
public sealed class CloseRecallCaseHandler : RecallCaseCommandHandler, IRequestHandler<CloseRecallCaseCommand, Response<RecallCaseDetailDto>>
{
    private readonly IRecallSignature _signature;

    public CloseRecallCaseHandler(IRecallRepository repository, RecallReads reads, RecallSupport support, IRecallSignature signature)
        : base(repository, reads, support) => _signature = signature;

    public async Task<Response<RecallCaseDetailDto>> Handle(CloseRecallCaseCommand request, CancellationToken ct)
    {
        var summary = RecallSupport.Trimmed(request.Summary);
        var acceptance = RecallSupport.Trimmed(request.OpenQuantityAcceptance);
        var signatureId = RecallSupport.Trimmed(request.SignatureId);
        var fingerprint = RecallSupport.Fingerprint(("op", "close"), ("id", request.CaseId.ToString("D")), ("summary", summary), ("acceptance", acceptance),
            ("signatureId", signatureId));
        if (await ReplayCaseAsync(request.CaseId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (RecallCaseRules.Reason(summary, "summary", RecallCaseRules.MaxSummaryLength) is { } refusal) return RecallSupport.Fail<RecallCaseDetailDto>(refusal);
        if (acceptance is not null && RecallCaseRules.Reason(acceptance, "openQuantityAcceptance") is { } acceptRefusal)
        {
            return RecallSupport.Fail<RecallCaseDetailDto>(acceptRefusal);
        }

        var c = await Repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallCaseDetailDto>();
        if (!RecallCaseRules.CanClose(c))
        {
            return RecallSupport.InvalidTransition<RecallCaseDetailDto>($"A case in status {c.Status} cannot be closed (the effectiveness check must be completed).");
        }

        if (RecallCaseRules.SeparationOfDuties(c, RecallPendingActionKind.Close, Support.UserId) is { } sod) return RecallSupport.Fail<RecallCaseDetailDto>(sod);
        if (RecallEffectivenessRule.HasOpenQuantity(c.Effectiveness!) && acceptance is null)
        {
            return RecallSupport.Fail<RecallCaseDetailDto>(RecallErrorCodes.OpenQuantityAcceptanceRequired,
                "An open quantity remains; closing needs openQuantityAcceptance (10-1000 characters).", 400);
        }

        var now = Support.Now();
        if (PendingRefusal<RecallCaseDetailDto>(c, RecallPendingActionKind.Close, PayloadSha(RecallPendingActionKind.Close, null, summary, acceptance), now) is { } pendingRefusal)
        {
            return pendingRefusal;
        }

        if (await SignatureRefusalAsync<RecallCaseDetailDto>(_signature, c, RecallPendingActionKind.Close, signatureId, Support.UserId, ct) is { } signatureRefusal)
        {
            return signatureRefusal;
        }

        if (c.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallCaseDetailDto>();

        c.Status = RecallCaseStatus.Closed;
        c.ClosureSummary = summary;
        c.OpenQuantityAcceptance = acceptance;
        c.ClosedBy = Support.UserId;
        c.ClosedAtUtc = now;
        c.ClosureSignatureId = signatureId;
        c.PendingAction = null;
        c.ActiveRootKey = null;
        return await WriteCaseAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, RecallCaseEventKind.Closed, now, summary, new Dictionary<string, string?> { ["openQuantityAcceptance"] = acceptance },
                signatureId, request.IdempotencyKey, fingerprint),
            Support.Audit(RecallContract.AuditCaseClosed, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey,
                Detail(c, ("summary", summary), ("openQuantityAcceptance", acceptance), ("signatureId", signatureId))),
            ct, (session, tx) => session.ReleaseLotBlocksAsync(c.Id, now, "Closed", tx));
    }
}
