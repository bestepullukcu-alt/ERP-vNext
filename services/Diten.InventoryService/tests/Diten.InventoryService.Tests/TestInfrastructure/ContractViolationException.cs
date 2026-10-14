namespace Diten.InventoryService.Tests.TestInfrastructure;

public sealed class ContractViolationException : Exception
{
    public ContractViolationException(string message) : base(message)
    {
    }
}
