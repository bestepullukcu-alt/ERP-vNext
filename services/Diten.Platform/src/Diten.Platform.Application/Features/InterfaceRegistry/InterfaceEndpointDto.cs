namespace Diten.Platform.Application.Features.InterfaceRegistry;

public sealed record InterfaceEndpointDto(
    string EndpointKey,
    string HttpMethod,
    string RoutePath,
    string Version,
    string? RouteName,
    string? PermissionKey,
    string? AuthPolicy,
    string? RequestContract,
    string? ResponseContract,
    IReadOnlyList<int> ProducesStatusCodes);
