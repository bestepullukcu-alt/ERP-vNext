using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>
/// Records → QUARANTINE-BUNDLE v1 wire shapes (contract §2). Stock statuses and movement types are written with the
/// INVENTORY-BUNDLE wire names (<see cref="WireNames"/>) — 0175 has no vocabulary of its own for them. Master-data names
/// (product, SKU code, warehouse / location codes, user names) are not resolved in slice Q1: those optional members are
/// absent, never invented.
/// </summary>
public static class QuarantineMapping
{
    public static string Id(Guid id) => id.ToString("D");

    public static LotInfoDto? Lot(string? lotId, IReadOnlyDictionary<string, Lot> lots)
    {
        if (lotId is null)
        {
            return null;
        }

        return lots.TryGetValue(lotId, out var lot)
            ? new LotInfoDto(lotId, lot.LotNumber, QuarantineSupport.Wire(lot.ExpiryDate), QuarantineSupport.Wire(lot.RetestDate), lot.CoaReference, lot.Status.ToString())
            : new LotInfoDto(lotId, lotId, null, null, null, null); // a typed (unregistered) lot text names itself
    }

    public static int WaitDays(DateTimeOffset entered, DateTimeOffset? decided, DateTimeOffset now)
        => Math.Max(0, (int)Math.Floor(((decided ?? now) - entered).TotalDays));

    public static HoldRefDto HoldRef(
        QuarantineHold hold, IReadOnlyDictionary<string, Lot> lots, int targetDays, ActiveDecisionDto? active, DateTimeOffset now)
    {
        var wait = WaitDays(hold.EnteredAtUtc, hold.DecidedAtUtc, now);
        return new HoldRefDto
        {
            HoldId = Id(hold.Id),
            HoldNumber = hold.HoldNumber,
            LegalEntityId = Id(hold.LegalEntityId!.Value),
            Item = new ItemRefDto(Id(hold.ItemId), Id(hold.SkuId), WireNames.ToWire(hold.SkuLevel)),
            Lot = Lot(hold.LotId, lots),
            Warehouse = new WarehouseRefDto(hold.WarehouseId),
            Location = new LocationRefDto(hold.LocationId),
            StockStatus = WireNames.ToWire(hold.StockStatus),
            Quantity = hold.Quantity,
            BaseUom = new UomRefDto(hold.BaseUomId, hold.BaseUomId),
            Source = hold.Source.ToString(),
            SourceDocument = hold.SourceDocument is { } d ? new SourceDocumentDto(d.Module, d.Type, d.Id, d.Number, d.Text) : null,
            EnteredAtUtc = QuarantineSupport.Wire(hold.EnteredAtUtc),
            CreatedBy = new UserRefDto(Id(hold.CreatedBy)),
            ReviewState = hold.ReviewState.ToString(),
            Status = hold.Status.ToString(),
            WaitDays = wait,
            TargetDays = targetDays,
            Late = hold.Status != HoldStatus.Decided && wait > targetDays,
            ActiveDecision = active,
            Version = hold.Version
        };
    }

    public static HoldDetailDto HoldDetail(
        HoldRefDto head, QuarantineHold hold, IReadOnlyList<QuarantineHistoryEntry> history, IReadOnlyList<InventoryTransaction> movements,
        IReadOnlyList<DecisionRefDto> decisions) => new()
    {
        HoldId = head.HoldId,
        HoldNumber = head.HoldNumber,
        LegalEntityId = head.LegalEntityId,
        Item = head.Item,
        Lot = head.Lot,
        Warehouse = head.Warehouse,
        Location = head.Location,
        StockStatus = head.StockStatus,
        Quantity = head.Quantity,
        BaseUom = head.BaseUom,
        Source = head.Source,
        SourceDocument = head.SourceDocument,
        EnteredAtUtc = head.EnteredAtUtc,
        CreatedBy = head.CreatedBy,
        ReviewState = head.ReviewState,
        Status = head.Status,
        WaitDays = head.WaitDays,
        TargetDays = head.TargetDays,
        Late = head.Late,
        ActiveDecision = head.ActiveDecision,
        Version = head.Version,
        AnalysisNote = hold.AnalysisNote,
        Attachments = hold.Attachments.Select(a => new AttachmentDto(a.Name, a.DmsRef)).ToList(),
        ReviewStartedAtUtc = QuarantineSupport.Wire(hold.ReviewStartedAtUtc),
        ReviewStartedBy = hold.ReviewStartedBy is { } by ? new UserRefDto(Id(by)) : null,
        DecidedAtUtc = QuarantineSupport.Wire(hold.DecidedAtUtc),
        History = history.Select(History).ToList(),
        Movements = movements.Select(Movement).ToList(),
        Decisions = decisions
    };

