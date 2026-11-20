using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

/// <summary>
/// MOD-0289 DL-1 — a bank of the tenant's own bank master (<c>mdm_banks</c>). Tenant-owned (the repository writes
/// <see cref="EntityBase.TenantId"/> from the trusted tenant, never from a request), soft-deleted only. <see cref="Code"/> comes
/// from the shared record code service (RCS-001, <c>BNK</c>, group prefix) — never from the caller. Every write carries its
/// audit intent in this same document (<see cref="IAuditIntentAggregate"/>).
/// </summary>
public sealed class Bank : EntityBase, IAuditIntentAggregate
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2, upper case (format only — KARAR D24/5).</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>ISO 9362, 8 or 11 characters, upper case; null when the bank has none.</summary>
    public string? Bic { get; set; }

    /// <summary>The bank's code inside its country (trimmed, no blanks or dashes, upper case); null when none.</summary>
    public string? NationalBankCode { get; set; }

    /// <summary>
    /// The domestic key fence (gap 14/2): starts at 0 and moves with every write that changes a field the domestic account
    /// key is built from. DL-1 only moves it; the account that reads it is DL-2.
    /// </summary>
    public int DomesticKeyFenceVersion { get; set; }

    /// <summary>True when this bank's domestic accounts carry the branch code in their key (DL-2 reads it).</summary>
    public bool DomesticKeyIncludesBranch { get; set; }

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

    /// <summary>Draft → anything; Active → the limited set; Inactive → nothing (the Durumlar table).</summary>
    public bool CanUpdate => Status is BankLifecycleStatus.Draft or BankLifecycleStatus.Active;

    /// <summary>An Active bank's edit is limited to its name; every other field is the identity others rely on.</summary>
    public bool UpdateIsLimited => Status == BankLifecycleStatus.Active;

    public bool CanActivate => Status is BankLifecycleStatus.Draft or BankLifecycleStatus.Inactive;
    public bool CanDeactivate => Status == BankLifecycleStatus.Active;

    /// <summary>Only a Draft that was never activated is deleted (soft).</summary>
    public bool CanDelete => Status == BankLifecycleStatus.Draft && ActivatedAt is null;

    public static Bank CreateDraft(
        Guid tenantId, string code, string name, string countryCode, string? bic, string? nationalBankCode,
        bool domesticKeyIncludesBranch, Guid actor, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A trusted tenant is required.", nameof(tenantId));
        return new Bank
        {
            TenantId = tenantId,
            Code = code,
            Name = name,
            CountryCode = countryCode,
            Bic = bic,
            NationalBankCode = nationalBankCode,
            DomesticKeyIncludesBranch = domesticKeyIncludesBranch,
            DomesticKeyFenceVersion = 0,
            Status = BankLifecycleStatus.Draft,
            CreatedBy = actor,
            UpdatedBy = actor,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 0
        };
    }

    /// <summary>True when the proposed values change a field the domestic account key is built from (the fence moves).</summary>
    public bool ChangesDomesticKey(string countryCode, string? nationalBankCode, bool includesBranch) =>
        !string.Equals(CountryCode, countryCode, StringComparison.Ordinal)
        || !string.Equals(NationalBankCode, nationalBankCode, StringComparison.Ordinal)
        || DomesticKeyIncludesBranch != includesBranch;

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
