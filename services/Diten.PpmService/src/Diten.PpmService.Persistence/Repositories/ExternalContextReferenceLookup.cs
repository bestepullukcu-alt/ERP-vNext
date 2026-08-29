using Diten.PpmService.Application.Features.ExternalContextReferences;
using Diten.PpmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Repositories;

public sealed class ExternalContextReferenceLookup(
    IPortfolioRepository portfolios,
    IInitiativeRepository initiatives,
    IProgramRepository programs,
    IProjectRepository projects)
    : IExternalContextReferenceLookup
{
    public async Task<ExternalContextReferenceLookupResult?> FindAsync(
        Guid tenantId,
        string contextKind,
        Guid contextId,
        CancellationToken cancellationToken)
    {
        try
        {
            return contextKind switch
            {
                "Portfolio" => Map(await portfolios.GetByIdAsync(tenantId, contextId, cancellationToken)),
                "Initiative" => Map(await initiatives.GetByIdAsync(tenantId, contextId, cancellationToken)),
                "Program" => Map(await programs.GetByIdAsync(tenantId, contextId, cancellationToken)),
                "Project" => Map(await projects.GetByIdAsync(tenantId, contextId, cancellationToken)),
                _ => null
            };
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw new ExternalContextReferenceDependencyException(
                "Mongo external context lookup failed.", exception);
        }
    }

    private static ExternalContextReferenceLookupResult? Map(Domain.Entities.Portfolio? value) =>
        value is null ? null : new(value.IsReferenceable, value.VisibilityPolicyKey);
    private static ExternalContextReferenceLookupResult? Map(Domain.Entities.Initiative? value) =>
        value is null ? null : new(value.IsReferenceable, value.VisibilityPolicyKey);
    private static ExternalContextReferenceLookupResult? Map(Domain.Entities.Program? value) =>
        value is null ? null : new(value.IsReferenceable, value.VisibilityPolicyKey);
    private static ExternalContextReferenceLookupResult? Map(Domain.Entities.Project? value) =>
        value is null ? null : new(value.IsReferenceable, value.VisibilityPolicyKey);
}
