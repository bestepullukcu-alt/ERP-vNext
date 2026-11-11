using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Recall.Services;

/// <summary>
/// The read side shared by the recall handlers: a case + its scope + its records, as the wire detail; the effectiveness inputs (pack §8.6);
/// the customers of an approved scope. No rule of its own: inclusion is <see cref="RecallScopeRules"/>, effectiveness <see cref="RecallEffectivenessRule"/>.
/// </summary>
public sealed class RecallReads
{
    private readonly IRecallRepository _repository;
    private readonly RecallSupport _support;
    private readonly RecallCustomerDisplay _customers;

    public RecallReads(IRecallRepository repository, RecallSupport support, RecallCustomerDisplay customers)
    {
        _repository = repository;
        _support = support;
        _customers = customers;
    }

    public async Task<RecallCaseDetailDto> DetailAsync(RecallCase c, CancellationToken ct)
    {
        var items = await _repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct);
        var notifications = await _repository.NotificationsAsync(c.Id, ct);
        var consumptions = await _repository.ConsumptionsAsync(c.Id, ct);
        var segregations = await _repository.SegregationsAsync(c.Id, ct);
        var submitted = c.SubmittedSnapshotId is { } sid ? await _repository.FindSnapshotAsync(sid, ct) : null;
        var approved = c.ApprovedSnapshotId is { } aid ? await _repository.FindSnapshotAsync(aid, ct) : null;
        return RecallMapping.Detail(c, items, notifications, Effective(consumptions), segregations, _support.Now()) with
        {
            SubmittedSnapshot = Trace.RecallTraceService.SnapshotRef(submitted),
            ApprovedSnapshot = Trace.RecallTraceService.SnapshotRef(approved),
            PendingRevision = c.PendingRevision is { } r
                ? new RecallPendingRevisionDto(r.Revision, r.Reason, r.AddedNodeKeys, new RecallUserAtDto(r.RevisedBy.ToString("D"), RecallSupport.Wire(r.RevisedAtUtc)))
                : null
        };
    }

    public async Task<Response<RecallCaseDetailDto>> CurrentAsync(Guid caseId, CancellationToken ct)
        => await _repository.FindCaseAsync(caseId, ct) is { } c
            ? Response<RecallCaseDetailDto>.Success(await DetailAsync(c, ct))
            : RecallSupport.CaseNotFound<RecallCaseDetailDto>();

    /// <summary>R2: the scope with the snapshot that governs it (the approved one once approved, else the submitted one). FU01: customer page 1.</summary>
    public Task<RecallScopeViewDto> ScopeAsync(RecallCase c, CancellationToken ct) => ScopeAsync(c, 1, ct);

    /// <summary>MOD-0177-FU01 (§8.6): the scope with the CRM names of the requested customer page (Page mode — never fails the read).</summary>
    public async Task<RecallScopeViewDto> ScopeAsync(RecallCase c, int customerPage, CancellationToken ct)
    {
        var snapshotId = c.ApprovedSnapshotId ?? c.SubmittedSnapshotId;
        var snapshot = snapshotId is { } id ? await _repository.FindSnapshotAsync(id, ct) : null;
        var items = await _repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct);
        var facts = RecallCustomerDisplay.Facts(items);
        var names = await _customers.PageAsync(facts.Values, customerPage, ct);
        return RecallMapping.Scope(c, items, facts, names) with { Snapshot = Trace.RecallTraceService.SnapshotRef(snapshot) };
    }

    public Task<Response<RecallScopeViewDto>> CurrentScopeAsync(Guid caseId, CancellationToken ct) => CurrentScopeAsync(caseId, 1, ct);

    public async Task<Response<RecallScopeViewDto>> CurrentScopeAsync(Guid caseId, int customerPage, CancellationToken ct)
        => await _repository.FindCaseAsync(caseId, ct) is { } c
            ? Response<RecallScopeViewDto>.Success(await ScopeAsync(c, customerPage, ct))
            : RecallSupport.CaseNotFound<RecallScopeViewDto>();

    /// <summary>
    /// MOD-0177-FU01 (§8.8) — the sales movements behind a Shipment item: the same lot and <c>DocumentId ?? "TXN-" + TransactionId</c> = the item's
    /// <c>SourceDocumentId</c> (the getCaseCustomers rule, moved here unchanged — the read order stays the ledger's).
    /// </summary>
    public static IReadOnlyList<TraceSalesIssue> MovementsBehind(RecallScopeItem item, IReadOnlyList<TraceSalesIssue> movements)
        => movements.Where(m => string.Equals(m.LotId, item.LotId, StringComparison.Ordinal)
                                && string.Equals(m.DocumentId ?? "TXN-" + m.TransactionId, item.SourceDocumentId, StringComparison.Ordinal)).ToList();

    /// <summary>The declarations that count: those no later declaration supersedes.</summary>
    public static IReadOnlyList<RecallConsumptionDeclaration> Effective(IReadOnlyList<RecallConsumptionDeclaration> all)
    {
        var superseded = all.Where(d => d.SupersedesId is not null).Select(d => d.SupersedesId!.Value).ToHashSet();
        return all.Where(d => !superseded.Contains(d.Id)).ToList();
    }

    /// <summary>The effectively included shipment items of the scope (the "shipped" side of effectiveness and the case's customers).</summary>
    public static IReadOnlyList<RecallScopeItem> Shipments(IReadOnlyList<RecallScopeItem> items)
    {
        var byKey = items.ToDictionary(i => i.NodeKey, StringComparer.Ordinal);
        return items.Where(i => i.Kind == RecallScopeItemKind.Shipment && RecallScopeRules.EffectivelyIncluded(i, byKey)).ToList();
    }

    /// <summary>Pack §8.6 inputs. R1: returned = 0 (return links are R3).</summary>
    public static IEnumerable<RecallEffectivenessRule.Input> EffectivenessInputs(
        IReadOnlyList<RecallScopeItem> items, IReadOnlyList<RecallConsumptionDeclaration> effectiveConsumptions, IReadOnlyList<RecallCustomerNotification> notifications,
        IReadOnlyList<RecallReturnLink>? returns = null)
    {
        var notified = notifications.Select(n => n.CustomerRef).ToHashSet(StringComparer.Ordinal);
        // R3 (pack §8.6 KR-H): returned = Σ linked returns per (customer, SKU, base unit)
        foreach (var r in returns ?? [])
        {
            yield return new RecallEffectivenessRule.Input(r.CustomerRef, r.SkuId, r.BaseUomId, 0m, r.Quantity, 0m, notified.Contains(r.CustomerRef));
        }

        foreach (var s in Shipments(items))
        {
            yield return new RecallEffectivenessRule.Input(s.CustomerRef!, s.SkuId, s.BaseUomId!, s.Quantity ?? 0m, 0m, 0m, notified.Contains(s.CustomerRef!));
        }

        foreach (var d in effectiveConsumptions)
        {
            yield return new RecallEffectivenessRule.Input(d.CustomerRef, d.SkuId, d.BaseUomId, 0m, 0m, d.Quantity, notified.Contains(d.CustomerRef));
        }
    }
}

