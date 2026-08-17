namespace Diten.Platform.Application.Features.InterfaceRegistry;

public sealed record InterfaceConsumerDependencyDto(
    string ConsumerModuleCode,
    string ConsumerService,
    string ConsumedInterfaceCode,
    string? ConsumedVersionRange,
    bool Required,
    string? UsageContext);
