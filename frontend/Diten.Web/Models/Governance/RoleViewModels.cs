using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.Governance;

// FE-C (MOD-0018-FU9) — tenant Roles screen view models. Mirrors the golden-reference Slim
// contract; backed by AuthService /api/roles via the gateway.
public sealed class RoleEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public sealed class RoleDetailViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public int PermissionCount { get; set; }
}

// AuthService CreateRoleRequest contract: { name, displayName, description }.
public sealed class RoleCreatePayload
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

// AuthService UpdateRoleRequest contract: { displayName, description } (name is immutable).
public sealed class RoleUpdatePayload
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class GovernanceGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
