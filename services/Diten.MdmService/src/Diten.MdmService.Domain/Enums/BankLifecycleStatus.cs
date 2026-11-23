namespace Diten.MdmService.Domain.Enums;

/// <summary>
/// MOD-0289 — lifecycle of a bank and of a bank branch (a bank account shares it in a later slice). The numbers are the
/// source's (BankMasterLifecycleStatus @da4abb20) and never change.
/// </summary>
public enum BankLifecycleStatus
{
    Draft = 1,
    Active = 2,
    Inactive = 3
}
