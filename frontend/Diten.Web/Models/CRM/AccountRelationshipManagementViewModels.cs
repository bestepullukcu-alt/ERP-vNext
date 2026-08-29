using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// MOD-0149/MOD-0150 — Account 360 relationship MANAGEMENT view models (Add / Edit / End) for Related Contacts
// (AccountContactLink) and Related Accounts (AccountRelationship). Frontend read/write models only — NO field is added
// to the Account or Contact master. Historical lifecycle: an "End" action never deletes; it sets Status=ended + ValidTo
// via the existing PUT endpoint so downstream sales/visit/order/forecast context is preserved.

// ---- Related Contacts (AccountContactLink) ----

/// <summary>GET /api/crm/accounts/{accountId}/contacts/{linkId} shape (mirrors CrmService AccountContactLinkDto).</summary>
public sealed class AccountContactLinkDetailViewModel
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid ContactId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Notes { get; set; }
    public Guid? ReportsToContactId { get; set; }
}

/// <summary>Add + Edit form for an account↔contact link. LinkId is null on Add.</summary>
public sealed class AccountContactLinkEditViewModel
{
    public Guid AccountId { get; set; }
    public Guid? LinkId { get; set; }
    public string? AccountName { get; set; }

    [Required]
    public Guid ContactId { get; set; }
    public string? ContactDisplayName { get; set; }

    [Required]
    public string RoleCode { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    /// <summary>Read-only lifecycle marker shown on Edit; never edited here (use End Link).</summary>
    public string Status { get; set; } = "active";

    [DataType(DataType.Date)]
    public DateTime? ValidFrom { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ValidTo { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    /// <summary>MOD-0150 hardening: required only for a cross-country Contact↔Account link (soft-warning / reason-required).</summary>
    [StringLength(500)]
    public string? CrossCountryReason { get; set; }

    /// <summary>MOD-0150 in-account hierarchy: the manager contact (within this account) this contact reports to. Optional.</summary>
    public Guid? ReportsToContactId { get; set; }

    public bool IsEdit => LinkId is not null;

    // Options (sourced live; never hardcoded).
    public IReadOnlyList<ReferenceOptionViewModel> RoleOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> ContactOptions { get; set; } = [];
    /// <summary>Manager candidates = contacts already linked to THIS account (excluding the contact being linked).</summary>
    public IReadOnlyList<ReferenceOptionViewModel> ReportsToOptions { get; set; } = [];
    public string? ReferenceDependencyMessage { get; set; }
}

/// <summary>End (historical close) form for an account↔contact link — sets Status=ended + ValidTo, never deletes.</summary>
public sealed class AccountContactLinkEndViewModel
{
    public Guid AccountId { get; set; }
    public Guid LinkId { get; set; }
    public string? ContactDisplayName { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public DateTimeOffset? ValidFrom { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; } = DateTime.UtcNow.Date;

    [StringLength(2000)]
    public string? Notes { get; set; }
}

// ---- Related Accounts (AccountRelationship) ----

/// <summary>GET /api/crm/accounts/{accountId}/relationships/{relationshipId} (mirrors CrmService AccountRelationshipDto).</summary>
public sealed class AccountRelationshipDetailViewModel
{
    public Guid Id { get; set; }
    public Guid SourceAccountId { get; set; }
    public Guid TargetAccountId { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Add + Edit form for an account↔account relationship. RelationshipId is null on Add.
/// Direction/inverse/self-allowed are backend-derived from metadata; the UI never sends a direction.</summary>
public sealed class AccountRelationshipEditViewModel
{
    public Guid AccountId { get; set; }
    public Guid? RelationshipId { get; set; }
    public string? AccountName { get; set; }

    [Required]
    public Guid TargetAccountId { get; set; }
    public string? TargetAccountName { get; set; }

    [Required]
    public string RelationshipType { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? ValidFrom { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ValidTo { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    /// <summary>MOD-0150 hardening: required only for a cross-country Account↔Account relationship (soft-warning / reason-required).</summary>
    [StringLength(500)]
    public string? CrossCountryReason { get; set; }

    public bool IsEdit => RelationshipId is not null;

    // Options (sourced live from MOD-0048 / accounts list; never hardcoded).
    public IReadOnlyList<ReferenceOptionViewModel> RelationshipTypeOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> RelationshipStatusOptions { get; set; } = [];
    public IReadOnlyList<ReferenceOptionViewModel> TargetAccountOptions { get; set; } = [];
    public string? ReferenceDependencyMessage { get; set; }
}

/// <summary>End (historical close) form for a relationship — sets Status=ended + ValidTo, never deletes.</summary>
public sealed class AccountRelationshipEndViewModel
{
    public Guid AccountId { get; set; }
    public Guid RelationshipId { get; set; }
    public string? TargetAccountName { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public DateTimeOffset? ValidFrom { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; } = DateTime.UtcNow.Date;

    [StringLength(2000)]
    public string? Notes { get; set; }
}

// ---- Payloads posted to the CRM backend (mirror the CrmService request DTOs) ----

public sealed class LinkContactPayload
{
    public Guid ContactId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Notes { get; set; }
    public string? CrossCountryReason { get; set; }
    public Guid? ReportsToContactId { get; set; }
}

public sealed class UpdateContactLinkPayload
{
    public string RoleCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Notes { get; set; }
    /// <summary>Historical lifecycle marker; set to "ended" by the End action, null otherwise (keeps current status).</summary>
    public string? Status { get; set; }
    public string? CrossCountryReason { get; set; }
    public Guid? ReportsToContactId { get; set; }
}

public sealed class CreateRelationshipPayload
{
    public Guid TargetAccountId { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Notes { get; set; }
    public string? CrossCountryReason { get; set; }
}

public sealed class UpdateRelationshipPayload
{
    public string RelationshipType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string? Notes { get; set; }
    public string? CrossCountryReason { get; set; }
}
