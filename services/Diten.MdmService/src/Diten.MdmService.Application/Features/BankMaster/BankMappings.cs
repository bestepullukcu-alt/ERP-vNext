using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.BankMaster;

/// <summary>Entity → DTO. No mapping returns the tenant, the audit intents or their receipts.</summary>
public static class BankMappings
{
    public static BankListItemDto ToListItem(Bank bank) => new(
        bank.Id, bank.Code, bank.Name, bank.CountryCode, bank.Bic, bank.NationalBankCode, bank.Status.ToString(), bank.Version);

    public static BankDetailDto ToDetail(Bank bank) => new(
        bank.Id, bank.Code, bank.Name, bank.CountryCode, bank.Bic, bank.NationalBankCode, bank.DomesticKeyIncludesBranch,
        bank.DomesticKeyFenceVersion, bank.Status.ToString(), bank.Version, bank.CreatedAt, bank.UpdatedAt, bank.CreatedBy,
        bank.UpdatedBy, bank.ActivatedAt, bank.ActivatedBy, bank.DeactivatedAt, bank.DeactivatedBy, bank.DeactivationReasonCode);

    public static BankBranchDto ToDto(BankBranch branch) => new(
        branch.Id, branch.BankId, branch.BranchCode, branch.Name, branch.CountryCode, branch.City, branch.Bic,
        branch.DomesticKeyFenceVersion, branch.Status.ToString(), branch.Version, branch.CreatedAt, branch.UpdatedAt,
        branch.CreatedBy, branch.UpdatedBy, branch.ActivatedAt, branch.DeactivatedAt, branch.DeactivationReasonCode);
}
