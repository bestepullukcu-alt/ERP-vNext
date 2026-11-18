namespace Diten.MdmService.Application.Features.BankMaster;

/// <summary>
/// MOD-0289 DL-1 — the bank and branch contracts. No DTO carries a tenant: the tenant is the trusted one, written and read by
/// the server. <c>Code</c> and <c>TenantId</c> exist on the write requests only so a request that SENDS them can be refused
/// (400 MDM_BANK_CODE_FORBIDDEN / MDM_BANK_VALIDATION_FAILED) instead of being silently ignored.
/// </summary>
public sealed record BankWriteRequest
{
    public string? Name { get; init; }
    public string? CountryCode { get; init; }
    public string? Bic { get; init; }
    public string? NationalBankCode { get; init; }
    public bool? DomesticKeyIncludesBranch { get; init; }

    /// <summary>Always refused: the code comes from the shared code service.</summary>
    public string? Code { get; init; }

    /// <summary>Always refused: the tenant is never taken from a request.</summary>
    public Guid? TenantId { get; init; }
}

public sealed record BankBranchWriteRequest
{
    /// <summary>Optional; when sent it must be the bank of the route / of the stored branch (else 400 MDM_BANK_BRANCH_BANK_MISMATCH).</summary>
    public Guid? BankId { get; init; }

    public string? BranchCode { get; init; }
    public string? Name { get; init; }

    /// <summary>Optional; defaults to the bank's country and, when sent, must equal it.</summary>
    public string? CountryCode { get; init; }

    public string? City { get; init; }
    public string? Bic { get; init; }

    /// <summary>Always refused.</summary>
    public Guid? TenantId { get; init; }
}

public sealed record DeactivateRequest
{
    public string? ReasonCode { get; init; }
}

public sealed record BulkDeleteBanksRequest
{
    public IReadOnlyList<Guid>? Ids { get; init; }
}

public sealed record BankListItemDto(
    Guid Id,
    string Code,
    string Name,
    string CountryCode,
    string? Bic,
    string? NationalBankCode,
    string Status,
    int Version);

public sealed record BankDetailDto(
    Guid Id,
    string Code,
    string Name,
    string CountryCode,
    string? Bic,
    string? NationalBankCode,
    bool DomesticKeyIncludesBranch,
    int DomesticKeyFenceVersion,
    string Status,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? CreatedBy,
    Guid? UpdatedBy,
    DateTimeOffset? ActivatedAt,
    Guid? ActivatedBy,
    DateTimeOffset? DeactivatedAt,
    Guid? DeactivatedBy,
    string? DeactivationReasonCode);

public sealed record BankBranchDto(
    Guid Id,
    Guid BankId,
    string BranchCode,
    string Name,
    string CountryCode,
    string? City,
    string? Bic,
    int DomesticKeyFenceVersion,
    string Status,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? CreatedBy,
    Guid? UpdatedBy,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? DeactivatedAt,
    string? DeactivationReasonCode);

public sealed record BankListResultDto(IReadOnlyList<BankListItemDto> Items, long Total, int Page, int PageSize);

public sealed record BankBranchListResultDto(IReadOnlyList<BankBranchDto> Items, long Total, int Page, int PageSize);