/// <summary>Records → RECALL-BUNDLE v1 wire shapes.</summary>
public static class RecallMapping
{
    public static RecallSettingsDto Settings(RecallSettings s, Guid legalEntityId, bool isDefault) => new(
        legalEntityId.ToString("D"),
        isDefault,
        s.DrillTargetHours,
        s.DrillFrequencyWarningDays,
        RecallSettingsRules.Classes.ToDictionary(c => c, c => RecallSettingsRules.TargetFor(s, int.Parse(c, System.Globalization.CultureInfo.InvariantCulture)), StringComparer.Ordinal),
        s.TraceMaxDepth,
        s.TraceMaxNodes,
        isDefault ? null : RecallSupport.Wire(s.UpdatedAt),
        isDefault || s.UpdatedBy is null ? null : User(s.UpdatedBy.Value),
        s.Version);

    public static RecallSettingsHistoryEntryDto History(RecallSettingsHistoryEntry e) => new(
        e.Field, e.OldValue, e.NewValue, e.Reason, User(e.ChangedBy), RecallSupport.Wire(e.ChangedAtUtc), e.SettingsVersion, e.SignatureId);

    public static RecallCaseRefDto Ref(RecallCase c, IReadOnlyList<RecallScopeItem> items) => new(
        c.Id.ToString("D"),
        c.Number,
        RecallContract.CaseTypeReal,
        c.Status.ToString(),
        c.Class,
        c.Level.ToString(),
        new RecallItemRefDto(c.ItemId.ToString("D"), c.SkuId.ToString("D"), WireNames.ToWire(c.SkuLevel)),
        new RecallLotRefDto(c.RootLotId, c.RootLotNumber),
        RecallSupport.Wire(c.OpenedAtUtc),
        User(c.OwnerUserId),
        Affected(items),
        Progress(items),
        c.Version);

