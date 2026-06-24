using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation.Handlers.QueryHandlers;

public sealed class GetInstantiationPrerequisitesHandler
    : IRequestHandler<GetInstantiationPrerequisitesQuery, Response<InstantiationPrerequisitesModel>>
{
    private readonly IBaselineReleaseRepository _baselineRepository;
    private readonly ICollectionDefinitionRepository _definitionRepository;
    private readonly ITenantContext _tenantContext;
    private readonly DocumentManagementFeatureFlagOptions _featureFlags;

    public GetInstantiationPrerequisitesHandler(
        IBaselineReleaseRepository baselineRepository,
        ICollectionDefinitionRepository definitionRepository,
        ITenantContext tenantContext,
        IOptions<DocumentManagementFeatureFlagOptions> featureFlags)
    {
        _baselineRepository = baselineRepository;
        _definitionRepository = definitionRepository;
        _tenantContext = tenantContext;
        _featureFlags = featureFlags.Value;
    }

    public async Task<Response<InstantiationPrerequisitesModel>> Handle(GetInstantiationPrerequisitesQuery request, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var baselines = await _baselineRepository.GetAllAsync(ct);
        var published = baselines
            .Where(x => x.Status == BaselineReleaseStatus.Published)
            .OrderByDescending(x => x.PublishedAt ?? x.CreatedAt)
            .ToList();

        var options = new List<PublishedBaselineReleaseOptionModel>(published.Count);
        foreach (var baseline in published)
        {
            var definitions = await _definitionRepository.GetByBaselineAsync(baseline.Id, ct);
            options.Add(new PublishedBaselineReleaseOptionModel(
                baseline.Id,
                baseline.BaselineReleaseId,
                baseline.BaselineVersion,
                baseline.ChangeSummary,
                baseline.EffectiveDate,
                definitions.Count,
                baseline.PublishedAt,
                definitions
                    .Where(x => x.Status == CollectionDefinitionStatus.Active)
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.FullPath, StringComparer.Ordinal)
                    .Select(x => new CollectionDefinitionOptionModel(
                        x.CanonicalId,
                        x.ParentCanonicalId,
                        x.Name,
                        x.FullPath,
                        x.DisplayOrder))
                    .ToList()));
        }

        var data = new InstantiationPrerequisitesModel(
            options,
            options.Count > 0,
            true,
            _featureFlags.Mod0220FallbackEnabled,
            _featureFlags.InstantiationRetryEnabled);

        return Response<InstantiationPrerequisitesModel>.Success(data, correlationId: request.CorrelationId);
    }
}
