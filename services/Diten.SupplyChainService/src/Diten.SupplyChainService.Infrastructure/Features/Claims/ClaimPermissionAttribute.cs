namespace Diten.SupplyChainService.Infrastructure.Features.Claims;
// Metadata only: ordered middleware performs coarse authorization, then the
// application handler performs the body-dependent exact target grant check.
[AttributeUsage(AttributeTargets.Method)]
public sealed class ClaimPermissionAttribute(params string[] permissions) : Attribute
{
    public string[] Permissions { get; } = permissions;
}
