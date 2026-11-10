using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.Supplier.Store;

/// <summary>
/// MOD-0140 DL-3 — one supplier as the CALLER'S COMPANY sees it: the tenant-wide common fields (§4.1, K-1) plus the caller
/// company's relationship (§4.2). Every handler reads and writes through this shape, whichever model
/// <c>Suppliers:Model</c> selects (K-3): under <c>TenantShared</c> it is a <c>supplier_masters</c> record joined with the
/// caller's <c>supplier_company_relationships</c> record; under <c>PerLegalEntity</c> it is today's per-company
/// <c>suppliers</c> record (one record = one relationship, <see cref="MasterId"/> = <see cref="RelationshipId"/>).
///
/// <para>A snapshot exists only when the caller's company has a NON-DELETED relationship — a supplier the company is not
/// related to is not found (404, §8). The other companies' relationship data never enters a snapshot.</para>
/// </summary>
public sealed class SupplierSnapshot
{
    /// <summary><c>supplierMasterId</c> in the contract (§3.1) — <c>Supplier.Id</c>.</summary>
    public Guid MasterId { get; init; }

    public Guid RelationshipId { get; init; }

    /// <summary>Canonical code (<c>SUP-xxxxxxxx</c>).</summary>
    public string SupplierId { get; init; } = string.Empty;

    // ── common fields (tenant-wide; K-1) ─────────────────────────────────────────
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? TaxId { get; set; }
    public List<SupplierContact> Contacts { get; set; } = new();
    public List<SupplierDocument> Documents { get; set; } = new();
    public KycOutcome KycOutcome { get; set; }
    public SanctionsOutcome SanctionsOutcome { get; set; }

    // ── the caller company's relationship ────────────────────────────────────────
    public SupplierStatus Status { get; set; }
    public OnboardingStatus OnboardingStatus { get; set; }
    public OnboardingCase? OnboardingCase { get; set; }
    public SupplierRelationshipApproval? Approval { get; set; }
    public string? PaymentTermsCode { get; set; }
    public bool PurchasingEnabled { get; set; }
    public DateTimeOffset? PurchasingSince { get; set; }
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }
    public IReadOnlyList<string> LegacySupplierCodes { get; init; } = [];
    public string? IdempotencyKey { get; init; }
    public string? OnboardingIdempotencyKey { get; set; }

    /// <summary>Version of the common record (TenantShared) — the legacy record's version under PerLegalEntity.</summary>
    public int MasterVersion { get; init; }

    /// <summary>Version of the relationship record (TenantShared) — 0 under PerLegalEntity (one record).</summary>
    public int RelationshipVersion { get; init; }

    /// <summary>
    /// The concurrency token the API shows (<c>Version</c>, <c>If-Match</c>): the sum of the two record versions. Both only
    /// ever grow, so an unchanged sum means neither record changed since it was read; the store still writes each record
    /// under its own version (no lost update between the two).
    /// </summary>
    public int Version => MasterVersion + RelationshipVersion;
}

/// <summary>
/// The single eligibility rule (§8, §12; S-1, Natig 2026-10-28): a non-deleted relationship in status Active. Nothing else
/// enters it — onboarding approval, <c>purchasingEnabled</c>, screening and tenant-level blocks are the consumer's.
/// <c>validate</c>, <c>GET /{supplierId}/eligibility</c> and <c>GET /eligible</c> all use this function.
/// </summary>
public static class SupplierEligibilityRule
{
    /// <param name="relationship">The caller company's non-deleted relationship, or null when there is none.</param>
    public static bool IsEligible(SupplierSnapshot? relationship) => relationship is not null && relationship.Status == SupplierStatus.Active;
}