    public static HistoryEntryDto History(QuarantineHistoryEntry entry)
        => new(QuarantineSupport.Wire(entry.AtUtc), new UserRefDto(Id(entry.ByUserId)), entry.Action.ToString(), entry.Reason,
            entry.DecisionId is { } d ? Id(d) : null, entry.SignatureId, entry.ApproverSignatureId);

    public static MovementLinkDto Movement(InventoryTransaction t)
        => new(Id(t.Id), t.TransactionNumber, WireNames.ToWire(t.MovementType), QuarantineSupport.Wire(t.PostingDate),
            t.FromStockStatus is { } f ? WireNames.ToWire(f) : null, t.ToStockStatus is { } s ? WireNames.ToWire(s) : null,
            t.FromLocationId, t.ToLocationId, t.BaseQuantity);

    public static DecisionDto Decision(QuarantineDecision decision, IReadOnlyList<QuarantineDecisionEvent> events)
    {
        var ordered = events.OrderBy(e => e.Sequence).ToList();
        var state = QuarantineFacts.StateOf(ordered);
        var started = ordered.LastOrDefault(e => e.Kind == DecisionEventKind.WorkflowStarted);
        var outcome = ordered.LastOrDefault(e => e.Kind is DecisionEventKind.Approved or DecisionEventKind.Rejected
            or DecisionEventKind.Cancelled or DecisionEventKind.TimedOut);
        var applied = ordered.LastOrDefault(e => e.Kind == DecisionEventKind.Applied);
        var failure = state is DecisionState.ApplyFailed or DecisionState.StartFailed or DecisionState.FourEyesViolation ? ordered.Last() : null;

        return new DecisionDto
        {
            DecisionId = Id(decision.Id),
            DecisionNumber = decision.DecisionNumber,
            Kind = decision.Kind.ToString(),
            HoldId = decision.HoldId is { } h ? Id(h) : null,
            BlockedStockId = decision.BlockedStockId is { } b ? Id(b) : null,
            LotId = decision.LotId,
            LegalEntityId = Id(decision.LegalEntityId!.Value),
            FromStockStatus = WireNames.ToWire(decision.FromStockStatus),
            Lines = decision.Lines.OrderBy(l => l.LineNo)
                .Select(l => new DecisionLineDto(l.LineNo, l.Decision.ToString(), l.Quantity, WireNames.ToWire(l.ToStockStatus))).ToList(),
            BlockReasonCode = decision.BlockReasonCode,
            Reason = decision.Reason,
            CoaChecked = decision.CoaChecked,
            CoaReference = decision.CoaReference,
            NewRetestDate = QuarantineSupport.WireDate(decision.NewRetestDate),
            Disposition = decision.Disposition is { } d
                ? new DispositionDto(d.Kind.ToString(), d.Quantity, d.SupplierId is { } s ? Id(s) : null, d.ReturnDocumentNo, d.DestructionMethod,
                    QuarantineSupport.WireDate(d.DestructionDate), d.SampleLocationId)
                : null,
            LotStatusTarget = decision.LotStatusTarget,
            PlannedMovements = QuarantineStockEffects.Plan(decision)
                .Select(m => new PlannedMovementDto(m.LineNo, WireNames.ToWire(m.MovementType),
                    m.FromStockStatus is { } f ? WireNames.ToWire(f) : null, m.ToStockStatus is { } t ? WireNames.ToWire(t) : null,
                    m.FromLocationId, m.ToLocationId, m.Quantity))
                .ToList(),
            Decider = new UserRefDto(Id(decision.DeciderUserId)),
            DecidedAtUtc = QuarantineSupport.Wire(decision.DecidedAtUtc),
            EsignActionCode = EsignActionCode(decision),
            DeciderSignatureId = ordered.LastOrDefault(e => e.Kind == DecisionEventKind.Submitted)?.DeciderSignatureId,
            SignatureRequired = ordered.LastOrDefault(e => e.Kind == DecisionEventKind.Submitted)?.SignatureRequired,
            State = state.ToString(),
            Workflow = started is null && outcome is null
                ? null
                : new WorkflowInfoDto(started?.WorkflowInstanceId, QuarantineContract.WorkflowTemplateCode, outcome?.WorkflowOutcome,
                    outcome?.ApproverUserId is { } a ? new UserRefDto(Id(a)) : null, outcome?.ApproverSignatureId,
                    outcome is null ? null : QuarantineSupport.Wire(outcome.AtUtc)),
            AppliedMovementIds = applied?.MovementIds ?? (failure?.MovementIds ?? []),
            Error = failure?.ErrorCode is { } code ? new ErrorInfoDto(code, failure.Detail) : null
        };
    }