    public static RecallCaseDetailDto Detail(
        RecallCase c, IReadOnlyList<RecallScopeItem> items, IReadOnlyList<RecallCustomerNotification> notifications,
        IReadOnlyList<RecallConsumptionDeclaration> effectiveConsumptions, IReadOnlyList<RecallSegregationConfirmation> segregations, DateTimeOffset now)
    {
        var head = Ref(c, items);
        var customers = RecallReads.Shipments(items).Select(s => s.CustomerRef!).Distinct(StringComparer.Ordinal).ToList();
        var notified = notifications.Select(n => n.CustomerRef).Where(customers.Contains).Distinct(StringComparer.Ordinal).Count();
        return new RecallCaseDetailDto(
            head.CaseId, head.Number, head.Type, head.Status, head.Class, head.Level, head.Item, head.RootLot, head.OpenedAtUtc, head.Owner,
            head.AffectedQuantity, head.Progress, head.Version,
            c.Reason,
            c.ReasonReference,
            c.Direction.ToString(),
            c.ScopeRevision,
            Seal(c.SubmittedScope),
            Seal(c.ApprovedScope),
            UserAt(c.ScopeSubmittedBy, c.ScopeSubmittedAtUtc),
            UserAt(c.ScopeApprovedBy, c.ScopeApprovedAtUtc),
            c.ScopeApprovalSignatureId,
            RecallSupport.Wire(c.EffectivenessStartedAtUtc),
            c.Effectiveness is { } e ? Effectiveness(e, Customers(RecallCustomerDisplay.Facts(items), RecallCustomerDisplay.Resolution.None)) : null,
            UserAt(c.EffectivenessCompletedBy, c.EffectivenessCompletedAtUtc),
            c.EffectivenessSignatureId,
            c.Status == RecallCaseStatus.Closed && c.ClosedBy is { } closer
                ? new RecallClosureDto(c.ClosureSummary ?? string.Empty, c.OpenQuantityAcceptance, new RecallUserAtDto(closer.ToString("D"), RecallSupport.Wire(c.ClosedAtUtc!.Value)), c.ClosureSignatureId)
                : null,
            c.Status == RecallCaseStatus.Cancelled && c.CancelledBy is { } canceller
                ? new RecallCancellationDto(c.CancelReason ?? string.Empty, new RecallUserAtDto(canceller.ToString("D"), RecallSupport.Wire(c.CancelledAtUtc!.Value)))
                : null,
            c.PendingAction is { } p && p.ExpiresAtUtc > now ? new RecallPendingActionDto(p.Action.ToString(), User(p.SetBy), RecallSupport.Wire(p.ExpiresAtUtc)) : null,
            new RecallCaseCountsDto(items.Count, RecallScopeRules.Excluded(items).Count, customers.Count, notified, effectiveConsumptions.Count, segregations.Count));
    }

