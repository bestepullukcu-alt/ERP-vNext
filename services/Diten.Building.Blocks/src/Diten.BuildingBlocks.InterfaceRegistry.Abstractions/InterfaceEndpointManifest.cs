namespace Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

public sealed record InterfaceEndpointManifest(
    string HttpMethod,
    string RoutePath,
    string Version,
    string? RouteName = null,
    string? PermissionKey = null,
    string? AuthPolicy = null,
    string? RequestContract = null,
    string? ResponseContract = null,
    IReadOnlyList<int>? ProducesStatusCodes = null);
