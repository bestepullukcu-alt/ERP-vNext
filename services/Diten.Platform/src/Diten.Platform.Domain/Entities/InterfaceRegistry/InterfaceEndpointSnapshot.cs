namespace Diten.Platform.Domain.Entities.InterfaceRegistry;

public sealed class InterfaceEndpointSnapshot
{
    public string EndpointKey { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string RoutePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? RouteName { get; set; }
    public string? PermissionKey { get; set; }
    public string? AuthPolicy { get; set; }
    public string? RequestContract { get; set; }
    public string? ResponseContract { get; set; }
    public List<int> ProducesStatusCodes { get; set; } = [];
}
