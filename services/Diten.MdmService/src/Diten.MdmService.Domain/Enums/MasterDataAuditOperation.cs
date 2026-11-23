namespace Diten.MdmService.Domain.Enums;

/// <summary>
/// MOD-0289 (KARAR D24/2) — the audit operations of the master data MDM owns outside the product family. Additive and
/// general: <see cref="ProductAuditOperation"/> stays the product family's and is never extended for these. The delivery
/// sends the NAME of the value to Platform's trusted-source map (aggregate <c>Bank</c> / <c>BankBranch</c> × this name).
/// Numbers are explicit and never reused; DL-1 has the bank and branch operations only.
/// </summary>
public enum MasterDataAuditOperation
{
    BankCreated = 1,
    BankUpdated = 2,
    BankActivated = 3,
    BankDeactivated = 4,
    BankDeleted = 5,
    BankBranchCreated = 6,
    BankBranchUpdated = 7,
    BankBranchActivated = 8,
    BankBranchDeactivated = 9,
    BankBranchDeleted = 10
}
