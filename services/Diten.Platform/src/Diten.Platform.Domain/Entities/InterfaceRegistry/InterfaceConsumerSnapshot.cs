namespace Diten.Platform.Domain.Entities.InterfaceRegistry;

public sealed class InterfaceConsumerSnapshot
{
    public string ConsumerModuleCode { get; set; } = string.Empty;
    public string ConsumerService { get; set; } = string.Empty;
    public string ConsumedInterfaceCode { get; set; } = string.Empty;
    public string? ConsumedVersionRange { get; set; }
    public bool Required { get; set; }
    public string? UsageContext { get; set; }
}
