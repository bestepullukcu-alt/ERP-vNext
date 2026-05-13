namespace Diten.Platform.Application.Features.InterfaceRegistry;

public sealed record DeprecateInterfaceRequestBody(
    string Version,
    string? Reason);
