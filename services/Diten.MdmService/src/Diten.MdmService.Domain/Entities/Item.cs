using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Entities;

// MOD-0290-FU04 §4.1: the item / material master row (mdm_items). Tenant-wide master data: no legal-entity scope
// (Decision 6). The code is the product identity family's (IT-{12 digits}); it never changes and is never reused.
public sealed class Item : EntityBase
{
    public string CanonicalCode { get; set; } = string.Empty;
    public Guid CodeReservationId { get; set; }
    public string CreationCommandId { get; set; } = string.Empty;
    // SHA-256 of the creation request's facts: the same key with other facts is IDEMPOTENCY_KEY_CONFLICT, never a replay.
    public string CreationFingerprint { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameNormalized { get; set; } = string.Empty;
    // The uniqueness key "{MaterialType}|{NameNormalized}" while the item is not Retired; null once Retired, so a
    // retired name can be used again (unique partial index on string values only).
    public string? NameKey { get; set; }
    public string? Description { get; set; }
    public ItemMaterialType MaterialType { get; set; }
    public ReferenceCatalogSelection? MaterialTypeSelection { get; set; }
    public string BaseUomCode { get; set; } = string.Empty;
    public ReferenceCatalogSelection? BaseUomSelection { get; set; }
    public List<ItemUomConversion> UomConversions { get; set; } = [];
    public List<string> StorageConditionCodes { get; set; } = [];
    public List<ReferenceCatalogSelection> StorageConditionSelections { get; set; } = [];
    // FIX1 M6: each stored storage code's ConditionKind (TEMPERATURE | PROTECTION) as the list answered it, so an edit
    // that adds no code checks O-2 without resolving again. A row without it (S1) resolves its storage codes once.
    public Dictionary<string, string>? StorageConditionKinds { get; set; }
    // FU04 S2 (R-04, contract "Etkinleştirme kuralı"): null = not filled. A draft may leave it null (a document written
    // before the field was nullable carries its value; a document without the field reads null — never "false").
    // Activation refuses a null flag (ITEM_STOCK_FIELDS_REQUIRED_FOR_ACTIVATION).
    public bool? LotControlled { get; set; }
    public bool Stockable { get; set; }
    public int? ShelfLifeDays { get; set; }
    public int? RetestDays { get; set; }
    public int? MinRemainingShelfLifeDays { get; set; }
    public ItemLifecycleStatus LifecycleStatus { get; set; } = ItemLifecycleStatus.Draft;
    public ItemActivationKind? ActivationKind { get; set; }
    // FU04 S2 (§2.6, the shared GP / GSKU / LSKU binding): the activation approval this item is bound to — set when the
    // item goes PendingActivation, its decision or cancellation evidence kept on it. Null on a draft never submitted.
    public ProductIdentityWorkflowBinding? ActivationWorkflowBinding { get; set; }
    // FU04 S2 (the LSKU submit fence, §8.4 A): the activation operation whose start may be in the engine's hands. While
    // it is set the item's own writes (edit, deactivate, retire) are refused — a start is never sent for facts that
    // changed under it. Released by the operation's end (pending, abandoned, closed). Null on a legacy document.
    public Guid? ActivationSubmitOperationId { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? DeactivationReason { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
    public string? RetirementReason { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
}
