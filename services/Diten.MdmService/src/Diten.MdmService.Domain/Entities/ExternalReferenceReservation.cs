namespace Diten.MdmService.Domain.Entities;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX2 (item 6) — one (source system, external id) pair claimed by one brand or one product of a
/// tenant. A unique index on (tenant, record type, source key, external id key) makes the claim race-safe: two concurrent
/// writes of the same pair cannot both commit. Written in the SAME transaction as the brand / product write.
///
/// <para>Only pairs a record ADDS from FIX2 on are reserved. Data from before — where two records could legally share a
/// pair — holds no reservation, so the index never meets it and editing such a record is not blocked (item 5); a NEW
/// record that would take a pair an older record still carries is refused by the read-side check.</para>
/// </summary>
public sealed class ExternalReferenceReservation : EntityBase
{
    public const string RecordTypeBrand = "brand";
    public const string RecordTypeProduct = "product";

    /// <summary><see cref="RecordTypeBrand"/> or <see cref="RecordTypeProduct"/>.</summary>
    public string RecordType { get; set; } = string.Empty;

    /// <summary>The source system, trimmed and upper-cased: "sap", " SAP " and "SAP" are one source.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>The external id, trimmed and upper-cased — an exact match, never a prefix.</summary>
    public string ExternalIdKey { get; set; } = string.Empty;

    public Guid RecordId { get; set; }

    /// <summary>The holder's code — what a refused writer is told (within the same tenant only).</summary>
    public string RecordCode { get; set; } = string.Empty;

    public static string Key(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
}
