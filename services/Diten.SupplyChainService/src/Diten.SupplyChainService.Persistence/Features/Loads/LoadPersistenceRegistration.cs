using Microsoft.Extensions.DependencyInjection;
using Diten.SupplyChainService.Domain.Features.Loads;
using Diten.SupplyChainService.Application.Features.Loads;
namespace Diten.SupplyChainService.Persistence.Features.Loads;
public static class LoadPersistenceRegistration
{ public static IServiceCollection AddLoadPersistence(this IServiceCollection s) { s.AddScoped<LoadRequestContext>();s.AddScoped<ILoadRepository,LoadRepository>();s.AddSingleton<ILoadCommitProbe,NoOpLoadCommitProbe>();s.AddScoped<LoadOutboxStore>();s.AddHostedService<LoadSchema>();return s; } }
