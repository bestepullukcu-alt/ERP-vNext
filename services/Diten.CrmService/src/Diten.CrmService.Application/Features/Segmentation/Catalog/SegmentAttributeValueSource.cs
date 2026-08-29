namespace Diten.CrmService.Application.Features.Segmentation.Catalog;

/// <summary>
/// MOD-0167 FU02 (P1a) — <b>where an authored criterion VALUE legitimately comes from</b>, declared per attribute and
/// published on the attribute catalog so a criteria editor can render the right input instead of a text box.
/// <para>This is DESCRIPTIVE metadata only. It changes nothing about the criteria contract: the predicate shape, the
/// persisted <c>Values</c> array, the resolver, the D6 fail-closed matrix and <c>evaluate</c> are all untouched. A
/// caller that ignores this field keeps working exactly as before, and free text remains accepted everywhere — the UI
/// uses it to OFFER the right values, never to narrow what the runtime will accept.</para>
/// <para>The reference set codes are taken from the EXISTING write-path validators
/// (<c>AccountReferenceValidation</c> / <c>ContactReferenceValidation</c> / <c>AccountContactValidation</c> /
/// <c>ContactWorkbookSchema</c>) rather than re-declared here, so the editor can never offer a value the Account or
/// Contact write path would reject. No new MOD-0048 set is invented.</para>
/// </summary>
public sealed record SegmentAttributeValueSource(
    string Kind,
    string? ReferenceSetCode,
    IReadOnlyList<string> AllowedValues,
    string? EntityKind)
{
    /// <summary>Genuinely open text (a name, a tenant-authored attribute value). A plain input is correct.</summary>
    public const string KindFreeText = "free-text";

    /// <summary>The value is a published MOD-0048 reference value; the UI reads the set through the tenant-scoped
    /// published-values consumer and offers a Select2. An unpublished set yields an EMPTY list, never a local
    /// fallback — the same rule the Account/Contact forms already follow.</summary>
    public const string KindReferenceSet = "reference-set";

    /// <summary>A closed, in-domain result vocabulary (for example the consent eligibility verdict). The values ride
    /// on this record, so the UI needs no second endpoint and no hardcoded list.</summary>
    public const string KindEnum = "enum";

    /// <summary>The value is another aggregate's id, so the UI offers that aggregate's existing selector.</summary>
    public const string KindEntityPicker = "entity-picker";

    // Entity kinds. Each one names a surface that ALREADY exists; none of them opens a new endpoint.
    public const string EntityAccount = "account";                 // /api/crm/accounts
    public const string EntityTerritoryModel = "territory-model";  // /api/crm/territory-models
    public const string EntityTerritoryNode = "territory-node";    // /api/crm/territory-models/{id}/nodes
    public const string EntityGlobalProduct = "global-product";    // /api/global-products/selector
    public const string EntityMdmProduct = "mdm-product";          // /api/mdm/products
    public const string EntityMdmBrand = "mdm-brand";              // /api/mdm/brands

    public static readonly SegmentAttributeValueSource FreeText =
        new(KindFreeText, null, Array.Empty<string>(), null);

    public static SegmentAttributeValueSource ReferenceSet(string setCode)
        => new(KindReferenceSet, setCode, Array.Empty<string>(), null);

    public static SegmentAttributeValueSource Enum(params string[] allowedValues)
        => new(KindEnum, null, allowedValues, null);

    public static SegmentAttributeValueSource EntityPicker(string entityKind)
        => new(KindEntityPicker, null, Array.Empty<string>(), entityKind);

    public static readonly IReadOnlyList<string> AllKinds =
        new[] { KindFreeText, KindReferenceSet, KindEnum, KindEntityPicker };
}
