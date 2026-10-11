namespace Diten.SupplyChainService.Infrastructure.Features.Claims;
public static class ClaimPermissions
{
    public const string Read = "supplychain.claims.read";
    public const string Create = "supplychain.claims.create";
    public const string Investigate = "supplychain.claims.investigate";
    public const string Decide = "supplychain.claims.decide";
    public const string Settle = "supplychain.claims.settle";
    public static readonly string[] Mutation = [Create, Investigate, Decide, Settle];
}
