namespace Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ConsumesInterfaceAttribute : Attribute
{
    public ConsumesInterfaceAttribute(string consumedInterfaceCode, string consumerModuleCode)
    {
        ConsumedInterfaceCode = consumedInterfaceCode;
        ConsumerModuleCode = consumerModuleCode;
    }

    public string ConsumedInterfaceCode { get; }
    public string ConsumerModuleCode { get; }
    public string? ConsumedVersionRange { get; init; }
    public bool Required { get; init; } = true;
    public string? UsageContext { get; init; }
}
