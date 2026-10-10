using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Commands;

// MOD-0290-FU04 §21 (AUD-001 path b, `mdm-merkezi-iletim`): every item write command is marked from day one; a refused
// one is forwarded too (Outcome = Failed). The item is tenant-wide master data (Decision 6): these commands are not
// legal-entity scope mutations and live outside the product scope inventory (CT decision 5).
public static class ItemAudit
{
    public const string EntityType = "Item";
    public const string SourceModule = "product-item-sku-master";

    public static AuditMetadata Metadata(AuditOperation operation) => new(
        Category: AuditCategory.MasterData, Operation: operation, EntityType: EntityType, SourceModule: SourceModule);

    // FIX1 item 2: the only names an item audit record carries (master data — no password, token or personal datum).
    public static readonly IReadOnlySet<string> Fields = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(Item.CanonicalCode), nameof(Item.Name), nameof(Item.Description), nameof(Item.MaterialType),
        nameof(Item.BaseUomCode), nameof(Item.UomConversions), nameof(Item.StorageConditionCodes),
        nameof(Item.LotControlled), nameof(Item.Stockable), nameof(Item.ShelfLifeDays), nameof(Item.RetestDays),
        nameof(Item.MinRemainingShelfLifeDays), nameof(Item.LifecycleStatus)
    };

    public const string FirstFill = "firstFill";
    // FIX1-EK (CT decision, AUD-001 §3): the free-text reason never enters the audit record — only whether one was given.
    // The text stays on the item. A coded reason list (S5) brings "reasonCode" back as a list code.
    public const string ReasonProvided = "reasonProvided";
    // S2 FIX1 item 6 (CT decision A): a reactivation's reason enters the record only as "given" and its length.
    public const string ReasonLength = "reasonLength";
    public const string Replay = "replay";
    public const string Lifecycle = "lifecycle";
    public const string StockCheck = "stockCheck";

    // FU04 S2 (§21): the approval's facts — the request (operation, kind), the decision's evidence (who, when, which task,
    // which transition) and the coded outcome. Ids and codes only.
    public const string OperationId = "operationId";
    public const string ActivationKind = "activationKind";
    public const string Decision = "decision";
    public const string DecisionActorId = "decisionActorId";
    public const string DecisionAt = "decisionAt";
    public const string ApprovalTaskId = "approvalTaskId";
    public const string TransitionSequence = "transitionSequence";
    public const string OutcomeCode = "outcomeCode";
    public const string MissingFields = "missingFields";

    public static readonly IReadOnlySet<string> MetadataKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        FirstFill, ReasonProvided, ReasonLength, Replay, Lifecycle, StockCheck, AuditDetails.RefusalCodeKey,
        OperationId, ActivationKind, Decision, DecisionActorId, DecisionAt, ApprovalTaskId, TransitionSequence, OutcomeCode,
        MissingFields
    };

    // The audited values of a row (codes and plain values; conversions as "FROM>TO=N/D", sorted).
    public static Dictionary<string, object?> Snapshot(Item item) => new(StringComparer.Ordinal)
    {
        [nameof(Item.CanonicalCode)] = item.CanonicalCode,
        [nameof(Item.Name)] = item.Name,
        [nameof(Item.Description)] = item.Description,
        [nameof(Item.MaterialType)] = ItemRules.Code(item.MaterialType),
        [nameof(Item.BaseUomCode)] = item.BaseUomCode,
        [nameof(Item.UomConversions)] = item.UomConversions
            .Select(c => $"{c.FromUom}>{c.ToUom}={c.Numerator}/{c.Denominator}").Order(StringComparer.Ordinal).ToArray(),
        [nameof(Item.StorageConditionCodes)] = item.StorageConditionCodes.Order(StringComparer.Ordinal).ToArray(),
        [nameof(Item.LotControlled)] = item.LotControlled,
        [nameof(Item.Stockable)] = item.Stockable,
        [nameof(Item.ShelfLifeDays)] = item.ShelfLifeDays,
        [nameof(Item.RetestDays)] = item.RetestDays,
        [nameof(Item.MinRemainingShelfLifeDays)] = item.MinRemainingShelfLifeDays,
        [nameof(Item.LifecycleStatus)] = item.LifecycleStatus.ToString()
    };

    // Only the fields whose value moved; an empty locked field filled on a non-Draft item is named in firstFill.
    public static void RecordChanges(AuditDetails details, Dictionary<string, object?> before, Item after, bool draft)
    {
        var next = Snapshot(after);
        var firstFill = new List<string>();
        foreach (var (field, value) in next)
        {
            var previous = before[field];
            if (Same(previous, value)) continue;
            details.Change(field, previous, value);
            if (!draft && Empty(previous) && field is not nameof(Item.Name) and not nameof(Item.Description))
                firstFill.Add(field);
        }
        if (firstFill.Count > 0) details.Set(FirstFill, firstFill.ToArray());
    }

    private static bool Same(object? left, object? right) =>
        left is string[] l && right is string[] r ? l.SequenceEqual(r, StringComparer.Ordinal) : Equals(left, right);

    private static bool Empty(object? value) => value is null || value is string[] { Length: 0 };
}