    public static RecallScopeViewDto Scope(RecallCase c, IReadOnlyList<RecallScopeItem> items)
        => Scope(c, items, RecallCustomerDisplay.Facts(items), RecallCustomerDisplay.Resolution.None);

    /// <summary>MOD-0177-FU01: the scope view with each Shipment item's customer display (its own source and warning, the page's names).</summary>
    public static RecallScopeViewDto Scope(RecallCase c, IReadOnlyList<RecallScopeItem> items, IReadOnlyDictionary<string, RecallCustomerDisplay.KeyFacts> facts,
        RecallCustomerDisplay.Resolution names)
    {
        var byKey = items.ToDictionary(i => i.NodeKey, StringComparer.Ordinal);
        return new RecallScopeViewDto(
            c.Id.ToString("D"),
            c.ScopeRevision,
            RecallCaseRules.CanEditScope(c.Status),
            RecallScopeRules.Excluded(items).Count,
            RecallScopeRules.Sha256(items, c.ScopeRevision),
            items.OrderBy(i => i.NodeKey, StringComparer.Ordinal)
                .Select(i => Item(i, RecallScopeRules.EffectivelyIncluded(i, byKey)) with { Customer = ItemCustomer(i, names) }).ToList(),
            c.Version);
    }

    /// <summary>A Shipment item's customer: the item's OWN source and warning (§8.7 ScopeItem), the key's resolution.</summary>
    private static RecallCustomerDisplayDto? ItemCustomer(RecallScopeItem i, RecallCustomerDisplay.Resolution names)
    {
        if (i.Kind != RecallScopeItemKind.Shipment || i.CustomerRef is null) return null;
        var own = new RecallCustomerDisplay.KeyFacts(i.CustomerRef, i.CustomerRefSource,
            i.CustomerRefSource == RecallCustomerRefSource.Crm && i.NoCrmWarning);
        return RecallCustomerDisplay.Display(own, names, i.CustomerRef); // a row without a CRM id shows its own stored text
    }

    /// <summary>MOD-0177-FU01: the customer display of a key in a table that is not the customer list (effectiveness, return links).</summary>
    public static Func<string?, RecallCustomerDisplayDto?> Customers(IReadOnlyDictionary<string, RecallCustomerDisplay.KeyFacts> facts, RecallCustomerDisplay.Resolution names)
        => key => key is null ? null : RecallCustomerDisplay.Display(RecallCustomerDisplay.Of(facts, key), names, key);

    public static RecallScopeItemDto Item(RecallScopeItem i, bool effective) => new(
        i.NodeKey,
        i.ParentKey,
        i.Kind.ToString(),
        i.IsRoot,
        new RecallLotRefDto(i.LotId, i.LotNumber),
        new RecallItemRefDto(i.ItemId.ToString("D"), i.SkuId.ToString("D"), WireNames.ToWire(i.SkuLevel)),
        i.WarehouseId,
        i.LocationId,
        i.StockStatus is { } status ? WireNames.ToWire(status) : null,
        i.SourceDocumentId,
        i.CustomerRef,
        i.Quantity,
        i.BaseUomId,
        i.Included,
        effective,
        i.Included ? null : i.ExclusionReason,
        i.Origin.ToString(),
        i.IsNew);

    public static RecallEffectivenessDto Effectiveness(RecallEffectivenessResult e, Func<string?, RecallCustomerDisplayDto?>? customer = null) => new(
        e.Class, e.TargetPercent, e.Rows.Select(r => Row(r) with { Customer = customer?.Invoke(r.CustomerRef) }).ToList(), e.Totals.Select(Row).ToList(),
        RecallSupport.Wire(e.ComputedAtUtc));

