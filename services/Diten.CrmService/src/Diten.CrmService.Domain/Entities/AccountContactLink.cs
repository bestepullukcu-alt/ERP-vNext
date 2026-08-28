namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0150 FU03 — M:N link between an Account (MOD-0149) and a Contact (MOD-0150). RoleCode is a MOD-0048
/// <c>contact-role</c> published value. Active-link semantics run on <see cref="EntityBase.IsDeleted"/> + validity
/// (ValidFrom/ValidTo), NOT on <see cref="Status"/> — there is no <c>account-contact-link-status</c> reference set yet,
/// so Status is a free internal lifecycle marker (default "active"), never reference-validated and never hardcoded-fallback.
/// Uniqueness: one active link per (Tenant, Account, Contact, Role); at most one IsPrimary per (Tenant, Account, Role).
/// </summary>
public sealed class AccountContactLink : EntityBase
{
    public Guid AccountId { get; set; }
    public Guid ContactId { get; set; }

    /// <summary>MOD-0048 published contact-role value code (required).</summary>
    public string RoleCode { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    /// <summary>Internal lifecycle marker (default "active"). NOT reference-validated (no set); active-link logic uses IsDeleted + validity.</summary>
    public string Status { get; set; } = "active";

    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Notes { get; set; }

    /// <summary>MOD-0150 in-account contact hierarchy: the contact this contact reports to <b>within this same account</b>
    /// (org chart). The parent must have an active link to the same account; self-report and cycles are forbidden. Null =
    /// top of the tree / no manager. This is per-account (on the link), never a global parent on the Contact master.</summary>
    public Guid? ReportsToContactId { get; set; }

    /// <summary>MOD-0150 hardening: business justification recorded when the Contact and Account are in different countries
    /// (controlled cross-country link). Non-empty only for cross-country links; never written raw to audit/log.</summary>
    public string? CrossCountryReason { get; set; }
}
