using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.KpiCatalog.Commands;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.KpiCatalog.Handlers;

public sealed class EvaluateKpiCatalogReadinessHandler : IRequestHandler<EvaluateKpiCatalogReadinessCommand, Response<KpiCatalogReadinessDto>>
{
    private readonly IKpiCatalogReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateKpiCatalogReadinessHandler(IKpiCatalogReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<KpiCatalogReadinessDto>> Handle(EvaluateKpiCatalogReadinessCommand request, CancellationToken ct)
    {
        var tenant = KpiCatalogGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<KpiCatalogReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<KpiCatalogReadinessDto>.Fail("KpiCatalog readiness record was not found.", 404);
        }

        KpiCatalogGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<KpiCatalogReadinessDto>.Success(KpiCatalogMapper.ToDto(entity));
    }
}