    public static RecallTimelineEntryDto Timeline(RecallCaseEvent e, long seq) => new(
        seq,
        e.Kind.ToString(),
        e.ActorUserId is { } actor ? User(actor) : null,
        RecallSupport.Wire(e.AtUtc),
        e.Text,
        new SortedDictionary<string, string>(e.Refs.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value!), StringComparer.Ordinal),
        e.SignatureId);

    public static RecallNotificationDto Notification(RecallCustomerNotification n) => new(
        n.Id.ToString("D"), n.CaseId.ToString("D"), n.CustomerRef, RecallSupport.WireDate(n.NotifiedOn), n.Channel.ToString(), n.Note,
        n.SupersedesId?.ToString("D"), new RecallUserAtDto(n.RecordedBy.ToString("D"), RecallSupport.Wire(n.RecordedAtUtc)));

    public static RecallConsumptionDto Consumption(RecallConsumptionDeclaration d) => new(
        d.Id.ToString("D"), d.CaseId.ToString("D"), d.CustomerRef, d.SkuId.ToString("D"), WireNames.ToWire(d.SkuLevel), d.BaseUomId, d.Quantity,
        RecallSupport.WireDate(d.DeclaredOn), d.Evidence, d.SupersedesId?.ToString("D"), new RecallUserAtDto(d.RecordedBy.ToString("D"), RecallSupport.Wire(d.RecordedAtUtc)));

    public static RecallSegregationDto Segregation(RecallSegregationConfirmation s) => new(
        s.Id.ToString("D"), s.CaseId.ToString("D"), s.NodeKey, s.WarehouseId, s.LocationId, s.Quantity, s.Note,
        new RecallUserAtDto(s.ConfirmedBy.ToString("D"), RecallSupport.Wire(s.ConfirmedAtUtc)));

    public static RecallUserRefDto User(Guid id) => new(id.ToString("D"));

    private static RecallUserAtDto? UserAt(Guid? user, DateTimeOffset? at)
        => user is { } u && at is { } a ? new RecallUserAtDto(u.ToString("D"), RecallSupport.Wire(a)) : null;

    private static RecallScopeSealDto? Seal(RecallScopeSeal? s)
        => s is null ? null : new RecallScopeSealDto(s.Revision, s.Sha256, s.CanonicalVersion, s.ItemCount, RecallSupport.Wire(s.ComputedAtUtc));

    private static RecallEffectivenessRowDto Row(RecallEffectivenessRow r) => new(
        r.CustomerRef, r.SkuId.ToString("D"), r.BaseUomId, r.Shipped, r.Returned, r.Consumed, r.Open, r.Percent, r.Met, r.Notified);

    /// <summary>Effectively included Stock + Shipment quantities per SKU + base unit (never summed across units).</summary>
    private static IReadOnlyList<RecallQuantityDto> Affected(IReadOnlyList<RecallScopeItem> items)
    {
        var byKey = items.ToDictionary(i => i.NodeKey, StringComparer.Ordinal);
        return items.Where(i => i.Kind is RecallScopeItemKind.Stock or RecallScopeItemKind.Shipment && i.Quantity is not null && i.BaseUomId is not null
                                && RecallScopeRules.EffectivelyIncluded(i, byKey))
            .GroupBy(i => (i.SkuId, i.BaseUomId!))
            .OrderBy(g => g.Key.SkuId).ThenBy(g => g.Key.Item2, StringComparer.Ordinal)
            .Select(g => new RecallQuantityDto(g.Key.SkuId.ToString("D"), g.Key.Item2, g.Sum(i => i.Quantity!.Value)))
            .ToList();
    }

    /// <summary>Returned / shipped per SKU + unit (R1: returned 0 — return links are R3).</summary>
    private static IReadOnlyList<RecallProgressDto> Progress(IReadOnlyList<RecallScopeItem> items)
        => RecallReads.Shipments(items)
            .Where(s => s.Quantity is not null && s.BaseUomId is not null)
            .GroupBy(s => (s.SkuId, s.BaseUomId!))
            .OrderBy(g => g.Key.SkuId).ThenBy(g => g.Key.Item2, StringComparer.Ordinal)
            .Select(g => new RecallProgressDto(g.Key.SkuId.ToString("D"), g.Key.Item2, 0m, g.Sum(s => s.Quantity!.Value), 0m))
            .ToList();
}
