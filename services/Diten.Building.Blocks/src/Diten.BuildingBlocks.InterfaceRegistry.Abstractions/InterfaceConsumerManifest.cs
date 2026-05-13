namespace Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

public sealed record InterfaceConsumerManifest(
    string ConsumerModuleCode,
    string ConsumerService,
    string ConsumedInterfaceCode,
    string? ConsumedVersionRange,
    bool Required,
    string? UsageContext);
