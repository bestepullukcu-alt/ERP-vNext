namespace Diten.SupplyChainService.Infrastructure.Features.SandopPlans;
[AttributeUsage(AttributeTargets.Method)]
public sealed class SandopPermissionAttribute(string permission):Attribute
{ public string Permission {get;}=permission; }