    /// <summary>Pack §8 signable actions: a partial decision uses the strictest row of its lines (Release &gt; Reject &gt; Block).</summary>
    public static string EsignActionCode(QuarantineDecision decision) => decision.Kind switch
    {
        DecisionKind.Disposition => "quarantine.blocked.dispose",
        DecisionKind.Unblock => "quarantine.blocked.unblock",
        DecisionKind.LotRelease => "quarantine.lot.release",
        _ => decision.Lines.Any(l => l.Decision == LineDecision.Release) ? "quarantine.hold.release"
            : decision.Lines.Any(l => l.Decision == LineDecision.Reject) ? "quarantine.hold.reject"
            : "quarantine.hold.block"
    };

    public static ActiveDecisionDto? Active(Guid? activeDecisionId, IReadOnlyDictionary<Guid, (QuarantineDecision Decision, DecisionState State)> decisions)
        => activeDecisionId is { } id && decisions.TryGetValue(id, out var d)
            ? new ActiveDecisionDto(Id(id), d.Decision.DecisionNumber, d.State.ToString())
            : null;

    public static IReadOnlyList<string> AllowedActions(BlockedStock blocked, bool lotHeld)
    {
        if (blocked.Status != BlockedStatus.Open || blocked.ActiveDecisionId is not null)
        {
            return [];
        }

        if (lotHeld)
        {
            return ["Dispose:Destroy"]; // CT-D4 / V8
        }

        var actions = new List<string> { "Dispose:Destroy", "Dispose:ReturnToSupplier", "Dispose:Rework", "Dispose:Sample" };
        if (blocked.Reviewed)
        {
            actions.Add("Unblock");
        }
        else
        {
            actions.Add("CompleteReview");
        }

        return actions;
    }

    public static BlockedStockRefDto BlockedRef(
        BlockedStock blocked, IReadOnlyDictionary<string, Lot> lots, IReadOnlyDictionary<string, BlockReason> reasons, string language,
        OriginRefDto? origin, ActiveDecisionDto? active)
    {
        var lotHeld = blocked.LotId is not null && lots.TryGetValue(blocked.LotId, out var lot) && QuarantineFacts.IsHeld(lot);
        return new BlockedStockRefDto
        {
            BlockedId = Id(blocked.Id),
            BlockedNumber = blocked.BlockedNumber,
            LegalEntityId = Id(blocked.LegalEntityId!.Value),
            Item = new ItemRefDto(Id(blocked.ItemId), Id(blocked.SkuId), WireNames.ToWire(blocked.SkuLevel)),
            Lot = Lot(blocked.LotId, lots),
            Warehouse = new WarehouseRefDto(blocked.WarehouseId),
            Location = new LocationRefDto(blocked.LocationId),
            Quantity = blocked.Quantity,
            BaseUom = new UomRefDto(blocked.BaseUomId, blocked.BaseUomId),
            BlockReason = blocked.BlockReasonCode is { } code
                ? new BlockReasonRefDto(code, reasons.TryGetValue(code, out var r) ? r.Names.GetValueOrDefault(language) ?? r.Names.GetValueOrDefault("en") : null)
                : null,
            Note = blocked.Note,
            BlockedAtUtc = QuarantineSupport.Wire(blocked.BlockedAtUtc),
            Origin = blocked.Origin.ToString(),
            OriginRef = origin,
            PendingDisposition = blocked.PendingDisposition,
            Reviewed = blocked.Reviewed,
            Status = blocked.Status.ToString(),
            AllowedActions = AllowedActions(blocked, lotHeld),
            ActiveDecision = active,
            Version = blocked.Version
        };
    }

    public static SettingsDto Settings(QuarantineSettings settings) => new()
    {
        LegalEntityId = settings.LegalEntityId is { } le ? Id(le) : null,
        WaitTargetDays = settings.WaitTargetDays,
        FourEyes = settings.FourEyes,
        ApproverCandidates = settings.ApproverCandidates,
        SampleLocations = settings.SampleLocations,
        LotReleaseRequiresApproval = settings.LotReleaseRequiresApproval,
        ReconcileAfterSeconds = settings.ReconcileAfterSeconds,
        Version = settings.Version
    };

    public static BlockReasonDto BlockReason(BlockReason reason)
        => new(reason.Code, reason.Names, reason.IsActive, reason.FirstUsedAtUtc is not null);
}
