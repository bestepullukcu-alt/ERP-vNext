using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

/// <summary>
/// MOD-0289 DL-1 — a branch of a bank (<c>mdm_bank_branches</c>). The branch code is the natural key inside its bank (no
/// record code service for branches). Same tenant, soft-delete and audit-intent rules as <see cref="Bank"/>.
/// </summary>
public sealed class BankBranch : EntityBase, IAuditIntentAggregate
{
    public Guid BankId { get; set; }

    /// <summary>Trimmed, blanks and dashes removed, upper case, 1..35; unique inside the bank.</summary>
    public string BranchCode { get; set; } = string.Empty;

    /// <summary>The branch's own domestic key fence (when the branch code is part of the key).</summary>
    public int DomesticKeyFenceVersion { get; set; }

    public string Name { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? City { get; set; }

    /// <summary>An 11-character ISO 9362 branch BIC whose first 8 characters are the bank's BIC (when it has one).</summary>
    public string? Bic { get; set; }

    public BankLifecycleStatus Status { get; set; } = BankLifecycleStatus.Draft;
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public Guid? ActivatedBy { get; set; }
    public Guid? DeactivatedBy { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
    public string? DeactivationReasonCode { get; set; }
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];

    public bool CanUpdate => Status is BankLifecycleStatus.Draft or BankLifecycleStatus.Active;

    /// <summary>An Active branch's edit is limited to its name and city; code, BIC and country are its identity.</summary>
    public bool UpdateIsLimited => Status == BankLifecycleStatus.Active;

    public bool CanActivate => Status is BankLifecycleStatus.Draft or BankLifecycleStatus.Inactive;
    public bool CanDeactivate => Status == BankLifecycleStatus.Active;
    public bool CanDelete => Status == BankLifecycleStatus.Draft && ActivatedAt is null;

    public static BankBranch CreateDraft(
        Guid tenantId, Guid bankId, string branchCode, string name, string countryCode, string? city, string? bic,
        Guid actor, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A trusted tenant is required.", nameof(tenantId));
        if (bankId == Guid.Empty) throw new ArgumentException("A bank is required.", nameof(bankId));
        return new BankBranch
        {
            TenantId = tenantId,
            BankId = bankId,
            BranchCode = branchCode,
            Name = name,
            CountryCode = countryCode,
            City = city,
            Bic = bic,
            DomesticKeyFenceVersion = 0,
            Status = BankLifecycleStatus.Draft,
            CreatedBy = actor,
            UpdatedBy = actor,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        };
    }

    /// <summary>True when the proposed code changes what the domestic account key is built from (the fence moves).</summary>
    public bool ChangesDomesticKey(string branchCode) =>
        !string.Equals(BranchCode, branchCode, StringComparison.Ordinal);

    public void Activate(Guid actor, DateTimeOffset now)
    {
        Status = BankLifecycleStatus.Active;
        ActivatedBy = actor;
        ActivatedAt = now;
        UpdatedBy = actor;
        UpdatedAt = now;
    }

    public void Deactivate(Guid actor, string reasonCode, DateTimeOffset now)
    {
        Status = BankLifecycleStatus.Inactive;
        DeactivatedBy = actor;
        DeactivatedAt = now;
        DeactivationReasonCode = reasonCode;
        UpdatedBy = actor;
        UpdatedAt = now;
    }
}
