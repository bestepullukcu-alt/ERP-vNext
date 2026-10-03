using Microsoft.Extensions.DependencyInjection;using MongoDB.Driver;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Persistence.Features.SandopPlans;
public static class SandopPersistenceRegistration
{ // Q220 (2026-10-03): added the schema hosted service, matching Capacity, Carrier, Load, Return and Claim.
 // Without it the six sandop_* indexes — five of them UNIQUE — are never created in a composed host, so the
 // duplicate-key detection SandopRepository relies on has nothing to fire on and the uniqueness guarantee
 // fails silently. This takes effect when Program.cs composes S&OP; today it does not (see Q236's exclusion
 // list and Q209), so this is a correctness fix that arms itself at composition rather than a live change.
 public static IServiceCollection AddSandopPersistence(this IServiceCollection services)
{ services.AddScoped<ISandopRepository,SandopRepository>();services.AddHostedService<SandopSchema>();return services; } }
