using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Expiry.Services;

/// <summary>Records → FEFO-BUNDLE v1 wire shapes. No rule here: <c>late</c> / <c>dueSoon</c> come from <see cref="ExpiryExceptionRules"/>.</summary>
public static class ExpiryMapping
{
    public static ExpiryPolicyDto Policy(ExpiryPolicy policy, Guid legalEntityId, bool isDefault) => new(
        legalEntityId.ToString("D"),
        isDefault,
        policy.ApproachingDays,
        policy.CriticalDays,
        policy.FefoOutboundMode.ToString(),
        policy.BelowMinRemainingMode.ToString(),
        policy.DateOverrideMode.ToString(),
        ExpiryWire.AllTypeCodes.ToDictionary(t => t, t => ExpiryPolicyRules.SlaHoursFor(policy, Parse(t)), StringComparer.Ordinal),
        policy.ExpiredOnHandThresholdDays,
        isDefault ? null : ExpirySupport.Wire(policy.UpdatedAt),
        isDefault || policy.UpdatedBy is null ? null : User(policy.UpdatedBy.Value),
        policy.Version);

    public static ExpiryPolicyHistoryEntryDto History(ExpiryPolicyHistoryEntry entry) => new(
        entry.Field, entry.OldValue, entry.NewValue, entry.Reason, User(entry.ChangedBy), ExpirySupport.Wire(entry.ChangedAtUtc), entry.PolicyVersion);

    public static ExpiryExceptionRefDto Ref(FefoExceptionCase c, IReadOnlyDictionary<Guid, Location> warehouses, DateTimeOffset now) => new(
        c.Id.ToString("D"),
        c.Number,
        ExpiryWire.Code(c.Type),
        c.Origin.ToString(),
        c.Status.ToString(),
        c.Resolution?.ToString(),
        Item(c),
        Lot(c),
        Warehouse(c, warehouses),
        c.Quantity,
        c.BaseUomId,
        ExpirySupport.Wire(c.OpenedAtUtc),
        ExpirySupport.Wire(c.DueAtUtc),
        ExpiryExceptionRules.IsLate(c, now),
        ExpiryExceptionRules.IsDueSoon(c, now),
        c.AssigneeUserId is { } assignee ? User(assignee) : null,
        c.Version);

    public static ExpiryExceptionDetailDto Detail(
        FefoExceptionCase c, IReadOnlyList<FefoExceptionEvent> events, IReadOnlyDictionary<Guid, Location> warehouses, DateTimeOffset now)
    {
        var head = Ref(c, warehouses, now);
        return new ExpiryExceptionDetailDto(
            head.ExceptionId, head.Number, head.Type, head.Origin, head.Status, head.Resolution, head.Item, head.Lot, head.Warehouse,
            head.Quantity, head.BaseUomId, head.OpenedAtUtc, head.DueAtUtc, head.Late, head.DueSoon, head.Assignee, head.Version,
            c.OpenedBy is { } opener ? User(opener) : null,
            c.Description,
            c.SourceMovement is { } m ? new ExpirySourceMovementDto(m.TransactionId, m.MovementType, m.SourceModule, m.SourceDocumentId) : null,
            new SortedDictionary<string, string>(c.Facts, StringComparer.Ordinal),
            c.ResolutionReason,
            c.ResolutionSignatureId,
            ExpirySupport.Wire(c.ResolvedAtUtc),
            c.ResolvedBy is { } resolver ? User(resolver) : null,
            ExpirySupport.Wire(c.ClosedAtUtc),
            c.ClosedBy is { } closer ? User(closer) : null,
            c.PendingResolution is { } p && p.ExpiresAtUtc > now
                ? new ExpiryPendingResolutionDto(p.Resolution.ToString(), p.Reason, User(p.SetBy), ExpirySupport.Wire(p.SetAtUtc), ExpirySupport.Wire(p.ExpiresAtUtc))
                : null,
            events.OrderBy(e => e.Sequence).Select((e, i) => Event(e, i + 1)).ToList(),
            new ExpiryExceptionLinksDto(c.Facts.GetValueOrDefault("holdId"), c.Facts.GetValueOrDefault("reversalTransactionId")));
    }

    /// <summary><paramref name="seq"/> is the 1-based position in the case's stream (the stored sequence is a process clock).</summary>
    public static ExpiryExceptionEventDto Event(FefoExceptionEvent e, long seq) => new(
        seq,
        e.Action.ToString(),
        e.ActorUserId is { } actor ? User(actor) : null,
        ExpirySupport.Wire(e.AtUtc),
        e.Text,
        e.Resolution?.ToString(),
        e.SignatureId);

    public static ExpiryUserRefDto User(Guid id) => new(id.ToString("D"));

    private static ExpiryItemRefDto Item(FefoExceptionCase c) => new(c.ItemId.ToString("D"), c.SkuId.ToString("D"), WireNames.ToWire(c.SkuLevel));

    private static ExpiryLotRefDto Lot(FefoExceptionCase c) => new(c.LotId, c.LotNumber, ExpirySupport.WireDate(c.ExpiryDate), ExpirySupport.WireDate(c.RetestDate));

    private static ExpiryWarehouseRefDto? Warehouse(FefoExceptionCase c, IReadOnlyDictionary<Guid, Location> warehouses)
    {
        if (c.WarehouseId is null) return null;
        return Guid.TryParse(c.WarehouseId, out var id) && warehouses.TryGetValue(id, out var location)
            ? new ExpiryWarehouseRefDto(c.WarehouseId, location.Code, location.Name)
            : new ExpiryWarehouseRefDto(c.WarehouseId, null, null);
    }

    private static Domain.Expiry.Enums.ExpiryExceptionType Parse(string code)
        => ExpiryWire.TryParseType(code, out var type) ? type : throw new ArgumentOutOfRangeException(nameof(code), code, null);
}
